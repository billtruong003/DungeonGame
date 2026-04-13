# BillVRCore — Hướng Dẫn Setup Chi Tiết

---

## Mục Lục

1. [Yêu Cầu Hệ Thống](#1-yêu-cầu-hệ-thống)
2. [Tạo Unity Project](#2-tạo-unity-project)
3. [Cài Đặt Packages](#3-cài-đặt-packages)
4. [Cấu Hình Project Settings](#4-cấu-hình-project-settings)
5. [Thêm BillVRCore Vào Project](#5-thêm-billvrcore-vào-project)
6. [Chạy Setup Wizard (Tự Động)](#6-chạy-setup-wizard-tự-động)
7. [Setup Thủ Công (Từng Bước)](#7-setup-thủ-công-từng-bước)
8. [Kiểm Tra Setup](#8-kiểm-tra-setup)
9. [Tạo Object Tương Tác Đầu Tiên](#9-tạo-object-tương-tác-đầu-tiên)
10. [Cấu Hình Nâng Cao](#10-cấu-hình-nâng-cao)
11. [Xử Lý Lỗi Thường Gặp](#11-xử-lý-lỗi-thường-gặp)

---

## 1. Yêu Cầu Hệ Thống

### Phần Mềm

| Phần Mềm | Phiên Bản | Ghi Chú |
|---|---|---|
| Unity | 6000.0+ (Unity 6) | Bắt buộc |
| Visual Studio / Rider | Bất kỳ | Để viết code |
| Meta Quest Developer Hub | Mới nhất | Để deploy lên Quest |
| SideQuest (tuỳ chọn) | Mới nhất | Để test APK |

### Phần Cứng

| Thiết Bị | Ghi Chú |
|---|---|
| Meta Quest 2/3/Pro | Target chính |
| Cáp USB 3.0 | Cho Quest Link (cáp data, không phải cáp sạc) |
| PC với GPU rời | Cho PCVR dev qua Quest Link |

### Packages Unity Bắt Buộc

| Package | ID | Lý Do |
|---|---|---|
| XR Plugin Management | `com.unity.xr.management` | Quản lý XR runtime |
| OpenXR Plugin | `com.unity.xr.openxr` | OpenXR backend |
| XR Interaction Toolkit | `com.unity.xr.interaction.toolkit` | XR base types |
| Input System | `com.unity.inputsystem` | New Input System support |

### Packages Tuỳ Chọn

| Package | ID | Lý Do |
|---|---|---|
| XR Hands | `com.unity.xr.hands` | Hand tracking (không dùng controller) |
| Ragdoll Animator 2 | Asset Store | Combat physics nâng cao |

---

## 2. Tạo Unity Project

### Bước 2.1 — Mở Unity Hub

1. Mở **Unity Hub**
2. Click **New Project**
3. Chọn template: **3D (URP)** hoặc **3D Core**
   - Khuyên dùng **URP** vì BillVRCore tạo material URP mặc định
   - Nếu dùng **Built-in RP**, cần đổi shader trong hand visual và highlight material
4. Đặt tên project, chọn folder lưu
5. Click **Create project**

### Bước 2.2 — Đợi Unity Khởi Động

- Lần đầu tạo project sẽ mất vài phút để import packages mặc định
- Đợi cho đến khi Unity Editor hiển thị đầy đủ, không còn thanh loading

---

## 3. Cài Đặt Packages

### Bước 3.1 — Mở Package Manager

1. Menu **Window > Package Manager**
2. Ở góc trái trên, chọn **Unity Registry** (không phải "In Project")

### Bước 3.2 — Cài XR Plugin Management

1. Tìm kiếm **XR Plugin Management**
2. Click **Install**
3. Đợi install xong

### Bước 3.3 — Cài OpenXR Plugin

1. Tìm kiếm **OpenXR Plugin**
2. Click **Install**
3. Sau khi install, Unity có thể hiện popup yêu cầu restart — click **Yes**

### Bước 3.4 — Cấu Hình OpenXR

1. Menu **Edit > Project Settings**
2. Chọn **XR Plug-in Management** ở panel trái
3. Tab **PC** (icon màn hình):
   - Tick **OpenXR**
   - Click icon warning bên cạnh (nếu có) để fix
4. Tab **Android** (icon robot):
   - Tick **OpenXR**
   - Phần **Interaction Profiles**, click **+** và thêm:
     - **Oculus Touch Controller Profile**
     - **Meta Quest Touch Pro Controller Profile** (nếu dùng Quest Pro)

### Bước 3.5 — Cài XR Interaction Toolkit

1. Quay lại **Package Manager**
2. Tìm **XR Interaction Toolkit**
3. Click **Install**
4. Nếu hiện popup **Import Starter Assets** — click **Import**
   - Starter Assets không bắt buộc cho BillVRCore nhưng có sample hữu ích

### Bước 3.6 — Cài Input System

1. Tìm **Input System** trong Package Manager
2. Click **Install**
3. Unity sẽ hỏi: **"Do you want to enable the new Input System?"**
   - Chọn **Both** (quan trọng! không chọn "New" hoặc "Old" riêng)
   - Unity sẽ restart

### Bước 3.7 — Cài XR Hands (Tuỳ Chọn)

Chỉ cần nếu muốn dùng **hand tracking** (không controller):

1. Tìm **XR Hands** trong Package Manager
2. Click **Install**

### Bước 3.8 — Xác Nhận

Sau khi cài xong, kiểm tra trong Package Manager tab **In Project**:
- `com.unity.xr.management` — có
- `com.unity.xr.openxr` — có
- `com.unity.xr.interaction.toolkit` — có
- `com.unity.inputsystem` — có

---

## 4. Cấu Hình Project Settings

### Bước 4.1 — Active Input Handling

1. Menu **Edit > Project Settings > Player**
2. Mở phần **Other Settings**
3. Tìm **Active Input Handling**
4. Chọn **Both**
5. Unity sẽ restart — đây là bình thường

**Tại sao "Both"?** BillVRCore hỗ trợ cả Legacy Input (cho Quest controller qua XR API) và New Input System (cho hand tracking và future input). Chọn "Both" đảm bảo cả hai đều hoạt động.

### Bước 4.2 — Color Space

1. Vẫn ở **Project Settings > Player > Other Settings**
2. Tìm **Color Space**
3. Chọn **Linear** (không dùng Gamma)

**Tại sao Linear?** VR render cần color space chính xác. Gamma sẽ làm sai màu và ảnh hưởng tới chiếu sáng.

### Bước 4.3 — Quality Settings (Khuyên Nghị)

1. Menu **Edit > Project Settings > Quality**
2. Chọn quality level đang dùng
3. **VSync Count** = Don't Sync (0)
   - VR đã có vsync riêng qua compositor, không cần Unity vsync
4. **Anti Aliasing** = 4x Multi Sampling (khuyên nghị cho VR)

### Bước 4.4 — Physics Settings (Khuyên Nghị)

1. Menu **Edit > Project Settings > Time**
2. **Fixed Timestep** = 0.01111 (tương đương 90Hz)
   - Mặc định Unity là 0.02 (50Hz), quá chậm cho VR
   - 90Hz match với Quest refresh rate, đảm bảo physics mượt

---

## 5. Thêm BillVRCore Vào Project

### Cách 1 — Copy Folder

1. Mở folder chứa `BillVRCore/` trong File Explorer
2. Copy toàn bộ folder `BillVRCore/` vào `Assets/Script/` trong Unity project
3. Quay lại Unity, đợi compile
   - Thanh progress bar ở cuối Unity Editor sẽ chạy
   - Có thể mất 30s - 2 phút tuỳ máy

### Cách 2 — Git Submodule (Cho Team)

```bash
cd Assets/Script
git submodule add <repo-url> BillVRCore
```

### Sau Khi Import

- Kiểm tra **Console** (Window > Console) — nên có 0 errors
- Nếu có error về missing reference: các package ở Bước 3 chưa được cài đủ
- Nếu có warning: bình thường, sẽ fix qua Setup Wizard

---

## 6. Chạy Setup Wizard (Tự Động)

Đây là cách nhanh nhất để setup. Wizard sẽ tự động thực hiện tất cả các bước.

### Bước 6.1 — Mở Wizard

1. Menu **BillVR > Setup Wizard**
2. Cửa sổ **BillVR Setup Wizard** sẽ hiển thị

### Bước 6.2 — Kiểm Tra Trạng Thái

Wizard hiển thị 8 steps với trạng thái (check xanh/X đỏ):

| Step | Nội Dung | Tự Động |
|---|---|---|
| 1 | Package Validation | Kiểm tra packages đã cài |
| 2 | Project Settings | Fix Color Space, kiểm tra Input |
| 3 | Physics Layers | Tạo 7 layers + collision matrix |
| 4 | Ragdoll Animator 2 | Detect RA2, apply integration |
| 5 | Player Rig | Build VR player trong scene |
| 6 | Default Assets | Tạo configs, poses, items |
| 7 | Performance | Apply VR performance settings |
| 8 | Validation | Kiểm tra toàn bộ scene |

### Bước 6.3 — Chạy Toàn Bộ

1. Click nút **Run All Steps** (nút xanh lá ở cuối)
2. Đợi vài giây
3. Console sẽ in: `[BillVR] All setup steps completed.`

**Lưu ý:** Nếu bạn đã có player rig trong scene, "Run All" sẽ xoá rig cũ và tạo lại. Dùng **Run All (Skip Scene Build)** nếu chỉ muốn fix settings mà không đổi scene.

### Bước 6.4 — Kiểm Tra Kết Quả

Sau khi chạy, kiểm tra:

1. **Hierarchy**: Có object `[BillVR] Player` với đầy đủ children:
   ```
   [BillVR] Player
   ├── TrackingSpace
   │   └── CameraOffset
   │       ├── Main Camera
   │       ├── LeftController
   │       ├── RightController
   │       ├── LeftHand
   │       └── RightHand
   ├── Locomotion
   │   ├── LocomotionStateMachine
   │   ├── JoystickMoveProvider
   │   ├── TeleportProvider
   │   ├── SnapTurnProvider
   │   └── ClimbProvider
   └── BillVRDebugOverlay
   ```

2. **Floor**: Có object `Floor` (plane trắng)

3. **Test Objects**: Có 5 cube màu (grabbable)

4. **Project**: Folder `Assets/VRCore/Data/` chứa:
   - `DefaultFingerMapping.asset`
   - `HighlightMaterial.mat`
   - `Poses/` — 8 hand pose assets
   - `Items/` — 5 item data assets

5. **Tags & Layers**: Layer 8-14 đã được tạo

---

## 7. Setup Thủ Công (Từng Bước)

Nếu bạn muốn kiểm soát từng bước thay vì dùng "Run All".

### Bước 7.1 — Tạo Physics Layers

1. Menu **BillVR > Setup Layers + Collision Matrix**
2. Hoặc trong Wizard, click **Create Layers + Configure Collision Matrix**

Wizard tạo 7 layers:

| Layer | Index | Mục Đích |
|---|---|---|
| PlayerBody | 8 | Capsule collider của player |
| RagdollDummy | 9 | Ragdoll physics skeleton |
| Grabbable | 10 | Vật có thể grab |
| HandPhysics | 11 | Hand rigidbody collider |
| InventorySlot | 12 | Inventory trigger zones |
| GroundCheck | 13 | Ground detection raycast |
| BodyIK | 14 | Body IK raycasts |

Và cấu hình collision matrix:
- PlayerBody **không** va chạm với RagdollDummy, HandPhysics
- RagdollDummy **không** va chạm với InventorySlot
- HandPhysics **có** va chạm với Grabbable, InventorySlot, RagdollDummy

### Bước 7.2 — Detect Ragdoll Animator 2

1. Menu **BillVR > Detect Ragdoll Animator 2**
2. Nếu bạn đã mua và import RA2 từ Asset Store:
   - Console sẽ in: `[BillVR] Ragdoll Animator 2 detected`
   - Tự động thêm define `VRCORE_HAS_RAGDOLL`
   - Tự động thêm assembly reference
3. Nếu không có RA2:
   - Console sẽ in: `[BillVR] Ragdoll Animator 2 not found`
   - Hoàn toàn OK — BillVRCore có fallback ragdoll

### Bước 7.3 — Tạo Player Rig

1. Menu **BillVR > Create Player Rig**
2. Wizard tạo `[BillVR] Player` với:
   - **BillVRBootstrap**: Khởi tạo InputManager khi Play
   - **VRPlayerBody**: Physics capsule, ground check, height tracking
   - **Main Camera** + **TrackedHeadDriver**: Head tracking
   - **LeftController/RightController** + **TrackedControllerDriver**: Controller tracking
   - **LeftHand/RightHand** + **VRHand**: Physics hands với:
     - Rigidbody (mass 1kg, no gravity, continuous collision)
     - BoxCollider (hand shape)
     - Palm transform (grab reference point)
     - GrabHandler (grab scan + joint creation)
     - HandHighlighter (highlight hover target)
     - HandHaptics (vibration feedback)
     - DistanceGrabber (ray-based distance grab)
   - **Locomotion**: JoystickMove, Teleport (với LineRenderer), SnapTurn, Climb
   - **BillVRDebugOverlay**: FPS + hand info overlay

### Bước 7.4 — Tạo Default Configs

1. Menu **BillVR > Create Default Configs**
2. Tạo trong `Assets/VRCore/Data/`:
   - `DefaultFingerMapping.asset` — finger input mapping config
   - 8 hand poses: OpenHand, ClosedFist, Pointing, ThumbsUp, PistolGrip, RifleGrip, SwordGrip, Pinch
   - 5 item datas: Weapon_Pistol, Weapon_Rifle, Weapon_Melee, Ammo_Magazine, Item_Generic

### Bước 7.5 — Apply Performance Settings

1. Menu **BillVR > Apply VR Performance Settings**
2. Tự động set:
   - Target frame rate: 120fps
   - VSync: Off
   - Fixed timestep: 0.01111s (90Hz physics)

### Bước 7.6 — Validate Scene

1. Menu **BillVR > Validate Scene**
2. Cửa sổ hiển thị checklist:
   - BillVRBootstrap — có trong scene?
   - InputManager — có hoặc sẽ tự động tạo?
   - Left Hand / Right Hand — có VRHand, FollowTarget, PalmTransform, GrabHandler?
   - TrackedHeadDriver — có trên camera?
   - TrackedControllerDrivers — có ít nhất 2?
   - VRPlayerBody — có?
   - LocomotionStateMachine — có?
   - Physics Layers — 7 layers đúng?
   - Color Space — Linear?

Nếu có issue, click **Open Setup Wizard** để fix.

---

## 8. Kiểm Tra Setup

### Bước 8.1 — Test Với Quest Link

1. Cắm Quest vào PC bằng cáp USB 3.0
2. Mở Quest, bật **Quest Link** (hoặc **Air Link** qua wifi 5GHz)
3. Trong Unity, nhấn **Play**
4. Bạn sẽ thấy:
   - Camera theo đầu bạn
   - 2 cube xanh/đỏ (hands) theo controller
   - 5 cube màu trên bàn có thể grab được
   - Di chuyển bằng joystick trái
   - Xoay bằng joystick phải (snap turn)

### Bước 8.2 — Test Grab

1. Đưa tay gần cube
2. Bóp **Grip** (nút bên cạnh controller)
3. Cube sẽ dính vào tay
4. Thả **Grip** — cube rơi xuống
5. Thả nhanh + vung tay = **ném** (throw velocity tracking)

### Bước 8.3 — Test Desktop (Không Cần Headset)

Nếu không có headset:

1. Thêm dòng này vào BillVRBootstrap hoặc code khởi động:
   ```csharp
   BillVRAPI.SwitchInputMode(InputMode.Desktop);
   ```
2. Hoặc trong Inspector của BillVRBootstrap, đổi **Default Input Mode** = Desktop
3. Play — dùng mouse để nhìn, WASD để đi, click để grab

### Bước 8.4 — Kiểm Tra Debug Overlay

- Góc trên trái sẽ hiển thị:
  - FPS
  - Input mode (Controller/HandTracking/Desktop)
  - Hand state (Empty/Hovering/Grabbing)
  - Locomotion state

---

## 9. Tạo Object Tương Tác Đầu Tiên

### Cách 1 — Context Menu (Nhanh Nhất)

1. Tạo bất kỳ 3D object (Cube, Sphere, ...)
2. Right-click object trong Hierarchy
3. Chọn **BillVR > Make Grabbable**
4. Tự động thêm: Rigidbody + Grabbable + set layer

### Cách 2 — Từ Code

```csharp
using BillVRCore;

// Biến bất kỳ object thành grabbable
BillVRAPI.MakeGrabbable(gameObject);

// Hoặc với tuỳ chỉnh
BillVRAPI.MakeGrabbable(gameObject, mass: 2f, parentOnGrab: false);
```

### Cách 3 — Thủ Công

1. Chọn object trong scene
2. **Add Component > Rigidbody**
   - Mass: 0.5 (nhẹ) đến 5 (nặng)
   - Use Gravity: On
3. **Add Component > Grabbable** (namespace `BillVRCore.Interaction`)
4. Trong Inspector, set:
   - Grab Type: Default
   - Hand Restriction: Both
   - Parent On Grab: On (vật theo tay)
5. Đổi layer của object thành **Grabbable** (layer 10)
   - Inspector > Layer dropdown > Grabbable

### Tạo Vật 2 Tay

1. Làm như trên nhưng thêm **TwoHandGrabbable** thay vì **Grabbable**
2. Tạo 2 empty child: `PrimaryGrip` và `SecondaryGrip`
3. Đặt vị trí grip points trên object
4. Trong Inspector của TwoHandGrabbable:
   - Kéo PrimaryGrip vào **Primary Grip Point**
   - Kéo SecondaryGrip vào **Secondary Grip Point**

### Tạo Snap Grip (Súng, Kiếm)

1. Thêm **SnapGrabbable** thay vì Grabbable
2. Set finger curl values cho từng ngón:
   - thumbCurl, indexCurl, middleCurl, ringCurl, pinkyCurl (0-1)
3. Hoặc dùng **BillVR > Hand Pose Wizard** để visual edit pose

---

## 10. Cấu Hình Nâng Cao

### 10.1 — Thêm Locomotion Provider

BillVRCore hỗ trợ nhiều kiểu di chuyển. Mặc định Setup Wizard tạo: Joystick, Teleport, SnapTurn, Climb.

Để thêm:

1. Chọn object **Locomotion** trong Hierarchy
2. **Add Component** và chọn provider:
   - **GorillaMoveProvider** — di chuyển kiểu Gorilla Tag (đẩy tay xuống đất)
   - **SwimProvider** — bơi
   - **GrappleProvider** — móc dây bắn
   - **PushProvider** — đẩy tường để di chuyển
   - **SmoothTurnProvider** — xoay mượt (thay snap turn)
   - **ParkourProvider** — nhảy parkour
3. Mỗi provider có **Priority** — provider nào active với priority cao nhất sẽ chiếm quyền di chuyển

### 10.2 — Cấu Hình Hand Physics

Chọn **LeftHand** hoặc **RightHand** trong Hierarchy, component **VRHand**:

| Property | Mặc Định | Giải Thích |
|---|---|---|
| Position Strength | 60 | Lực kéo tay về target (tăng = cứng hơn) |
| Max Velocity | 20 | Tốc độ tối đa của tay |
| Rotation Strength | 100 | Lực xoay tay về target |
| Base Drag | 20 | Lực cản của tay |
| Throw Power Multiplier | 1.25 | Hệ số lực ném |

### 10.3 — Cấu Hình Player Body

Component **VRPlayerBody** trên `[BillVR] Player`:

| Property | Mặc Định | Giải Thích |
|---|---|---|
| Auto Adjust Height | On | Capsule tự động theo chiều cao đầu |
| Max Step Height | 0.3m | Bước qua vật cản cao nhất |
| Max Slope Angle | 45 độ | Độ dốc tối đa có thể đi |
| Grounded Drag | 8 | Lực cản khi đứng trên mặt đất |
| Airborne Drag | 0.5 | Lực cản khi trên không |

### 10.4 — Custom Input Provider

Tạo input provider riêng (ví dụ: multiplayer, custom hardware):

```csharp
using BillVRCore.Input;

public class MyCustomInput : IVRInput
{
    public InputSourceType ActiveSource => InputSourceType.Controller;
    public void UpdateState() { /* đọc input */ }
    public bool GrabPressed(HandSide side) { /* ... */ }
    // ... implement toàn bộ interface
}

// Đăng ký:
InputManager.Instance.SetCustomProvider(new MyCustomInput(), InputMode.LegacyController);
```

### 10.5 — Inventory System

1. Thêm **BodyEstimator** component (để estimate hip/back positions)
2. Thêm **InventoryManager** component
3. Tạo **BodySlot** components tại các vị trí trên cơ thể:
   - HipLeft, HipRight — bên hông
   - Back — lưng
   - Belt — thắt lưng
   - Chest — ngực
4. Mỗi BodySlot cần có **PlacePoint** component
5. Đưa vật gần slot > thả grip > vật snap vào
6. Grab lại từ slot để lấy ra

### 10.6 — Weapon Setup

**Firearm (Súng bắn):**
1. Tạo object súng với **FirearmWeapon** component
2. Set: muzzle point, range, damage, fire rate, ammo
3. Thêm **PlacePoint** trên súng cho magazine socket
4. Squeeze trigger = bắn, raycast damage

**Melee (Vũ khí cận chiến):**
1. Tạo object với **MeleeWeapon** component
2. Set: blade collider (trigger zone), min swing speed
3. Damage = baseDamage x (swingSpeed / damageVelocityScale)

**Throwable (Ném):**
1. Tạo object với **ThrowableWeapon** component
2. Set: impact behavior (Bounce/Stick/Shatter/Explode)
3. Grab > ném > on impact trigger behavior

---

## 11. Xử Lý Lỗi Thường Gặp

### Lỗi: Tay Không Di Chuyển

| Nguyên Nhân | Cách Fix |
|---|---|
| Thiếu TrackedControllerDriver | Kiểm tra LeftController/RightController có component này |
| VRHand.followTarget chưa gán | Trong Inspector VRHand, kéo controller target vào Follow Target |
| OpenXR chưa bật | Project Settings > XR Plug-in Management > tick OpenXR |
| Interaction Profile chưa thêm | Project Settings > OpenXR > thêm Oculus Touch Controller Profile |

### Lỗi: Không Grab Được

| Nguyên Nhân | Cách Fix |
|---|---|
| Thiếu Rigidbody | Thêm Rigidbody component vào object |
| Thiếu Grabbable | Thêm Grabbable component |
| Thiếu Collider | Thêm Box/Sphere/Mesh Collider |
| Sai layer | Đổi layer object thành "Grabbable" (10) |
| GrabHandler thiếu | Kiểm tra VRHand có GrabHandler component |

### Lỗi: Grab Bị Jitter / Rung

| Nguyên Nhân | Cách Fix |
|---|---|
| Mass ratio quá lớn | Giảm mass của object (< 3kg cho grab thoải mái) |
| Position Strength quá thấp | Tăng VRHand position strength (mặc định 60) |
| Fixed Timestep quá thấp | Đổi Time.fixedDeltaTime = 0.01111 (90Hz) |

### Lỗi: Quest Link Không Chạy

| Nguyên Nhân | Cách Fix |
|---|---|
| Cáp không hỗ trợ data | Dùng cáp USB 3.0 data (không phải cáp sạc) |
| OpenXR chưa enable | Project Settings > XR Plug-in Management > tick OpenXR |
| Quest chưa bật Link | Trên Quest: Settings > System > Quest Link > Enable |
| Driver cũ | Cập nhật Oculus/Meta app trên PC |

### Lỗi: Input Không Đọc Được

| Nguyên Nhân | Cách Fix |
|---|---|
| Active Input Handling sai | Project Settings > Player > Active Input Handling = "Both" |
| InputManager chưa có | BillVRBootstrap tự động tạo, hoặc thêm thủ công |
| Provider sai | Kiểm tra InputManager.CurrentMode trong Inspector |

### Lỗi: Ragdoll Không Phản Ứng

| Nguyên Nhân | Cách Fix |
|---|---|
| RA2 chưa detect | Menu BillVR > Detect Ragdoll Animator 2 |
| Define chưa có | Kiểm tra VRCORE_HAS_RAGDOLL trong Player Settings > Scripting Defines |
| RagdollBridge thiếu | Thêm RagdollBridge component trên enemy |

### Lỗi: Performance Thấp

| Nguyên Nhân | Cách Fix |
|---|---|
| Physics rate thấp | Time.fixedDeltaTime = 0.01111 |
| Quá nhiều ragdoll | Thêm RagdollPerformanceManager (auto LOD) |
| Debug overlay | Tắt BillVRDebugOverlay khi build |
| VSync Unity | QualitySettings.vSyncCount = 0 |

### Lỗi: Foot IK Không Hoạt Động

| Nguyên Nhân | Cách Fix |
|---|---|
| Thiếu BodyEstimator | Thêm component BodyEstimator |
| Sai locomotion state | FootIK chỉ chạy khi LocomotionState = JoystickMoving |
| Layer GroundCheck thiếu | Chạy Setup Wizard để tạo layers |

---

## Kết Luận

Sau khi hoàn thành các bước trên, bạn đã có:

- Player rig VR hoàn chỉnh với physics hands
- Grab system hoạt động (1 tay, 2 tay, snap, distance)
- Locomotion đa dạng (joystick, teleport, climb, ...)
- Sẵn sàng thêm gameplay logic

**Menu tham khảo nhanh:**
- **BillVR > Setup Wizard** — Setup tự động
- **BillVR > Validate Scene** — Kiểm tra setup
- **BillVR > Hand Pose Wizard** — Chỉnh hand pose
- **BillVR > Hand Pose Baker** — Bake pose thành asset
- **BillVR > Create Default Configs** — Tạo asset mặc định

Chúc bạn dev VR vui vẻ!
