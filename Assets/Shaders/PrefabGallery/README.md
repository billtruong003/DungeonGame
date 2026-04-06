# Prefab Gallery — Unity Level Design Tool

Visual prefab browser organized by **Themes → Categories**, with folder scanning, auto preview generation, and drag-and-drop into scenes.

## Quick Start

1. Copy `PrefabGallery/` folder into `Assets/`
2. Open **Tools → Prefab Gallery** (`Ctrl+Shift+G`)
3. Create a Theme → Add Categories → Scan folders → Drag prefabs into scene

## Architecture

Only 1 ScriptableObject per theme — categories are embedded inline, no extra SO files needed.

## Scan Modes

- **Scan → Category**: Scan path, add all prefabs to selected category
- **Smart Scan**: Each subfolder becomes a category (e.g. Dungeon/Floors → "Floors")
- **Scan Selected**: Scan whatever folder is selected in Project window
- **Regen Previews**: Re-generate thumbnails for current category

## Features

- Drag & Drop into Scene with auto scale
- Scale Control (0.1x–5x) + exact input
- Adjustable grid thumbnail size
- Search filter by name
- Resizable sidebar
- Right-click context menus
- Inline category add/rename/delete
