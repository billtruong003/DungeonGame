# BillVRCore

Physics-based VR interaction framework cho Unity 6 + OpenXR, target Meta Quest (primary) và PCVR (secondary).

---

## Features

**Input System**
- Input abstraction: swap Legacy Input / New Input System / Hand Tracking tại runtime
- 5-finger curl tracking từ controller analog hoặc hand tracking
- Desktop simulator cho dev không cần headset

**Physics Hands**
- Velocity-follow hands (không kinematic) — tương tác vật lý tự nhiên
- Dynamic mass adjustment theo khoảng cách/góc lệch
- Throw tracking với smoothed velocity

**Grab & Interaction**
- One-hand, two-hand, snap grip, distance grab
- Physics gadgets: Button, Lever, Dial, Slider, Hinge, Steering Wheel
- Magnetic grab, pull-apart, sticky objects
- PlacePoint snap system cho inventory

**Locomotion**
- Priority-based state machine: Joystick, Teleport, Climb, Gorilla, Swim, Grapple, Push
- Smooth/Snap turn
- Physics-based player body với ground check, step height, slope limit

**Combat**
- Firearm (raycast + recoil + magazine reload)
- Melee (velocity-based damage scaling)
- Throwable (bounce/stick/shatter/explode)
- HitZone damage multiplier system
- Ragdoll Animator 2 integration (fallback khi không có RA2)

**Body IK**
- Head + hands → estimate hip, spine, shoulders
- Procedural foot IK stepping
- Inventory body slots follow estimated skeleton

**Editor Tools**
- 1-click Setup Wizard (8 bước)
- Hand Pose Baker + Pose Wizard
- Scene Validator
- Grabbable Quick Setup (right-click context menu)
- Auto layer + collision matrix configuration
- Default config/pose/item asset generator

---

## Requirements

| Dependency | Version | Required |
|---|---|---|
| Unity | 6000.0+ (Unity 6) | Yes |
| XR Plugin Management | 4.x+ | Yes |
| OpenXR Plugin | 1.x+ | Yes |
| XR Interaction Toolkit | 3.x+ | Yes |
| Input System | 1.x+ | Yes |
| XR Hands | 1.x+ | No |
| Ragdoll Animator 2 | Any | No |

---

## Quick Start

1. Copy folder `BillVRCore/` vào `Assets/Script/` trong Unity project
2. Đợi compile xong
3. Menu **BillVR > Setup Wizard** → click **Run All Steps**
4. Cắm Quest qua USB3, bật Quest Link, nhấn Play

Setup Wizard tự động: validate packages, fix project settings, tạo 7 physics layers + collision matrix, detect RA2, build player rig, tạo default configs, apply VR performance settings, validate scene.

---

## Project Structure

```
BillVRCore/
├── Editor/                    # Editor-only tools
│   ├── BillVRSetupWizard      # 8-step auto setup
│   ├── BillVRSceneBuilder     # Player rig builder
│   ├── BillVRSceneValidator   # Scene health check
│   ├── BillVRPackageValidator # Package + RA2 detection
│   ├── BillVRLayerSetup       # Layer + collision matrix
│   ├── BillVRAssetCreator     # Default config generator
│   ├── HandPoseBaker          # Bake finger poses to asset
│   ├── HandPoseWizard         # Visual finger pose editor
│   ├── HandBoneDetector       # Auto-detect finger bones
│   ├── GrabbableQuickSetup    # Context menu shortcuts
│   └── InputManagerEditor     # Input provider inspector
├── Runtime/
│   ├── Core/                  # Bootstrap, API, Enums, Extensions
│   ├── Input/                 # InputManager, IVRInput, Providers
│   ├── Hand/                  # VRHand, FingerRig, HandAnimator, Poses
│   ├── Interaction/           # Grabbable, GrabHandler, PlacePoint
│   │   └── Gadgets/           # Physics Button/Lever/Dial/Slider/Hinge
│   ├── Locomotion/            # StateMachine, 10+ providers
│   ├── Weapons/               # Firearm, Melee, Throwable, DamageSystem
│   ├── Inventory/             # BodySlot, InventoryManager, QuickSwap
│   ├── Tracking/              # Head + Controller drivers
│   ├── BodyIK/                # Body estimator, Foot IK
│   ├── Ragdoll/               # RagdollBridge, PerformanceManager
│   ├── Feedback/              # CollisionSound
│   ├── UI/                    # UIPointer
│   └── Debug/                 # BillVRDebugOverlay
└── README.md
```

---

## API

Static facade — one-liner cho mọi thao tác thường dùng:

```csharp
// Input
BillVRAPI.GrabPressed(HandSide.Right);
BillVRAPI.Trigger(HandSide.Left);        // float 0-1
BillVRAPI.Joystick(HandSide.Left);       // Vector2
BillVRAPI.Finger(side, FingerType.Index); // float 0-1

// Grab
BillVRAPI.Grab(hand, grabbable);
BillVRAPI.Release(hand);
BillVRAPI.GetHeldObject(HandSide.Right);

// Movement
BillVRAPI.TeleportPlayer(position);
BillVRAPI.MovePlayer(direction, speed);
BillVRAPI.IsPlayerGrounded();

// Setup
BillVRAPI.MakeGrabbable(gameObject);
BillVRAPI.Haptic(HandSide.Right, 0.5f, 0.05f);

// Combat
BillVRAPI.DealDamage(target, 50f, direction);
```

---

## Documentation

- **[SETUP_GUIDE.md](SETUP_GUIDE.md)** — Hướng dẫn setup chi tiết (tiếng Việt)
- **Menu BillVR > Validate Scene** — Kiểm tra scene setup
- **Menu BillVR > Setup Wizard** — Auto setup 8 bước

---

## Namespace

Toàn bộ code nằm trong `BillVRCore` namespace:

```
BillVRCore              — Core (Bootstrap, API, Enums)
BillVRCore.Input        — Input management
BillVRCore.Hand         — Hand + finger systems
BillVRCore.Interaction  — Grab + gadgets
BillVRCore.Locomotion   — Movement providers
BillVRCore.Weapons      — Combat systems
BillVRCore.Inventory    — Body slot inventory
BillVRCore.Tracking     — XR tracking drivers
BillVRCore.BodyIK       — Body estimation + foot IK
BillVRCore.Ragdoll      — Ragdoll integration
BillVRCore.Editor       — Editor-only tools
BillVRCore.DebugTools   — Debug overlay
```

---

## Performance

- Zero GC allocation trong Update/FixedUpdate/LateUpdate
- Zero LINQ, zero reflection trong runtime
- OverlapSphereNonAlloc với pre-allocated buffer
- Ragdoll LOD auto-scale solver iterations theo camera distance
- Physics timestep 90Hz cho VR stability
