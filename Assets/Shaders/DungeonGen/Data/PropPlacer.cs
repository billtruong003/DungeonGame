using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DungeonSystem.Core;
using DungeonSystem.Data;
using DungeonSystem.Layout;

namespace DungeonSystem.Runtime
{
    /// <summary>
    /// Multi-pass prop placement engine.
    /// 
    /// Replaces the ad-hoc DecorateProps / PlaceWallProp / PlaceStandaloneProp
    /// methods in PieceAssembler with a structured approach:
    /// 
    ///   Pass 1: Major props — furniture that defines the room (beds, tables, altars)
    ///   Pass 2: Minor props — secondary decoration (barrels, crates, weapon racks)
    ///   Pass 3: Wall/ceiling props — torches, paintings, banners
    ///   Pass 4: Clutter — child items on furniture anchors (books, plates, candles)
    ///   Pass 5: Spawn points — gameplay entities (enemies, chests, traps)
    /// 
    /// Key feature: Footprint Reservation Grid
    ///   Before instantiating anything, all placements are planned on a 2D grid.
    ///   Each placed prop reserves its footprint so subsequent props don't overlap.
    ///   Only after planning is complete are GameObjects instantiated.
    /// </summary>
    public class PropPlacer
    {
        readonly DungeonConfig _config;
        readonly RoomPiecePalette _palette;
        readonly System.Random _rng;
        readonly Dictionary<int, PropBounds> _boundsCache = new();

        // Reservation grid: discretized at 0.25m resolution
        const float GRID_RES = 0.25f;

        public PropPlacer(DungeonConfig config, RoomPiecePalette palette, System.Random rng)
        {
            _config = config;
            _palette = palette;
            _rng = rng;
        }

        /// <summary>
        /// Decorate a room using its recipe and the piece palette.
        /// Call after structural assembly (floor, walls, pillars) is complete.
        /// </summary>
        public void DecoratRoom(
            PlacedRoom placed,
            RoomInstance instance,
            RoomRecipe recipe,
            FloorLayout layout)
        {
            if (recipe == null) return;

            Transform root = instance.transform;
            RoomType type = placed.Node.Type;
            float sizeX = placed.Width * _config.cellSize;
            float sizeZ = placed.Height * _config.cellSize;
            float halfW = sizeX * 0.5f;
            float halfH = sizeZ * 0.5f;
            float wallHeight = GetWallHeight(type);

            // Create reservation grid
            var grid = new ReservationGrid(sizeX, sizeZ, GRID_RES);

            // Container for all props
            var propRoot = new GameObject("Props");
            propRoot.transform.SetParent(root);
            propRoot.transform.localPosition = Vector3.zero;

            // Sort recipe entries by importance: Major first, then Minor, then Clutter
            var sorted = recipe.props
                .OrderBy(p => p.importance)
                .ToList();

            float density = recipe.densityMultiplier;

            foreach (var entry in sorted)
            {
                if (grid.FillRatio >= recipe.maxFillRatio) break;

                // Find matching props from palette by tags
                PieceEntry[] candidates = FindCandidatesByTags(entry.requiredTags, type);
                if (candidates == null || candidates.Length == 0) continue;

                int count = DetermineCount(entry, density, placed.Width, placed.Height);
                int placed_count = 0;

                for (int i = 0; i < count; i++)
                {
                    if (grid.FillRatio >= recipe.maxFillRatio) break;
                    if (_rng.NextDouble() > entry.chance && placed_count >= entry.minCount) continue;

                    PieceEntry piece = WeightedPick(candidates);
                    if (piece == null || piece.prefab == null) continue;

                    var profile = piece.placementProfile;
                    if (profile == null)
                    {
                        // Fallback: place on floor with auto anchor
                        PlaceFallback(propRoot.transform, piece, halfW, halfH, grid);
                        placed_count++;
                        continue;
                    }

                    bool success = false;
                    switch (profile.surface)
                    {
                        case PlacementSurface.Floor:
                            success = PlaceOnFloor(propRoot.transform, piece, profile, entry,
                                halfW, halfH, wallHeight, grid);
                            break;
                        case PlacementSurface.Wall:
                            success = PlaceOnWall(propRoot.transform, piece, profile,
                                halfW, halfH, wallHeight, grid);
                            break;
                        case PlacementSurface.Ceiling:
                            success = PlaceOnCeiling(propRoot.transform, piece, profile,
                                halfW, halfH, wallHeight, grid);
                            break;
                        case PlacementSurface.Corner:
                            success = PlaceInCorner(propRoot.transform, piece, profile,
                                halfW, halfH, wallHeight, grid);
                            break;
                    }

                    if (success) placed_count++;
                }
            }

            // Place spawn points from recipe
            PlaceSpawnPoints(propRoot.transform, recipe, sizeX, sizeZ, grid);
        }

        // ======================== FLOOR PLACEMENT ========================

        bool PlaceOnFloor(Transform parent, PieceEntry piece, PropPlacementProfile profile,
            RecipePropEntry entry, float halfW, float halfH, float wallHeight, ReservationGrid grid)
        {
            PropBounds bounds = MeasureBounds(piece);
            Vector3 anchor = GetEffectiveAnchor(profile, bounds);
            Vector2 footprint = GetEffectiveFootprint(profile, bounds);

            // Determine target zone based on placement hints
            float marginX = footprint.x * 0.5f + 0.1f;
            float marginZ = footprint.y * 0.5f + 0.1f;

            float minX, maxX, minZ, maxZ;

            if (entry.preferCenter)
            {
                float zone = 0.4f;
                minX = -halfW * zone; maxX = halfW * zone;
                minZ = -halfH * zone; maxZ = halfH * zone;
            }
            else if (entry.preferWalls)
            {
                // Place near a random wall
                int wall = _rng.Next(4);
                float inset = footprint.y * 0.5f + profile.wallOffset + 0.2f;
                switch (wall)
                {
                    case 0: // North
                        minX = -halfW + marginX; maxX = halfW - marginX;
                        minZ = halfH - inset - 0.1f; maxZ = halfH - inset + 0.1f;
                        break;
                    case 1: // South
                        minX = -halfW + marginX; maxX = halfW - marginX;
                        minZ = -halfH + inset - 0.1f; maxZ = -halfH + inset + 0.1f;
                        break;
                    case 2: // East
                        minX = halfW - inset - 0.1f; maxX = halfW - inset + 0.1f;
                        minZ = -halfH + marginZ; maxZ = halfH - marginZ;
                        break;
                    default: // West
                        minX = -halfW + inset - 0.1f; maxX = -halfW + inset + 0.1f;
                        minZ = -halfH + marginZ; maxZ = halfH - marginZ;
                        break;
                }
            }
            else if (entry.preferCorners)
            {
                // Pick a random corner
                int c = _rng.Next(4);
                float cx = (c % 2 == 0) ? (-halfW + marginX + 0.5f) : (halfW - marginX - 0.5f);
                float cz = (c < 2) ? (-halfH + marginZ + 0.5f) : (halfH - marginZ - 0.5f);
                minX = cx - 0.2f; maxX = cx + 0.2f;
                minZ = cz - 0.2f; maxZ = cz + 0.2f;
            }
            else
            {
                // Random anywhere with wall margin
                float wallThick = 0.5f;
                minX = -halfW + marginX + wallThick;
                maxX = halfW - marginX - wallThick;
                minZ = -halfH + marginZ + wallThick;
                maxZ = halfH - marginZ - wallThick;
            }

            // Try multiple random positions
            for (int attempt = 0; attempt < 15; attempt++)
            {
                float x = Mathf.Lerp(minX, maxX, (float)_rng.NextDouble());
                float z = Mathf.Lerp(minZ, maxZ, (float)_rng.NextDouble());

                if (!grid.CanPlace(x, z, footprint.x, footprint.y)) continue;
                if (profile.minSeparation > 0 && !grid.CheckSeparation(x, z, profile.minSeparation))
                    continue;

                // Compute facing rotation
                Quaternion rot = ComputeFacingRotation(profile.facing, x, z, halfW, halfH);

                // Anchor-based positioning: place so anchor lands at (x, 0, z)
                Vector3 surfacePoint = new Vector3(x, 0f, z);
                Vector3 worldPos = profile.ComputeWorldPosition(surfacePoint, rot, Vector3.one, anchor);

                var go = Object.Instantiate(piece.prefab, parent);
                go.transform.localPosition = worldPos;
                go.transform.localRotation = rot;

                grid.Reserve(x, z, footprint.x, footprint.y);

                // Place child items on anchors
                if (profile.canHoldChildren && profile.childAnchors != null)
                    FillChildAnchors(go.transform, profile, piece.prefab.transform);

                return true;
            }

            return false;
        }

        // ======================== WALL PLACEMENT ========================

        bool PlaceOnWall(Transform parent, PieceEntry piece, PropPlacementProfile profile,
            float halfW, float halfH, float wallHeight, ReservationGrid grid)
        {
            PropBounds bounds = MeasureBounds(piece);
            Vector3 anchor = GetEffectiveAnchor(profile, bounds);

            // Pick a random wall
            int wall = _rng.Next(4);
            float wallLen = (wall < 2) ? halfW * 2f : halfH * 2f;
            float halfLen = wallLen * 0.5f;

            for (int attempt = 0; attempt < 10; attempt++)
            {
                float t = (float)(_rng.NextDouble() * 0.7 + 0.15) * wallLen - halfLen;
                float targetY = profile.wallHeightRatio * wallHeight;

                Vector3 surfacePoint;
                Quaternion rot;

                switch (wall)
                {
                    case 0: // North
                        surfacePoint = new Vector3(t, targetY, halfH - profile.wallOffset);
                        rot = Quaternion.Euler(0, 180, 0);
                        break;
                    case 1: // South
                        surfacePoint = new Vector3(t, targetY, -halfH + profile.wallOffset);
                        rot = Quaternion.identity;
                        break;
                    case 2: // East
                        surfacePoint = new Vector3(halfW - profile.wallOffset, targetY, t);
                        rot = Quaternion.Euler(0, 270, 0);
                        break;
                    default: // West
                        surfacePoint = new Vector3(-halfW + profile.wallOffset, targetY, t);
                        rot = Quaternion.Euler(0, 90, 0);
                        break;
                }

                // Check floor reservation beneath wall prop
                Vector2 fp = GetEffectiveFootprint(profile, bounds);
                float floorX = surfacePoint.x;
                float floorZ = surfacePoint.z;

                if (profile.clearanceAbove > 0 || fp.x > 0.1f)
                {
                    if (!grid.CanPlace(floorX, floorZ, fp.x, fp.y)) continue;
                }

                Vector3 worldPos = profile.ComputeWorldPosition(surfacePoint, rot, Vector3.one, anchor);

                var go = Object.Instantiate(piece.prefab, parent);
                go.transform.localPosition = worldPos;
                go.transform.localRotation = rot;

                if (fp.x > 0.1f)
                    grid.Reserve(floorX, floorZ, fp.x, fp.y);

                return true;
            }

            return false;
        }

        // ======================== CEILING PLACEMENT ========================

        bool PlaceOnCeiling(Transform parent, PieceEntry piece, PropPlacementProfile profile,
            float halfW, float halfH, float wallHeight, ReservationGrid grid)
        {
            PropBounds bounds = MeasureBounds(piece);
            Vector3 anchor = GetEffectiveAnchor(profile, bounds);
            // For ceiling, anchor is the top of the object
            if (profile.autoDetectAnchor)
                anchor = new Vector3(bounds.center.x, bounds.center.y + bounds.size.y * 0.5f, bounds.center.z);

            float ceilingY = _config.wallHeightOffset +
                Mathf.Max(1, _config.roomHeightMultiplier) * (wallHeight - _config.pieceOverlap);

            for (int attempt = 0; attempt < 10; attempt++)
            {
                float x = (float)(_rng.NextDouble() * halfW * 1.4f - halfW * 0.7f);
                float z = (float)(_rng.NextDouble() * halfH * 1.4f - halfH * 0.7f);

                Vector3 surfacePoint = new Vector3(x, ceilingY, z);
                Quaternion rot = Quaternion.Euler(0, (float)_rng.NextDouble() * 360f, 0);

                Vector3 worldPos = profile.ComputeWorldPosition(surfacePoint, rot, Vector3.one, anchor);

                var go = Object.Instantiate(piece.prefab, parent);
                go.transform.localPosition = worldPos;
                go.transform.localRotation = rot;

                return true;
            }

            return false;
        }

        // ======================== CORNER PLACEMENT ========================

        bool PlaceInCorner(Transform parent, PieceEntry piece, PropPlacementProfile profile,
            float halfW, float halfH, float wallHeight, ReservationGrid grid)
        {
            PropBounds bounds = MeasureBounds(piece);
            Vector3 anchor = GetEffectiveAnchor(profile, bounds);
            Vector2 fp = GetEffectiveFootprint(profile, bounds);

            // 4 corners, try each in random order
            var corners = new (float x, float z, float rotY)[]
            {
                (-halfW, +halfH, 135f),
                (+halfW, +halfH, -135f),
                (-halfW, -halfH, 45f),
                (+halfW, -halfH, -45f)
            };
            Shuffle(corners);

            float inset = Mathf.Max(fp.x, fp.y) * 0.5f + profile.wallOffset + 0.3f;

            foreach (var (cx, cz, rotY) in corners)
            {
                float ix = cx > 0 ? cx - inset : cx + inset;
                float iz = cz > 0 ? cz - inset : cz + inset;

                if (!grid.CanPlace(ix, iz, fp.x, fp.y)) continue;

                Quaternion rot = Quaternion.Euler(0, rotY, 0);
                Vector3 surfacePoint = new Vector3(ix, 0f, iz);
                Vector3 worldPos = profile.ComputeWorldPosition(surfacePoint, rot, Vector3.one, anchor);

                var go = Object.Instantiate(piece.prefab, parent);
                go.transform.localPosition = worldPos;
                go.transform.localRotation = rot;

                grid.Reserve(ix, iz, fp.x, fp.y);
                return true;
            }

            return false;
        }

        // ======================== CHILD ANCHORS ========================

        void FillChildAnchors(Transform parent, PropPlacementProfile profile, Transform prefabRef)
        {
            if (profile.childAnchors == null) return;

            foreach (var anchor in profile.childAnchors)
            {
                if (anchor.allowedTags == null || anchor.allowedTags.Length == 0) continue;

                // Find clutter props matching the anchor's allowed tags
                PieceEntry[] candidates = FindCandidatesByTags(anchor.allowedTags, RoomType.Combat);
                if (candidates == null || candidates.Length == 0) continue;

                for (int i = 0; i < anchor.maxItems; i++)
                {
                    if (_rng.NextDouble() > 0.7) continue;

                    PieceEntry piece = WeightedPick(candidates);
                    if (piece == null || piece.prefab == null) continue;

                    PropBounds childBounds = MeasureBounds(piece);
                    Vector3 childAnchor = GetEffectiveAnchor(
                        piece.placementProfile, childBounds);

                    Quaternion rot = Quaternion.LookRotation(
                        anchor.localForward != Vector3.zero ? anchor.localForward : Vector3.forward);

                    Vector3 worldPos = anchor.localPosition - rot * Vector3.Scale(childAnchor, Vector3.one);

                    var go = Object.Instantiate(piece.prefab, parent);
                    go.transform.localPosition = worldPos;
                    go.transform.localRotation = rot;
                }
            }
        }

        // ======================== FALLBACK (no profile) ========================

        void PlaceFallback(Transform parent, PieceEntry piece, float halfW, float halfH, ReservationGrid grid)
        {
            PropBounds bounds = MeasureBounds(piece);
            Vector3 anchor = new Vector3(bounds.center.x, bounds.center.y - bounds.size.y * 0.5f, bounds.center.z);

            for (int attempt = 0; attempt < 10; attempt++)
            {
                float x = (float)(_rng.NextDouble() * halfW * 1.2f - halfW * 0.6f);
                float z = (float)(_rng.NextDouble() * halfH * 1.2f - halfH * 0.6f);

                if (!grid.CanPlace(x, z, bounds.size.x, bounds.size.z)) continue;

                Quaternion rot = Quaternion.Euler(0, (float)_rng.NextDouble() * 360f, 0);
                Vector3 surfacePoint = new Vector3(x, 0f, z);
                Vector3 worldPos = surfacePoint - rot * anchor;

                var go = Object.Instantiate(piece.prefab, parent);
                go.transform.localPosition = worldPos;
                go.transform.localRotation = rot;

                grid.Reserve(x, z, bounds.size.x, bounds.size.z);
                return;
            }
        }

        // ======================== SPAWN POINTS ========================

        void PlaceSpawnPoints(Transform parent, RoomRecipe recipe, float sizeX, float sizeZ, ReservationGrid grid)
        {
            if (recipe.spawnPoints == null) return;

            var spawnRoot = new GameObject("SpawnPoints");
            spawnRoot.transform.SetParent(parent);
            spawnRoot.transform.localPosition = Vector3.zero;

            float halfW = sizeX * 0.5f;
            float halfH = sizeZ * 0.5f;
            float margin = Mathf.Min(2.0f, Mathf.Min(sizeX, sizeZ) * 0.15f);

            foreach (var entry in recipe.spawnPoints)
            {
                for (int i = 0; i < entry.count; i++)
                {
                    Vector3 pos = ComputeSpawnPosition(entry.placement, i, entry.count,
                        halfW - margin, halfH - margin);

                    var spGO = new GameObject($"Spawn_{entry.pointType}_{i}");
                    spGO.transform.SetParent(spawnRoot.transform);
                    spGO.transform.localPosition = pos;
                    var sp = spGO.AddComponent<SpawnPoint>();
                    sp.pointType = entry.pointType;
                    sp.priority = entry.priority;
                }
            }
        }

        Vector3 ComputeSpawnPosition(SpawnPlacement placement, int index, int total, float sx, float sz)
        {
            switch (placement)
            {
                case SpawnPlacement.Center:
                    return Vector3.zero;

                case SpawnPlacement.Corners:
                    var corners = new Vector3[]
                    {
                        new(-sx * 0.7f, 0, +sz * 0.7f),
                        new(+sx * 0.7f, 0, +sz * 0.7f),
                        new(-sx * 0.7f, 0, -sz * 0.7f),
                        new(+sx * 0.7f, 0, -sz * 0.7f)
                    };
                    return corners[index % corners.Length];

                case SpawnPlacement.Edges:
                    float t = total > 1 ? (float)index / (total - 1) : 0.5f;
                    return new Vector3(Mathf.Lerp(-sx * 0.6f, sx * 0.6f, t), 0, 0);

                case SpawnPlacement.NearEntrance:
                    return new Vector3(0, 0, -sz * 0.6f);

                case SpawnPlacement.FarFromEntrance:
                    return new Vector3(0, 0, sz * 0.6f);

                default: // Random
                    return new Vector3(
                        (float)(_rng.NextDouble() * sx * 1.2f - sx * 0.6f),
                        0,
                        (float)(_rng.NextDouble() * sz * 1.2f - sz * 0.6f));
            }
        }

        // ======================== HELPERS ========================

        float GetWallHeight(RoomType type)
        {
            PieceEntry[] walls = _palette.GetWallSegments(type);
            return (walls != null && walls.Length > 0) ? MeasureBounds(walls[0]).size.y : 3f;
        }

        Vector3 GetEffectiveAnchor(PropPlacementProfile profile, PropBounds bounds)
        {
            if (profile == null || profile.autoDetectAnchor)
            {
                // Auto: bottom-center of bounds
                return new Vector3(
                    bounds.center.x,
                    bounds.center.y - bounds.size.y * 0.5f,
                    bounds.center.z);
            }
            return profile.anchorPoint;
        }

        Vector2 GetEffectiveFootprint(PropPlacementProfile profile, PropBounds bounds)
        {
            if (profile != null && (profile.footprint.x > 0.01f || profile.footprint.y > 0.01f))
                return profile.footprint;
            return new Vector2(bounds.size.x, bounds.size.z);
        }

        Quaternion ComputeFacingRotation(FacingMode mode, float x, float z, float halfW, float halfH)
        {
            switch (mode)
            {
                case FacingMode.FaceWall:
                    // Face the nearest wall
                    float dN = halfH - z, dS = z + halfH, dE = halfW - x, dW = x + halfW;
                    float min = Mathf.Min(dN, Mathf.Min(dS, Mathf.Min(dE, dW)));
                    if (min == dN) return Quaternion.Euler(0, 0, 0);
                    if (min == dS) return Quaternion.Euler(0, 180, 0);
                    if (min == dE) return Quaternion.Euler(0, 90, 0);
                    return Quaternion.Euler(0, 270, 0);

                case FacingMode.FaceCenter:
                    float angle = Mathf.Atan2(-x, -z) * Mathf.Rad2Deg;
                    return Quaternion.Euler(0, angle, 0);

                case FacingMode.FaceAway:
                    float awayAngle = Mathf.Atan2(x, z) * Mathf.Rad2Deg;
                    return Quaternion.Euler(0, awayAngle, 0);

                default: // FaceRandom
                    return Quaternion.Euler(0, (float)_rng.NextDouble() * 360f, 0);
            }
        }

        PieceEntry[] FindCandidatesByTags(string[] tags, RoomType type)
        {
            if (tags == null || tags.Length == 0) return null;

            var tagSet = new HashSet<string>(tags);
            var results = new List<PieceEntry>();

            // Search all prop categories in palette
            SearchArray(_palette.GetFloorProps(type), tagSet, results);
            SearchArray(_palette.GetWallProps(type), tagSet, results);
            SearchArray(_palette.GetCornerProps(type), tagSet, results);
            SearchArray(_palette.GetCeilingProps(type), tagSet, results);
            SearchArray(_palette.GetTorches(type), tagSet, results);

            return results.Count > 0 ? results.ToArray() : null;
        }

        void SearchArray(PieceEntry[] entries, HashSet<string> tags, List<PieceEntry> results)
        {
            if (entries == null) return;
            foreach (var e in entries)
            {
                if (e?.placementProfile?.tags == null) continue;
                foreach (var t in e.placementProfile.tags)
                {
                    if (tags.Contains(t))
                    {
                        results.Add(e);
                        break;
                    }
                }
            }
        }

        int DetermineCount(RecipePropEntry entry, float density, int roomW, int roomH)
        {
            int max = entry.maxCount > 0
                ? entry.maxCount
                : Mathf.Max(1, roomW * roomH);
            int count = Mathf.RoundToInt(max * density);
            return Mathf.Max(entry.minCount, count);
        }

        // ======================== BOUNDS ========================

        struct PropBounds
        {
            public Vector3 size;
            public Vector3 center;
        }

        PropBounds MeasureBounds(PieceEntry entry)
        {
            if (entry == null || entry.prefab == null)
                return new PropBounds { size = Vector3.one, center = Vector3.zero };

            int id = entry.prefab.GetInstanceID();
            if (_boundsCache.TryGetValue(id, out var cached)) return cached;

            var instance = Object.Instantiate(entry.prefab);
            instance.transform.position = Vector3.zero;
            instance.transform.rotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            instance.SetActive(true);

            Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
            bool init = false;

            foreach (var r in instance.GetComponentsInChildren<Renderer>(true))
            {
                if (!init) { bounds = r.bounds; init = true; }
                else bounds.Encapsulate(r.bounds);
            }

            if (!init)
                foreach (var col in instance.GetComponentsInChildren<Collider>(true))
                {
                    if (!init) { bounds = col.bounds; init = true; }
                    else bounds.Encapsulate(col.bounds);
                }

            Object.DestroyImmediate(instance);

            Vector3 size = init ? bounds.size : Vector3.one;
            size.x = Mathf.Max(size.x, 0.01f);
            size.y = Mathf.Max(size.y, 0.01f);
            size.z = Mathf.Max(size.z, 0.01f);

            var result = new PropBounds
            {
                size = size,
                center = init ? bounds.center : Vector3.zero
            };

            // Apply overrides from PieceEntry
            if (entry.widthOverride > 0) result.size.x = entry.widthOverride;
            if (entry.heightOverride > 0) result.size.y = entry.heightOverride;
            if (entry.depthOverride > 0) result.size.z = entry.depthOverride;

            _boundsCache[id] = result;
            return result;
        }

        PieceEntry WeightedPick(PieceEntry[] entries)
        {
            if (entries == null || entries.Length == 0) return null;
            if (entries.Length == 1) return entries[0];

            float total = 0f;
            foreach (var e in entries) total += Mathf.Max(e.spawnWeight, 0.01f);

            float roll = (float)(_rng.NextDouble() * total);
            float acc = 0f;
            foreach (var e in entries)
            {
                acc += Mathf.Max(e.spawnWeight, 0.01f);
                if (roll <= acc) return e;
            }
            return entries[^1];
        }

        void Shuffle<T>(T[] array)
        {
            for (int i = array.Length - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                (array[i], array[j]) = (array[j], array[i]);
            }
        }
    }

    // ======================== RESERVATION GRID ========================

    /// <summary>
    /// Simple 2D grid that tracks which areas are occupied.
    /// Props reserve rectangular footprints before instantiation.
    /// Prevents overlapping placements without expensive physics queries.
    /// </summary>
    public class ReservationGrid
    {
        readonly bool[,] _grid;
        readonly float _res;
        readonly int _gridW, _gridH;
        readonly float _halfW, _halfH;
        int _reservedCells;
        int _totalCells;

        public float FillRatio => _totalCells > 0 ? (float)_reservedCells / _totalCells : 0f;

        public ReservationGrid(float worldW, float worldH, float resolution)
        {
            _res = resolution;
            _halfW = worldW * 0.5f;
            _halfH = worldH * 0.5f;
            _gridW = Mathf.CeilToInt(worldW / resolution);
            _gridH = Mathf.CeilToInt(worldH / resolution);
            _grid = new bool[_gridW, _gridH];
            _totalCells = _gridW * _gridH;
        }

        public bool CanPlace(float cx, float cz, float w, float h)
        {
            GetGridRect(cx, cz, w, h, out int minGX, out int minGZ, out int maxGX, out int maxGZ);

            for (int gx = minGX; gx <= maxGX; gx++)
                for (int gz = minGZ; gz <= maxGZ; gz++)
                    if (gx >= 0 && gx < _gridW && gz >= 0 && gz < _gridH && _grid[gx, gz])
                        return false;
            return true;
        }

        public void Reserve(float cx, float cz, float w, float h)
        {
            GetGridRect(cx, cz, w, h, out int minGX, out int minGZ, out int maxGX, out int maxGZ);

            for (int gx = minGX; gx <= maxGX; gx++)
                for (int gz = minGZ; gz <= maxGZ; gz++)
                    if (gx >= 0 && gx < _gridW && gz >= 0 && gz < _gridH && !_grid[gx, gz])
                    {
                        _grid[gx, gz] = true;
                        _reservedCells++;
                    }
        }

        public bool CheckSeparation(float cx, float cz, float minDist)
        {
            // Check if any reserved cell is within minDist
            GetGridRect(cx, cz, minDist * 2, minDist * 2, out int minGX, out int minGZ, out int maxGX, out int maxGZ);

            for (int gx = minGX; gx <= maxGX; gx++)
                for (int gz = minGZ; gz <= maxGZ; gz++)
                    if (gx >= 0 && gx < _gridW && gz >= 0 && gz < _gridH && _grid[gx, gz])
                        return false;
            return true;
        }

        void GetGridRect(float cx, float cz, float w, float h,
            out int minGX, out int minGZ, out int maxGX, out int maxGZ)
        {
            float worldMinX = cx - w * 0.5f + _halfW;
            float worldMinZ = cz - h * 0.5f + _halfH;
            float worldMaxX = cx + w * 0.5f + _halfW;
            float worldMaxZ = cz + h * 0.5f + _halfH;

            minGX = Mathf.FloorToInt(worldMinX / _res);
            minGZ = Mathf.FloorToInt(worldMinZ / _res);
            maxGX = Mathf.CeilToInt(worldMaxX / _res);
            maxGZ = Mathf.CeilToInt(worldMaxZ / _res);
        }
    }
}