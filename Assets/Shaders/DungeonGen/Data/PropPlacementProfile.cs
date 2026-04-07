using UnityEngine;

namespace DungeonSystem.Data
{
    public enum PlacementSurface
    {
        Floor,
        Wall,
        Ceiling,
        Corner,
        OnFurniture   // child item placed on top of another prop
    }

    public enum FacingMode
    {
        FaceWall,       // front faces nearest wall
        FaceCenter,     // front faces room center
        FaceAway,       // front faces away from wall
        FaceRandom,     // random Y rotation
        FaceParent      // inherit rotation from parent anchor
    }

    public enum PropImportance
    {
        Major,    // must-have: bed in bedroom, chest in loot room
        Minor,    // nice-to-have: barrels, crates, candles
        Clutter   // tiny fill items: books on shelf, plates on table
    }

    /// <summary>
    /// Shared placement profile for props. One profile can be assigned to many prefabs.
    /// E.g. a single "Chair" profile works for 20 different chair models.
    /// 
    /// The key insight: anchorPoint defines where the prop contacts its surface,
    /// independent of the prefab's pivot. The placement system positions the prop
    /// so that anchorPoint lands exactly on the target surface point.
    /// </summary>
    [CreateAssetMenu(fileName = "PropProfile", menuName = "DungeonSystem/Prop Placement Profile")]
    public class PropPlacementProfile : ScriptableObject
    {
        [Header("Surface & Orientation")]
        [Tooltip("What surface this prop attaches to.")]
        public PlacementSurface surface = PlacementSurface.Floor;

        [Tooltip("How the prop faces when placed.")]
        public FacingMode facing = FacingMode.FaceRandom;

        [Header("Anchor Point")]
        [Tooltip("The point on the prefab that touches the placement surface. " +
                 "For floor props this is typically the bottom center (0,0,0 if pivot is at feet). " +
                 "For wall props this is the back-center that presses against the wall. " +
                 "Set once per profile — works for all prefabs sharing this profile.")]
        public Vector3 anchorPoint = Vector3.zero;

        [Tooltip("If true, auto-detect anchorPoint from mesh bounds bottom-center at placement time. " +
                 "Overrides the manual anchorPoint value.")]
        public bool autoDetectAnchor = true;

        [Header("Footprint & Clearance")]
        [Tooltip("2D footprint on the placement plane (width, depth). " +
                 "Used for collision reservation so props don't overlap.")]
        public Vector2 footprint = new Vector2(1f, 1f);

        [Tooltip("Vertical clearance needed above the prop. " +
                 "Prevents a painting from being placed behind a tall wardrobe.")]
        public float clearanceAbove = 0f;

        [Header("Placement Rules")]
        [Tooltip("How important is this prop to the room's identity?")]
        public PropImportance importance = PropImportance.Minor;

        [Tooltip("Minimum distance from other props of the same profile.")]
        [Min(0f)]
        public float minSeparation = 0f;

        [Tooltip("Wall inset: how far from the wall surface the prop should be. " +
                 "Positive = away from wall. Only used for Wall and Corner surfaces.")]
        public float wallOffset = 0f;

        [Tooltip("Height ratio on the wall where this prop should sit (0=floor, 1=ceiling). " +
                 "Only used for Wall surface type.")]
        [Range(0f, 1f)]
        public float wallHeightRatio = 0f;

        [Header("Hierarchy")]
        [Tooltip("Can other props be placed on top of this one?")]
        public bool canHoldChildren = false;

        [Tooltip("Anchor points for child items (local space). " +
                 "E.g. a table has anchors on its surface for plates/candles.")]
        public ChildAnchor[] childAnchors;

        [Header("Tags")]
        [Tooltip("Semantic tags for recipe matching: 'seating', 'lighting', 'storage', etc.")]
        public string[] tags;

        /// <summary>
        /// Compute the world position for this prop given a target surface point and rotation.
        /// The prop is positioned so that its anchor point lands exactly on surfacePoint.
        /// </summary>
        public Vector3 ComputeWorldPosition(Vector3 surfacePoint, Quaternion rotation, Vector3 scale, Vector3 autoAnchor)
        {
            Vector3 anchor = autoDetectAnchor ? autoAnchor : anchorPoint;
            Vector3 scaledAnchor = Vector3.Scale(anchor, scale);
            Vector3 rotatedAnchor = rotation * scaledAnchor;
            return surfacePoint - rotatedAnchor;
        }
    }

    [System.Serializable]
    public class ChildAnchor
    {
        [Tooltip("Position in parent's local space where a child can be placed.")]
        public Vector3 localPosition;

        [Tooltip("Forward direction for the child at this anchor.")]
        public Vector3 localForward = Vector3.forward;

        [Tooltip("What tags of child items are allowed here.")]
        public string[] allowedTags;

        [Tooltip("Max number of items at this anchor.")]
        public int maxItems = 1;
    }
}