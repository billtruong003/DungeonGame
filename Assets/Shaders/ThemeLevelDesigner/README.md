# Theme Level Designer — Unity 6 URP

A visual drag-and-drop level design system with theme-based sections, map canvas editor, and procedural dungeon generation.

## Installation

1. **Unzip** the `ThemeLevelDesigner` folder.
2. **Copy** it into your Unity project's `Assets/` folder (e.g. `Assets/ThemeLevelDesigner/`).
3. Unity will auto-compile. No extra packages required — works with built-in Unity 6 URP.

## Quick Start

### 1. Create a Theme

- Right-click in Project → **Create → Level Design → Theme**
- Name it (e.g. "Dungeon_Crypt"), set `Theme Color`
- **Add sections** — choose one of these fast workflows:

**Option A — Drag & Drop (fastest):**
Select prefabs in the Project window (multi-select with Shift/Ctrl), then drag them onto the **"Drag Prefabs or Folders Here"** drop zone in the Theme Inspector. You can also drag an entire folder — all prefabs inside will be added.

**Option B — Scan Folder:**
Click **"Scan Folder..."**, pick a folder, and all prefabs will be imported automatically.

**What happens automatically:**
- **Grid size** detected from mesh bounds, or parsed from the name (e.g. `Floor_Stone_4x4` → 4×4)
- **Tags** detected from prefab name keywords (floor, wall, door, prop, stair, trap, etc.)
- **Display name** cleaned up (removes prefixes like `SM_`, `Env_`, size suffixes, underscores)
- **Preview thumbnail** captured from prefab
- **Duplicates** skipped

**Naming convention (optional but recommended):**
Name prefabs like `Floor_Stone_4x4`, `Wall_Brick_1x3`, `Door_Iron_Arch` — the system will auto-detect size and tags from the name. Common prefixes (`SM_`, `Env_`, `Prop_`, etc.) are stripped automatically.

**Bulk tools in the Inspector:**
- "Rescan All Sizes" — re-detect grid sizes from bounds/names
- "Re-detect Tags" — re-scan all names for tag keywords
- "Generate Previews" — regenerate all thumbnails
- "Remove Missing" — clean up entries with deleted prefabs
- "Sort A-Z" — alphabetical sort

### 2. Open the Level Designer

- Menu: **Tools → Level Designer** (shortcut: `Ctrl+Shift+L`)
- The window has 3 panels:
  - **Left — Palette**: shows all sections from the selected theme
  - **Center — Map Canvas**: the grid where you build your layout
  - **Right — Inspector**: properties of the selected section

### 3. Build a Map

- Select your theme from the **Theme dropdown** (top-left)
- Filter sections by tag or use the **search bar**
- **Drag** a section from the palette onto the canvas
  - Green highlight = valid placement
  - Red highlight = overlapping
- **Left-click** a placed section to select it
- **R key** to rotate 90°
- **Delete/Backspace** to remove
- **Middle mouse** to pan, **scroll wheel** to zoom
- **F key** to reset view

### 4. Group into Rooms

- **Left-click drag** on empty canvas to box-select multiple sections
- **Right-click → "Group as Room"** to assign them to a room group
- Rooms get a unique color overlay for visual distinction

### 5. Replace Sections

- Select a placed section
- In the Inspector, click **"Replace Section..."**
- A dropdown shows all compatible sections (same grid size)
- Pick one to swap instantly

### 6. Save / Load

- **"New Map"** — create a fresh map
- **"Save Map"** — save as a ScriptableObject asset (`.asset`)
- **"Load Map"** — load an existing map asset

## Dungeon Generation

### Setup

1. Create some **Room** assets: Right-click → **Create → Level Design → Room**
   - Set room type (Start, Combat, Treasure, Boss, etc.)
   - Add section references and their offsets

2. Create a **Dungeon Config**: Right-click → **Create → Level Design → Dungeon Config**
   - Add rooms to the **Room Pool**
   - Set min/max rooms, critical path length
   - Adjust the **Difficulty Curve**
   - Set seed (0 = random)

3. In the Inspector, click **"Preview in Scene View"** to see the generated layout as wireframes

### Runtime

1. Add a `DungeonInstantiator` component to a GameObject
2. Assign either a **MapData** (hand-crafted) or **DungeonConfigSO** (procedural)
3. Toggle `Use Map Data` accordingly
4. At runtime, it instantiates all prefabs into the scene

You can also call it from code:
```csharp
var instantiator = GetComponent<DungeonInstantiator>();

// Hand-crafted map
instantiator.InstantiateMap(myMapData);

// Procedural
var dungeon = instantiator.InstantiateGenerated(myDungeonConfig);
Debug.Log($"Generated {dungeon.rooms.Count} rooms with seed {dungeon.seed}");
```

## Canvas Controls

| Input | Action |
|-------|--------|
| **Drag from palette** | Place section on grid |
| **Left-click** | Select section |
| **Left-drag (empty area)** | Box select |
| **Right-click** | Context menu (rotate, duplicate, delete, group) |
| **Middle mouse drag** | Pan canvas |
| **Scroll wheel** | Zoom in/out |
| **R** | Rotate selected section 90° |
| **Delete / Backspace** | Delete selected |
| **F** | Reset view |

## ScriptableObject Types

| Asset | Menu Path | Purpose |
|-------|-----------|---------|
| `ThemeSO` | Level Design → Theme | Holds sections grouped by visual theme |
| `MapData` | Level Design → Map Data | Stores placed sections layout |
| `RoomSO` | Level Design → Room | A reusable room template |
| `DungeonConfigSO` | Level Design → Dungeon Config | Rules for procedural generation |

## File Structure

```
ThemeLevelDesigner/
├── package.json
├── README.md
├── Runtime/
│   ├── ThemeLevelDesigner.Runtime.asmdef
│   ├── Data/
│   │   ├── ThemeSO.cs            # Theme with section list
│   │   ├── SectionEntry.cs       # Section data + snap points
│   │   ├── MapData.cs            # Placed sections + room groups
│   │   ├── RoomSO.cs             # Room template
│   │   └── DungeonConfigSO.cs    # Generation config
│   ├── Generator/
│   │   ├── DungeonGenerator.cs   # Graph-based dungeon gen
│   │   └── DungeonInstantiator.cs # Runtime prefab spawner
│   └── Utils/
│       ├── PreviewUtility.cs     # Auto-capture thumbnails
│       └── SectionAutoDetect.cs  # Auto-detect size, tags, names
└── Editor/
    ├── ThemeLevelDesigner.Editor.asmdef
    ├── USS/
    │   └── LevelDesignerStyles.uss
    └── Windows/
        ├── LevelDesignerWindow.cs       # Main editor window
        ├── MapCanvasElement.cs           # Grid canvas with pan/zoom/drag
        ├── LevelDesignerSceneView.cs     # SceneView wireframe preview
        ├── ThemeSOEditor.cs              # Theme custom inspector
        └── DungeonConfigSOEditor.cs      # Config inspector + gen preview
```

## Tips

- **Mix themes**: you can place sections from different themes on the same map
- **Preview in SceneView**: the wireframe overlay helps visualize the layout in 3D
- **Seed-based generation**: use a fixed seed for reproducible dungeons
- **Prefab workflow**: each section prefab should have its pivot at the bottom-left corner for correct grid alignment

## Requirements

- Unity 6 (6000.0+)
- URP (Universal Render Pipeline) — for preview rendering
- No third-party dependencies
