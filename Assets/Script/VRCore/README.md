# VRCore — Documentation

---

## 1. VRCore là gì

VRCore là framework VR interaction cho Unity 6, thiết kế cho Meta Quest (primary) và PCVR (secondary). Framework cung cấp hệ thống hoàn chỉnh từ input → hand → grab → locomotion → combat → inventory, tất cả chạy trên OpenXR.

Điểm khác biệt so với Auto Hand hay XR Interaction Toolkit:

- Input abstraction cho phép swap giữa Legacy Input / New Input System / Hand Tracking tại runtime mà không đổi game code
- Physics-based hand (velocity follow, không kinematic) cho tương tác tự nhiên
- Tích hợp Ragdoll Animator 2 cho combat physics — graceful fallback khi không có RA2
- Zero GC allocation trong tất cả hot paths
- 1-click setup wizard
- Open API với 34+ static convenience methods qua VRCoreAPI

---

## 2. Setup Guide

### 2.1 Cài đặt nhanh (dưới 5 phút)

1. Copy folder VRCore/ vào Assets/ trong Unity project
2. Đợi compile
3. Menu → VRCore → Setup Wizard → "Run All Steps"
4. Cắm Quest USB3, bật Quest Link, Play

Wizard tự động: tạo layers + collision matrix, detect packages + RA2, build player rig với tracking drivers, tạo highlight material, tạo default configs, spawn test cubes + floor.

### 2.2 Cài đặt thủ công

Bước 1 — Packages (Package Manager, Unity Registry):
- XR Plugin Management → tick OpenXR trong settings
- XR Interaction Toolkit (import Starter Assets)
- Input System → Project Settings → Player → Active Input Handling = "Both"

Bước 2 — Layers: Menu → VRCore → Setup Layers. Tạo 7 layers:

| Layer | Index | Mục đích |
|---|---|---|
| PlayerBody | 8 | Capsule collider player |
| RagdollDummy | 9 | Ragdoll physics skeleton |
| Grabbable | 10 | Vật grab được |
| HandPhysics | 11 | Hand rigidbody |
| InventorySlot | 12 | Inventory triggers |
| GroundCheck | 13 | Ground detection |
| BodyIK | 14 | IK raycasts |

Bước 3 — Player Rig: Menu → VRCore → Create Player Rig. Tạo hierarchy:

```
[VRCore] Player
├── VRCoreBootstrap + DebugOverlay
└── TrackingSpace
    └── CameraOffset (y=1.6)
        ├── Main Camera + TrackedHeadDriver
        ├── LeftController + TrackedControllerDriver
        ├── RightController + TrackedControllerDriver
        ├── LeftHand (VRHand + GrabHandler + Highlighter + Haptics)
        └── RightHand (tương tự)
```

Bước 4 — Verify: Menu → VRCore → Validate Scene

---

## 3. Luồng hoạt động

### 3.1 Khởi tạo (khi Play)

```
VRCoreBootstrap (-100) → tạo InputManager, validate hands
InputManager (-50)      → tạo input provider, bắt đầu đọc input
TrackedDrivers (-30)    → drive controller/camera transforms từ XR
VRHand (-10)            → physics follow, grab scan
Gameplay (0)            → game code
LocomotionSM (50)       → evaluate movement state
VRPlayerBody (75)       → ground check, capsule sync
BodyEstimator (100)     → estimate body từ head+hands
FootIKSolver (110)      → procedural foot stepping
HandAnimator (200)      → final finger poses
```

### 3.2 Grab Flow

```
FixedUpdate → GrabHandler scan (OverlapSphereNonAlloc) → tìm closest Grabbable
  ↓
Grip pressed → ExecuteGrab → tạo ConfigurableJoint (spring/damper drives)
  ↓
Đang giữ: Joint giữ kết nối. Trigger → OnSqueeze. Object follow hand qua physics.
  ↓
Grip released → Destroy joint → apply smoothed throw velocity → ignore colliders 0.25s
```

### 3.3 Input Flow

```
Hardware (Controller / Hands / Keyboard)
  → IVRInput Provider (QuestLegacy / HandTracking / Desktop)
  → InputManager.Update() → UpdateState() lưu prev/cur state
  → Game code đọc: InputManager.Instance.Input.GrabPressed(side)
  → Hoặc: VRCoreAPI.GrabPressed(HandSide.Right)
```

### 3.4 Locomotion Flow

```
LocomotionStateMachine evaluate mỗi FixedUpdate:
  Priority 50: ClimbProvider  → đang grab Climbable?
  Priority 30: TeleportProvider → đang aim?
  Priority 20: GorillaMoveProvider → tay đẩy ground?
  Priority 10: JoystickMoveProvider → joystick có input?
  → Provider active cao nhất drive movement
```

### 3.5 Combat Flow

```
Weapon fire/swing → Raycast/Collision → tìm HitZone → nhân damage multiplier
  → tìm IDamageable → TakeDamage(DamageEvent)
  → RagdollBridge: trừ health, apply bone impact
    → Có RA2: User_AddBoneImpact, User_SwitchFallState
    → Không RA2: disable Animator, enable gravity
```

### 3.6 Inventory Flow

```
BodySlot follow BodyEstimator (hip/back positions)
  → Đưa vật gần slot → PlacePoint detect → thả grip → snap vào
  → Grab lại → PlacePoint remove → vật tự do
  → Quick swap: A/X hoặc B/Y → grab/store theo slot type
```

---

## 4. Ngữ cảnh sử dụng

### 4.1 Làm vật grab được

Nhanh nhất: right click object → VRCore → Make Grabbable

Từ code: `VRCoreAPI.MakeGrabbable(gameObject);`

Tuỳ chỉnh:
```csharp
grabbable.SetSingleHandOnly(true);
grabbable.SetHandRestriction(HandRestriction.RightOnly);
grabbable.SetJointBreakForce(500f);
grabbable.OnGrabEvent += (hand, grab) => { };
```

### 4.2 Súng 2 tay

Thêm TwoHandGrabbable. Tạo 2 child transforms: PrimaryGrip và SecondaryGrip. Tay đầu = primary, tay hai = secondary. Object tự rotate nhìn từ primary → secondary.

### 4.3 Snap grip (pistol, tool)

Thêm SnapGrabbable. Set finger curl per finger (thumbCurl, indexCurl...). Tay snap vào grip point khi grab.

### 4.4 Inventory

Thêm BodyEstimator + InventoryManager + BodySlot components. BodySlot gắn vào estimated hip/back. Đưa vật gần → thả → snap. Quick swap qua QuickSwapHandler.

### 4.5 Firearm

FirearmWeapon component. Set muzzle point, damage, fire rate, ammo. Squeeze trigger → raycast → damage. Magazine socket (PlacePoint trên súng) cho reload.

### 4.6 Melee

MeleeWeapon component. Set blade collider (trigger zone). Damage = baseDamage × (swingSpeed / scale). Tự tính velocity.

### 4.7 Throwable

ThrowableWeapon component. Grab → ném → on impact: Bounce/Stick/Shatter/Explode.

### 4.8 Enemy ragdoll

RagdollBridge component trên enemy. Có RA2 → bone impacts, fall/getup. Không có RA2 → fallback ragdoll.

### 4.9 Physics Gadgets

PhysicsButton: nút nhấn spring. PhysicsLever: cần gạt. PhysicsDial: núm xoay (stepped optional). PhysicsSlider: trượt linear. PhysicsHinge: bản lề. Tất cả output 0-1 normalized value qua OnValueEvent.

### 4.10 Teleport

Default: joystick forward aim, release teleport. Hoặc code:
```csharp
VRCoreAPI.TeleportPlayer(position);
VRCoreAPI.TeleportPlayer(waypointTransform);
tp.TeleportTo(position, yRotation);
```

### 4.11 Gorilla Tag locomotion

Thêm GorillaMoveProvider. Tay gần ground → đẩy → player di chuyển. Auto disable khi hold object.

### 4.12 Climbing

Thêm Climbable lên surface + ClimbProvider lên player. Grab surface → body follow hand delta. Gravity off khi climbing.

### 4.13 Wrist menu

WristLookEvent component. Nhìn cổ tay > 0.3s → trigger event. Dùng cho radial menu, watch UI.

### 4.14 Distance grab

DistanceGrabber trên VRHand. Ray → highlight target → trigger → pull to hand → auto grab.

---

## 5. VRCoreAPI Reference

Static facade — tất cả one-liner operations:

```csharp
// Input
VRCoreAPI.GrabPressed(HandSide.Right)
VRCoreAPI.Grip(HandSide.Right)              // float 0-1
VRCoreAPI.Trigger(HandSide.Left)             // float 0-1
VRCoreAPI.Joystick(HandSide.Left)            // Vector2
VRCoreAPI.Finger(side, FingerType.Index)     // float 0-1

// Grab
VRCoreAPI.Grab(hand, grabbable)
VRCoreAPI.Release(hand)
VRCoreAPI.IsHolding(HandSide.Right)
VRCoreAPI.GetHeldObject(HandSide.Right)

// Haptic
VRCoreAPI.Haptic(HandSide.Right, 0.5f, 0.05f)

// Movement
VRCoreAPI.TeleportPlayer(position)
VRCoreAPI.TeleportPlayer(position, yRotation)
VRCoreAPI.TeleportPlayer(transform)
VRCoreAPI.MovePlayer(direction, speed)
VRCoreAPI.RotatePlayer(angle)
VRCoreAPI.IsPlayerGrounded()

// Setup
VRCoreAPI.MakeGrabbable(gameObject)
VRCoreAPI.CreatePlacePoint(position, slotType)
VRCoreAPI.SwitchInputMode(InputMode.Desktop)

// Combat
VRCoreAPI.DealDamage(target, 50f, direction)
VRCoreAPI.DealDamage(raycastHit, 25f, 500f, DamageType.Ranged)

// Query
VRCoreAPI.GetHand(HandSide.Left)
VRCoreAPI.GetHeadPose()
VRCoreAPI.GetHandPose(HandSide.Right)
```

---

## 6. Extending VRCore

### Custom Input Provider

```csharp
public class MyNetworkInput : IVRInput { /* implement all */ }
InputManager.Instance.SetCustomProvider(new MyNetworkInput());
```

### Custom Locomotion Provider

```csharp
public class FlyProvider : MonoBehaviour, ILocomotionProvider
{
    public bool IsActive => flyEnabled;
    public LocomotionState ProvidedState => LocomotionState.Idle;
    public int Priority => 15;
    public void SetLocomotionActive(bool active) { }
}
```

### Custom Grabbable

```csharp
public class MagneticGrabbable : Grabbable
{
    public override bool CanBeGrabbedBy(VRHand hand) { /* custom logic */ }
    public override void OnGrab(VRHand hand) { base.OnGrab(hand); /* extra */ }
}
```

---

## 7. Performance

- Zero GC trong Update/FixedUpdate/LateUpdate
- Zero LINQ trong runtime
- Zero reflection trong runtime
- OverlapSphereNonAlloc (pre-allocated buffer)
- Debug overlay cached string rebuild mỗi 0.15s
- Ragdoll LOD auto-scale solver iterations theo distance
- Max 8-10 active ragdolls trên Quest

---

## 8. Troubleshooting

| Vấn đề | Giải pháp |
|---|---|
| Tay không di chuyển | Check TrackedControllerDriver trên controller target, VRHand.followTarget assigned |
| Không grab được | Check Rigidbody + Grabbable + Collider + layer "Grabbable" |
| Grab bị jitter | Tăng GrabHandler spring/damper, giảm mass ratio |
| Quest Link fail | Check OpenXR enabled + Interaction Profile + USB data cable |
| Input không đọc | Active Input Handling = "Both", InputManager trong scene |
| Ragdoll không phản ứng | Run Detect RA2, check VRCORE_HAS_RAGDOLL define |
| Performance thấp | Thêm RagdollPerformanceManager, tắt debug overlay, check timestep |
| Foot IK không chạy | Cần BodyEstimator + locomotion state = JoystickMoving |

Menu → VRCore → Validate Scene để check toàn bộ setup.

---

## 9. File Stats

73 C# files, 8705 lines, 115KB zip. Zero comments — clean code throughout.
