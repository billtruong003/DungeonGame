namespace DungeonSystem.Core
{
    /// <summary>
    /// Centralised tag constants for prop matching.
    /// Use these instead of raw strings to avoid typos and enable IDE autocomplete.
    /// 
    /// Tags are grouped by semantic category.
    /// A single prop can carry multiple tags (e.g. a torch is both "lighting" and "wall_decor").
    /// 
    /// Usage in PropPlacementProfile:
    ///   tags = new[] { PropTags.Lighting, PropTags.WallDecor };
    /// 
    /// Usage in RoomRecipe (RecipePropEntry.requiredTags):
    ///   requiredTags = new[] { PropTags.Lighting };
    ///   → matches any prop that has the "lighting" tag.
    /// </summary>
    public static class PropTags
    {
        // ── Furniture ──────────────────────────────────────
        public const string Seating    = "seating";     // chairs, stools, benches
        public const string Table      = "table";       // tables, desks, counters
        public const string Bed        = "bed";         // beds, cots, sleeping bags
        public const string Storage    = "storage";     // chests, crates, barrels, wardrobes
        public const string Shelf      = "shelf";       // bookshelves, weapon racks, display cases
        public const string Altar      = "altar";       // altars, shrines, ritual tables

        // ── Lighting ───────────────────────────────────────
        public const string Lighting   = "lighting";    // torches, candles, lanterns, chandeliers
        public const string Campfire   = "campfire";    // campfires, braziers, fire pits

        // ── Decoration ─────────────────────────────────────
        public const string WallDecor  = "wall_decor";  // paintings, banners, shields, mounted heads
        public const string FloorDecor = "floor_decor"; // rugs, floor runes, puddles
        public const string CeilDecor  = "ceil_decor";  // hanging chains, stalactites, chandeliers
        public const string Pillar     = "pillar";      // decorative pillars, columns
        public const string Statue     = "statue";      // statues, busts, gargoyles

        // ── Clutter (child items on furniture) ─────────────
        public const string Book       = "book";        // books, scrolls, tomes
        public const string Dish       = "dish";        // plates, bowls, cups, goblets
        public const string Potion     = "potion";      // bottles, flasks, vials
        public const string Tool       = "tool";        // hammers, tongs, quills, inkwells
        public const string Food       = "food";        // bread, cheese, meat, fruit

        // ── Structural / Functional ────────────────────────
        public const string Door       = "door";        // door-related props
        public const string Stairs     = "stairs";      // staircase elements
        public const string Cage       = "cage";        // cages, prison bars
        public const string Trap       = "trap";        // spike traps, pressure plates
        public const string Mechanism  = "mechanism";   // levers, buttons, gears, pulleys

        // ── Nature / Organic ───────────────────────────────
        public const string Plant      = "plant";       // vines, mushrooms, potted plants
        public const string Rock       = "rock";        // boulders, rubble, stalactites
        public const string Water      = "water";       // fountains, wells, pools, puddles
        public const string Bones      = "bones";       // skulls, skeletons, bone piles

        // ── Commerce ───────────────────────────────────────
        public const string Goods      = "goods";       // shop display items, wares
        public const string Sign       = "sign";        // shop signs, directional signs

        /// <summary>
        /// All known tags. Useful for validation and dropdowns.
        /// </summary>
        public static readonly string[] All = new[]
        {
            Seating, Table, Bed, Storage, Shelf, Altar,
            Lighting, Campfire,
            WallDecor, FloorDecor, CeilDecor, Pillar, Statue,
            Book, Dish, Potion, Tool, Food,
            Door, Stairs, Cage, Trap, Mechanism,
            Plant, Rock, Water, Bones,
            Goods, Sign
        };
    }
}
