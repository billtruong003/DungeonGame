# Stylized Texture Baker — Unity Editor Tool

Mesh-aware stylized texture baking tool for Unity. Bakes curvature-driven outlines, cel-shading, hatching, painterly strokes, and weathering directly into UV-space textures using geometry data from your mesh.

## Requirements

- Unity 2021.3+ with Universal Render Pipeline (URP)
- Project set to Linear Color Space (Edit → Project Settings → Player → Color Space → Linear)
- GPU with Compute Shader support

## Installation

1. Copy the `StylizedTextureBaker` folder into your project's `Assets/` directory
2. Open via **Tools → Stylized Texture Baker**

## Pipeline

```
Mesh + UV + Texture
      │
      ▼
[1] Mesh Data Extraction → Curvature, Normal, Position, AO, Edge Mask, Directional Field
      │
      ▼
[2] Edge Detection → Sobel on texture + geometry edges → Composite Edge Map
      │
      ▼
[3] Stylization → Modular layers: Outline, Cel Shading, Hatching, Painterly, Weathering
      │
      ▼
[4] UV Composite → Seam blending + Edge padding (mipmap-safe dilation)
      │
      ▼
[5] Export → PNG / TGA / EXR with optional data map and outline mask outputs
```

## Style Modules

| Module | Description |
|--------|-------------|
| **Outline** | Curvature-modulated ink strokes along geometry and texture edges. Optional brush texture with jitter. |
| **Cel Shading** | Tone quantization into N bands. Optional baked directional light and AO integration. Custom ramp texture support. |
| **Hatching** | Surface-flow-aligned line hatching. Cross-hatch for dark regions. Pressure variation via noise. |
| **Painterly** | Directional smear along principal curvature directions. Color jitter and saturation boost. |
| **Weathering** | Convex edge wear (bright/worn) and concave cavity grime (dark/dirty). Noise-driven breakup. |

Each module is an independent layer with blend mode (Normal, Multiply, Screen, Overlay, Add, SoftLight), opacity, and ordering. Solo any layer to preview it in isolation.

## Workflow

1. Assign a **MeshFilter** and **Source Texture**
2. Choose **Resolution** (256–4096)
3. Click **Bake Data Maps** to preview curvature, normals, AO, edges
4. Add **Style Layers** and configure each
5. Click **BAKE & EXPORT**
6. Find outputs in your configured output folder

## Presets

Save and load style configurations as ScriptableObject assets. Ship presets across projects or share with your team.

## File Structure

```
StylizedTextureBaker/
├── Editor/
│   ├── Data/              ScriptableObjects and data containers
│   ├── Styles/            Style module implementations
│   ├── Utility/           Mesh analysis, seam blending, shader loading
│   ├── BakerPipeline.cs   Pipeline orchestrator
│   ├── EdgeDetector.cs    Stage 2: edge detection
│   ├── MeshDataExtractor  Stage 1: geometry bake
│   ├── PreviewRenderer    3D preview viewport
│   ├── StylizationEngine  Stage 3: style compositing
│   ├── StylizedBakerWindow Main editor window
│   ├── TextureExporter    Stage 5: file export
│   └── UVCompositor       Stage 4: seam + padding
├── Shaders/
│   ├── Compute/           All GPU compute shaders
│   └── Preview/           Preview render shaders
└── Resources/             Brush textures, hatch patterns, default presets
```
