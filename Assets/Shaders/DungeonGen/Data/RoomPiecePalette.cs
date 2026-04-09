using System;
using System.Collections.Generic;
using UnityEngine;
using DungeonSystem.Core;

namespace DungeonSystem.Data
{
    [CreateAssetMenu(fileName = "RoomPiecePalette", menuName = "DungeonSystem/Piece Palette")]
    public class RoomPiecePalette : ScriptableObject
    {
        [Header("Floor & Ceiling")]
        public PieceEntry[] floorTiles;
        public PieceEntry[] ceilingTiles;

        [Header("Walls & Doors")]
        public PieceEntry[] wallSegments;
        public PieceEntry[] doorFrames;
        public PieceEntry[] doorLockedFrames;
        public PieceEntry[] doorSecretFrames;

        [Header("Baseboards (floor-wall transition)")]
        [Tooltip("Horizontal trim pieces placed at the base of walls. " +
                 "Creates a clean transition between floor and wall surfaces.")]
        public PieceEntry[] baseboards;

        [Header("Structural")]
        public PieceEntry[] pillars;
        public PieceEntry[] mapPillars;

        [Header("Decorations & Props")]
        public PieceEntry[] torches;
        public PieceEntry[] wallProps;
        public PieceEntry[] cornerProps;
        public PieceEntry[] floorProps;
        public PieceEntry[] ceilingProps;

        [Header("Room Type Overrides")]
        public List<RoomTypePieceOverride> overrides;

        public PieceEntry[] GetFloorTiles(RoomType t) => Resolve(floorTiles, t, o => o.floorTiles);
        public PieceEntry[] GetBaseboards(RoomType t) => Resolve(baseboards, t, o => o.baseboards);
        public PieceEntry[] GetWallSegments(RoomType t) => Resolve(wallSegments, t, o => o.wallSegments);
        public PieceEntry[] GetDoorFrames(RoomType t) => Resolve(doorFrames, t, o => o.doorFrames);
        public PieceEntry[] GetPillars(RoomType t) => Resolve(pillars, t, o => o.pillars);
        public PieceEntry[] GetMapPillars(RoomType t) => Resolve(mapPillars, t, o => o.mapPillars);
        public PieceEntry[] GetCeilingTiles(RoomType t) => Resolve(ceilingTiles, t, o => o.ceilingTiles);
        public PieceEntry[] GetTorches(RoomType t) => Resolve(torches, t, o => o.torches);
        public PieceEntry[] GetWallProps(RoomType t) => Resolve(wallProps, t, o => o.wallProps);
        public PieceEntry[] GetCornerProps(RoomType t) => Resolve(cornerProps, t, o => o.cornerProps);
        public PieceEntry[] GetFloorProps(RoomType t) => Resolve(floorProps, t, o => o.floorProps);
        public PieceEntry[] GetCeilingProps(RoomType t) => Resolve(ceilingProps, t, o => o.ceilingProps);

        /// <summary>
        /// Get ALL prop entries (all categories) for a given room type.
        /// Used by PropPlacer for tag-based searching.
        /// </summary>
        public List<PieceEntry> GetAllProps(RoomType t)
        {
            var all = new List<PieceEntry>();
            AddRange(all, GetTorches(t));
            AddRange(all, GetWallProps(t));
            AddRange(all, GetCornerProps(t));
            AddRange(all, GetFloorProps(t));
            AddRange(all, GetCeilingProps(t));
            return all;
        }

        static void AddRange(List<PieceEntry> list, PieceEntry[] array)
        {
            if (array != null) list.AddRange(array);
        }

        PieceEntry[] Resolve(PieceEntry[] defaults, RoomType type, Func<RoomTypePieceOverride, PieceEntry[]> selector)
        {
            if (overrides == null) return defaults;
            foreach (var ov in overrides)
            {
                if (ov.roomType != type) continue;
                var result = selector(ov);
                if (result != null && result.Length > 0) return result;
            }
            return defaults;
        }
    }

    [Serializable]
    public class PieceEntry
    {
        public GameObject prefab;
        [Range(0.01f, 10f)] public float spawnWeight = 1f;
        public float widthOverride;
        public float heightOverride;
        public float depthOverride;

        [Tooltip("If true, this piece is already oriented horizontally (lying flat). " +
                 "The assembler will NOT rotate it 90° when placing as a baseboard. " +
                 "Use for meshes modeled in landscape orientation.")]
        public bool isPreRotatedHorizontal = false;

        [Tooltip("Shared placement profile. Defines anchor point, surface type, " +
                 "facing rules, footprint, and tags. Many prefabs can share one profile.")]
        public PropPlacementProfile placementProfile;
    }

    [Serializable]
    public class RoomTypePieceOverride
    {
        public RoomType roomType;
        public PieceEntry[] floorTiles;
        public PieceEntry[] baseboards;
        public PieceEntry[] wallSegments;
        public PieceEntry[] doorFrames;
        public PieceEntry[] pillars;
        public PieceEntry[] mapPillars;
        public PieceEntry[] ceilingTiles;
        public PieceEntry[] torches;
        public PieceEntry[] wallProps;
        public PieceEntry[] cornerProps;
        public PieceEntry[] floorProps;
        public PieceEntry[] ceilingProps;
    }
}
