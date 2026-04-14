# RPGModular — Tổng Quan Hệ Thống & Game Design
### Tài Liệu Tham Khảo Sau Triển Khai | 82 file C# | Phase 0-8 Hoàn Tất

---

## 1. TỔNG QUAN GAME

### Thể loại
Action RPG thế giới mở, chiến đấu real-time lock-on, lấy cảm hứng từ Toram Online nhưng có bản sắc riêng với hệ thống võ học Hán-Việt, Chi gauge (Khí), và Tamer (bắt quái thú).

### Tầm nhìn
- **Giai đoạn 1**: Game offline single-player hoàn chỉnh
- **Giai đoạn 2**: Mở rộng thành MMORPG qua SpacetimeDB (nhiều người chơi, giao dịch, PvP)

### Khác biệt so với Toram
| Yếu tố | Toram Online | Game này |
|---------|-------------|----------|
| Tên skill | Tiếng Anh/Nhật | Hán-Việt wuxia (Kiếm Pháp, Đạo Thuật...) |
| Resource | HP + MP | HP + MP + **Chi** (Khí) + **Focus** (Tập Trung) |
| Chi gauge | Không có | Resource thứ 3, TAO skills dùng Chi thay MP |
| Focus gauge | Không có | Riêng cho Katana, đứng yên tích lực → damage bonus |
| Block/Parry | Nhánh Shield riêng | Skill mặc định mọi player có, nâng cấp qua Thủ Thuật tree |
| Pet system | Không có | Bắt quái, nuôi, chiến đấu cùng, Fuse (hợp nhất) lấy stat |
| Enemy AI | Đơn giản | PackManager: chỉ 2-3 con đuổi, bầy phản ứng theo level gap |

---

## 2. KIẾN TRUC HE THONG

### Sơ đồ tầng

```
┌──────────────────────────────────────────────────────────┐
│  GAME.*  (Lớp truy cập tĩnh)                            │
│  Game.Stats  Game.Health  Game.Inv  Game.Equip           │
│  Game.Level  Game.Skill   Game.Combo  Game.Status        │
│  Game.Combat Game.LockOn  Game.Weapon Game.Focus         │
│  Loc.Get("key")  — đa ngôn ngữ                          │
├──────────────────────────────────────────────────────────┤
│  PLAYERCORE  (Hub — tự tìm 18 hệ thống con)             │
├───────────┬───────────┬──────────┬───────────────────────┤
│ CHIẾN ĐẤU │ PHÁT TRIỂN│ THẾ GIỚI │ KỸ NĂNG SỐNG         │
│───────────│───────────│──────────│───────────────────────│
│CombatSM   │Inventory  │QuestTrack│CraftingSystem         │
│DmgHandler │Equipment  │Dialogue  │WeaponEnhancement      │
│DmgPipeline│LevelSystem│ShopServ  │TamerSystem            │
│HitboxMgr  │SkillBook  │ZoneSystem│                       │
│LockOnSys  │SkillBar   │Portal    │                       │
│AutoAttack │SkillCaster│DeathSys  │                       │
│FocusGauge │ComboTrack │SaveLoad  │                       │
│CombatLoco │StatusFX   │          │                       │
│AnimCtrl   │LootSystem │          │                       │
│WeaponHdlr │           │          │                       │
│WeaponVis  │           │          │                       │
├───────────┴───────────┴──────────┴───────────────────────┤
│  ENEMY AI                                                │
│  EnemyBase → EnemyAI (9 trạng thái) → PackManager (mối  │
│  đe dọa) → VAT_MobSpawner (spawn theo bầy qua Bill.Pool)│
├──────────────────────────────────────────────────────────┤
│  BILLGAMECORE v3  (Framework chung cho mọi game)         │
│  Bill.Pool  Bill.Audio  Bill.Save  Bill.UI  Bill.Scene   │
├──────────────────────────────────────────────────────────┤
│  BILLINSPECTOR  (95+ attribute tùy chỉnh Inspector)      │
└──────────────────────────────────────────────────────────┘
```

### Cách truy cập — Gọi 1 dòng, không cần biết component nằm đâu

```csharp
Game.Stats.GetStat(StatType.STR);       // lấy chỉ số STR
Game.Health.Heal(50f);                   // hồi 50 HP
Game.Inv.AddItem(swordData, 1);         // thêm kiếm vào túi đồ
Game.Equip.Equip(swordData, EquipSlot.MainHand);  // trang bị
Game.Level.AddExp(200);                  // cộng kinh nghiệm
Game.Skill.Cast(0);                      // thi triển skill slot 0
Game.Combo.CurrentComboCount;            // đếm combo hiện tại
Game.Focus.GetDamageBonus();             // bonus sát thương Focus
Loc.Get("skill.blade.hard_hit.name");    // → "Trảm Kích" (vi) / "Hard Hit" (en)
```

---

## 3. LUỒNG CHƠI GAME — Trải Nghiệm Người Chơi

### Vòng lặp gameplay chính

```
┌─── THỊ TRẤN ────────────────────────────────────────────┐
│                                                          │
│  NPC Thương Nhân → Mua trang bị, thuốc, vật bắt quái    │
│  Luyện Khí Sư → Rèn vũ khí/giáp, nâng cấp +1 đến +10  │
│  Điều Chế Sư → Pha thuốc, bom, dầu nguyên tố           │
│  Huấn Luyện Viên → Xem cây kỹ năng, phân bổ SP         │
│  Quản Thú → Quản lý thú cưng, cho ăn, hợp nhất         │
│  Bảng Nhiệm Vụ → Nhận quest                             │
│  Cổng Truyền → Di chuyển đến khu vực chiến đấu          │
│                                                          │
├─── KHU VỰC CHIẾN ĐẤU ──────────────────────────────────┤
│                                                          │
│  Tiếp cận bầy quái                                      │
│  ├─ PackManager đánh giá mức đe dọa dựa trên level gap  │
│  │  ├─ Player mạnh hơn nhiều → quái SỢ, chạy trốn      │
│  │  ├─ Mạnh hơn chút → quái DÈ CHỪNG, chỉ phản đòn    │
│  │  ├─ Ngang nhau → 2-3 con ĐUỔI, còn lại quan sát     │
│  │  ├─ Yếu hơn → 4-5 con đuổi, hung hăng hơn          │
│  │  └─ Yếu hơn nhiều → CẢ BẦY lao vào, vùng chết      │
│  │                                                       │
│  Chiến đấu                                              │
│  ├─ Auto-attack (đánh tự động) → HỒI MP mỗi đòn        │
│  ├─ Skill burst (kỹ năng) → TIÊU MP/Chi → sát thương    │
│  │  lớn                                                  │
│  │  ├─ Combo chain: skill liên tiếp → +20% tốc độ,     │
│  │  │  +10% sát thương mỗi chain sau skill thứ 2        │
│  │  └─ Katana: Focus → đứng yên tích lực = bonus 50%    │
│  ├─ Dodge (né tránh, bất tử trong i-frame, tốn stamina) │
│  ├─ Block/Parry (kỹ năng mặc định, nâng cấp được)      │
│  └─ Pet chiến đấu cùng (nếu đã triệu hồi)             │
│                                                          │
│  Giết quái                                              │
│  ├─ Popup EXP + Vàng + Item rơi                        │
│  ├─ Lên level → 5 điểm chỉ số + 1 SP                  │
│  └─ Tiến trình quest tự động cập nhật                   │
│                                                          │
│  Bắt quái (Ngự Thú Sư)                                 │
│  ├─ Đánh quái xuống < 30% HP                           │
│  ├─ Dùng vật phẩm bắt → roll tỉ lệ                    │
│  └─ Thành công → thêm vào kho thú cưng                 │
│                                                          │
│  Chết → Mất 10% vàng                                   │
│  ├─ Hồi sinh tại thị trấn (miễn phí, full HP)          │
│  └─ Hồi sinh tại chỗ (tốn vàng, 50% HP)               │
│                                                          │
├─── KHU VỰC BOSS ────────────────────────────────────────┤
│                                                          │
│  Boss (VAT, HP cao, nhiều đòn đặc biệt, phần thưởng     │
│  hiếm)                                                   │
│  Tương lai: Boss nhiều giai đoạn (phase), combo chain    │
│                                                          │
├─── TRỞ VỀ THỊ TRẤN ────────────────────────────────────┤
│                                                          │
│  Bán đồ thừa → Trang bị → Rèn/Nâng cấp → Học skill    │
│  → Lặp lại ở khu vực khó hơn                           │
│                                                          │
└──────────────────────────────────────────────────────────┘
```

### Nhịp chiến đấu (Combat Rhythm)

```
Auto-attack ──→ Tích MP ──→ Đủ MP ──→ Bung Skill ──→ Tiêu MP ──→ Auto-attack lại
     ↑                                      │
     └──────────────────────────────────────┘
                     LẶP LẠI

Mỗi auto-attack trúng: +50 + LUK×2 MP
TAO skills tốn Chi thay MP → quản lý 2 resource song song
Combo: Skill → Skill → Skill trong 0.5s window → bonus damage chồng
```

---

## 4. HỆ THỐNG CHỈ SỐ

### 7 chỉ số gốc
| Chỉ số | Tên đầy đủ | Ảnh hưởng |
|--------|-----------|-----------|
| **STR** | Sức Mạnh | Tấn công vật lý, chịu đòn |
| **INT** | Trí Tuệ | Tấn công phép, phòng phép, hồi MP |
| **AGI** | Nhanh Nhẹn | Tốc độ đánh, tốc độ di chuyển, né |
| **DEX** | Khéo Léo | Chí mạng, tốc độ đánh, chính xác |
| **VIT** | Sinh Lực | HP, Stamina, phòng vật lý, Chi max |
| **LUK** | May Mắn | Chí mạng, né, hồi MP từ auto-attack |
| **TECH** | Kỹ Thuật | Thời gian parry, craft success rate |

### Chỉ số phái sinh (tự tính từ gốc)
```
MaxHP = 100 + VIT × 15
MaxMP = 50 + INT × 12
MaxStamina = 100 + VIT × 8 + AGI × 4
MaxChi = 100 + VIT × 5
Tấn công vật lý = STR × 2 + DEX × 0.5
Tấn công phép = INT × 2.5
Tốc độ đánh = 1.0 + (AGI-10) × 0.02 + (DEX-10) × 0.01
Tỉ lệ chí mạng = 5% + DEX × 0.5% + LUK × 0.3%
Sát thương chí mạng = 1.5 + LUK × 0.015
Né tránh = AGI × 0.4% + LUK × 0.2%
```

### 4 tài nguyên chiến đấu
| Resource | Mô tả | Hồi phục |
|----------|-------|----------|
| **HP** | Sinh lực | Chậm, VIT tăng tốc |
| **MP** | Nội lực (thi triển skill vũ khí) | Auto-attack hồi MP, hồi tự nhiên chậm |
| **Stamina** | Thể lực (dodge, sprint) | Hồi nhanh, chậm hơn trong chiến đấu |
| **Chi** | Khí (skill Đạo Thuật) | +5/đòn đánh, +10/bị đánh, -5/s ngoài chiến |

---

## 5. 15 NHÁNH KỸ NĂNG (Cây Võ Học)

### Nhánh Vũ Khí (8)

| # | Nhánh | Vũ khí | Chỉ số chính | Phong cách chơi |
|---|-------|--------|-------------|-----------------|
| 1 | **Kiếm Pháp** | Kiếm 1 tay | STR+DEX | Cân bằng, combo linh hoạt, phù hợp mới chơi |
| 2 | **Trọng Kiếm Đạo** | Đại kiếm 2 tay | STR | Chậm, đòn nặng, super armor (không bị flinch khi thi triển) |
| 3 | **Nhẫn Đạo** | Katana | DEX | Focus gauge — đứng yên tích lực, counter stance phản đòn. "Kiên nhẫn = Sức mạnh" |
| 4 | **Song Kiếm Thuật** | Song kiếm | AGI+STR | Đánh nhiều đòn, tỉ lệ chí mạng cao, tốc độ nhanh |
| 5 | **Thủ Thuật** | Kiếm + Khiên | VIT+STR | Tank, taunt (kéo quái), nâng cấp Block/Parry mặc định |
| 6 | **Thương Pháp** | Thương | STR+AGI | Tầm trung, AoE hàng ngang, đâm xuyên tuyến |
| 7 | **Kích Pháp** | Kích | STR+DEX | Quét rộng AoE, buff tốc độ đánh |
| 8 | **Xạ Thuật** | Cung | DEX | Đánh xa, charge shot, mưa tên AoE |

### Nhánh Chia Sẻ (3) — Mọi vũ khí đều dùng được

| # | Nhánh | Chỉ số chính | Phong cách chơi |
|---|-------|-------------|-----------------|
| 9 | **Võ Thuật** | AGI+STR | Đấm/đá, nhanh nhất game, stun lock kẻ địch |
| 10 | **Đạo Thuật (TAO)** | INT+VIT | Dùng **Chi** thay MP. Buff/heal bản thân, nổ khí AoE. Mọi build đều hưởng lợi |
| 11 | **Ma Thuật** | INT | Gậy/Pháp cụ, đòn phép AoE, cast time, nguyên tố (lửa/băng/sét) |

### Nhánh Kỹ Năng Sống (3)

| # | Nhánh | Chỉ số chính | Chức năng |
|---|-------|-------------|-----------|
| 12 | **Luyện Khí Sư** | TECH | Rèn vũ khí/giáp, nâng cấp +1→+10 |
| 13 | **Điều Chế Sư** | TECH+INT | Pha thuốc hồi/buff, bom damage, dầu nguyên tố |
| 14 | **Ngự Thú Sư** | LUK+VIT | Bắt quái, nuôi, chiến đấu cùng, Fuse (hợp nhất 30s) |

### Nhánh Phổ Thông (1)

| # | Nhánh | Mô tả |
|---|-------|-------|
| 15 | **Sinh Tồn** | Passive: +HP%, +kháng tính, +thời gian dodge. Mọi build nên đầu tư |

### Kinh tế SP (Skill Point)
```
1 SP mỗi level. Level cap = giới hạn theo content (50 ra mắt, 60/70/80 cập nhật sau).
Chi phí nâng skill (tăng dần): {1,1,1,2,2,2,3,3,3,3} = 21 SP để max 1 skill.
Level 50: 50 SP → chỉ max được 2 skill (42 SP) + vài passive (8 SP).
→ BUỘC PHẢI CHỌN BUILD. Reset tốn vàng.
```

---

## 6. HỆ THỐNG ENEMY + PACK AI

### PackManager — Quản lý bầy quái tập trung

Mỗi SpawnZone có 1 PackManager. **Từng con quái KHÔNG tự quyết định đuổi hay không** — PackManager nhìn tổng thể rồi ra lệnh.

### 5 mức đe dọa (Threat Level)

| Level gap (player - enemy) | Mức | Hành vi bầy |
|---------------------------|-----|-------------|
| >= +10 | **Kinh Hoàng** | Cả bầy BỎ CHẠY |
| +5 đến +9 | **Dè Chừng** | Né tránh, chỉ PHẢN ĐÒN khi bị đánh |
| -2 đến +4 | **Bình Thường** | 2-3 con đuổi, còn lại quan sát |
| -3 đến -7 | **Hung Hăng** | 4-5 con đuổi, phạm vi phát hiện rộng |
| <= -8 | **Khát Máu** | CẢ BẦY lao vào — vùng chết |

### 9 trạng thái AI của từng con quái
```
Idle → Patrol → Alert → Chase → Attack → Retreat → Flee → ReactiveDefend → Dead

Idle:     Đứng yên, chờ
Patrol:   Đi loanh quanh trong bán kính spawn
Alert:    Biết player ở đâu nhưng CHƯA được đuổi (nervously quan sát)
Chase:    Được PackManager cho phép → đuổi player (NavMesh)
Attack:   Trong tầm đánh → ra đòn
Retreat:  Chase quá xa / quái chết → quay về spawn point
Flee:     Player quá mạnh → chạy ngược hướng player
ReactiveDefend: Bị đánh → phản đòn 1-2 hit rồi rút
Dead:     Chết → loot → despawn về pool
```

### VAT_MobSpawner — Spawn theo bầy
```
Player vào activationRange (50m) → Bill.Pool.Spawn × packSize con
Player ra despawnRange (80m) → Bill.Pool.Return tất cả
Bầy chết hết → đợi respawnDelay (30s) → spawn lại
Khoảng cách LOD:
  Gần: AI đầy đủ + render
  Vừa: Chỉ render idle animation, tắt AI
  Xa: Despawn hẳn về pool
```

---

## 7. HỆ THỐNG CRAFTING + NÂNG CẤP

### Luyện Khí Sư (Rèn)
```
Nguyên liệu + Vàng → Roll tỉ lệ → Sản phẩm
Tỉ lệ thành công = baseRate + (skillLevel - requiredLevel) × 5%
```

### Nâng cấp vũ khí (+1 đến +10)
| Cấp | Tỉ lệ | Hậu quả thất bại |
|-----|--------|-------------------|
| +1→+3 | 100% | Không mất gì |
| +4→+5 | 80% | Không mất gì |
| +6 | 60% | Không mất gì |
| +7 | 40% | **Giảm về +6** (trừ khi có Đá Bảo Hộ) |
| +8 | 30% | Giảm về +7 |
| +9 | 20% | Giảm về +8 |
| +10 | 10% | Giảm về +9 |

```
Bonus stat: baseStat × (1 + 0.05 × enhanceLevel)
→ +5 = 25% mạnh hơn, +10 = 50% mạnh hơn
Hiệu ứng ánh sáng: +1~3 nhạt, +4~6 xanh, +7~9 tím, +10 vàng gold + trail
Level Luyện Khí Sư: +2% tỉ lệ mỗi level (tối đa +20%)
```

### Điều Chế Sư (Pha chế)
```
Thuốc: HP Potion, MP Potion, Chi Potion, Stamina Potion, Giải Độc
Elixir: Buff Tấn Công 30s, Buff Phòng Thủ 30s, Buff Tốc Độ 30s, Buff EXP 10 phút
Bom: Bom Lửa (AoE damage), Bom Băng (AoE slow), Bom Sáng (AoE stun)
Dầu Nguyên Tố: Dầu Lửa → vũ khí có fire damage 60s (tương lai)
```

---

## 8. HỆ THỐNG THÚ CƯNG (Ngự Thú Sư)

### Luồng bắt quái
```
1. Đánh quái xuống < 30% HP
2. Dùng "Quả Cầu Bắt Thú" (consumable, chế bởi Điều Chế Sư)
3. Roll: tỉ lệ = baseCaptureRate × (1 - HP%hiện tại) × tamerSkillBonus
   tamerSkillBonus = 1 + (level Ngự Thú Sư × 5%)
4. Thành công → quái biến mất, thêm vào kho thú cưng
5. Thất bại → mất quả cầu, quái vẫn sống
6. Boss/Elite = KHÔNG bắt được
```

### Chiến đấu cùng pet
```
Triệu hồi pet → pet đi theo player
Lock-on quái → pet tự đánh cùng target
Pet dùng 1-2 skill (cooldown-based)
Pet nhận damage từ AoE (có thể chết → tự thu hồi, hồi sinh sau 60s)
Pet nhận 50% EXP từ quái chết
Gắn kết +1 mỗi kill cùng nhau (bond 0-100)
```

### Hợp Nhất (Fuse) — Ultimate
```
Kích hoạt Fuse → player nhận stat bonus của pet trong 30s
Pet biến mất trong thời gian Fuse
Kết thúc → pet xuất hiện lại
Cooldown dài → chỉ dùng lúc cần thiết (boss fight)
```

---

## 9. HỆ THỐNG QUEST + DIALOGUE + NPC

### Quest tự động theo dõi
```
Nhận quest → Mục tiêu hiện trên HUD
Giết quái đúng loại → tự đếm (Kill quest)
Nhặt item đúng loại → tự đếm (Collect quest)
Nói chuyện NPC → tự hoàn thành (Talk quest)
Đến khu vực → tự phát hiện (Reach quest)
Craft đồ → tự đếm (Craft quest)
Bắt quái → tự đếm (Capture quest)
Hoàn thành → trả quest → nhận EXP + Vàng + Item
```

### Dialogue
```
Node-based: Text → Choice → Condition → Event
Mỗi NPC có greetingDialogue → mở DialoguePanel
Choice: player chọn → rẽ nhánh hội thoại
Condition: kiểm tra quest/level → rẽ nhánh tự động
Event: cho quest, mở shop, tặng item
```

---

## 10. SAVE/LOAD + SCENE + DEATH

### Lưu game
```
Auto-save khi: Level up, đổi zone, trang bị thay đổi, quest hoàn thành, mỗi 5 phút
Manual save: 3 slot
Dữ liệu: level, EXP, chỉ số, túi đồ, trang bị, skill, quest, pet, vị trí, cài đặt
Lưu bằng ID string (không reference SO trực tiếp) → sẵn sàng cho SpacetimeDB
```

### Chuyển zone
```
Player bước vào Portal → AutoSave → Bill.Scene.Load → spawn ở SpawnPoint đích
BGM tự đổi theo zone → hiện tên khu vực fade-in
Khu vực đầu tiên → đánh dấu "đã khám phá"
```

### Chết + Hồi sinh
```
HP ≤ 0 → DeadState → camera zoom out + grayscale
Mất 10% vàng hiện tại
Tùy chọn:
  A) Hồi sinh tại thị trấn (miễn phí, full HP/MP)
  B) Hồi sinh tại chỗ (tốn vàng = 5% tổng vàng, tối thiểu 100, 50% HP)
Bất tử 3 giây sau khi hồi sinh
```

---

## 11. ANIMATION CẦN CHUẨN BỊ

### Player (Mecanim Animator)

**Quy ước tên: `{WeaponType}_{Action}`**

#### Exploration (dùng chung mọi vũ khí)
| # | Tên clip | Mô tả |
|---|---------|-------|
| 1 | Explore_Idle | Đứng yên khám phá |
| 2 | Explore_Walk | Đi bộ |
| 3 | Explore_Run | Chạy |
| 4 | Explore_Sprint | Chạy nước rút |
| 5 | Explore_Jump | Nhảy |
| 6 | Explore_DoubleJump | Nhảy đôi |
| 7 | Explore_Fall | Rơi |
| 8 | Explore_Land_Soft | Tiếp đất nhẹ |
| 9 | Explore_Land_Hard | Tiếp đất nặng |
| 10 | Explore_Dash | Lao tới |

#### Combat — Mỗi loại vũ khí (ví dụ Sword)
| # | Tên clip | Mô tả |
|---|---------|-------|
| 1 | Sword_Idle | Đứng sẵn sàng chiến đấu |
| 2 | Sword_Walk_Fwd | Đi tới (lock-on) |
| 3 | Sword_Walk_Back | Đi lùi |
| 4 | Sword_Walk_Left | Đi trái |
| 5 | Sword_Walk_Right | Đi phải |
| 6 | Sword_Atk1 | Combo đòn 1 |
| 7 | Sword_Atk2 | Combo đòn 2 |
| 8 | Sword_Atk3 | Combo đòn 3 |
| 9 | Sword_Hit_Light | Bị đánh nhẹ |
| 10 | Sword_Hit_Heavy | Bị đánh nặng |
| 11 | Sword_Knockback | Bị đẩy lùi |
| 12 | Sword_Equip | Rút kiếm |
| 13 | Sword_Unequip | Thu kiếm |

#### Dùng chung mọi vũ khí (Combat)
| # | Tên clip | Mô tả |
|---|---------|-------|
| 1 | Dodge_Fwd | Né trước |
| 2 | Dodge_Back | Né sau |
| 3 | Dodge_Left | Né trái |
| 4 | Dodge_Right | Né phải |
| 5 | Death | Ngã chết |
| 6 | Skill_Charge | Tư thế tích chiêu (charge) |

#### Skill Animation
Mỗi skill active có 1 clip riêng, đặt tên trong SkillData.vatAnimClip. Ví dụ:
```
Blade_HardHit        — Trảm Kích (Kiếm Pháp)
Blade_SonicBlade     — Phi Kiếm Kỳ (Kiếm Pháp)
Katana_CounterStance — Phản Kích Trạm (Nhẫn Đạo)
Tao_ChiBurst         — Khí Bộc Phát (Đạo Thuật)
```

### Tổng hợp animation player
```
Exploration:           10 clips  (dùng chung)
Combat chung:           6 clips  (dodge × 4, death, skill charge)
Mỗi loại vũ khí:      13 clips
15 loại vũ khí:       195 clips  (đầy đủ)

MVP (1 loại vũ khí):   10 + 6 + 13 = 29 clips
Đầy đủ 15 vũ khí:      10 + 6 + 195 = 211 clips
+ Skill animations:    ~3-5 clip/nhánh × 14 nhánh = ~50-70 clips
```

### Enemy (VAT — mỗi loại quái)

| # | Tên clip | Mô tả | Bắt buộc |
|---|---------|-------|----------|
| 1 | Idle | Đứng yên | BẮT BUỘC |
| 2 | Walk | Đi/chạy | BẮT BUỘC |
| 3 | Attack1 | Đòn tấn công chính | BẮT BUỘC |
| 4 | Attack2 | Đòn tấn công phụ | Tùy chọn |
| 5 | Hit | Bị đánh flinch | BẮT BUỘC |
| 6 | Death | Chết | BẮT BUỘC |
| 7 | Attack3 | Đòn đặc biệt (boss) | Boss only |
| 8 | Phase | Chuyển giai đoạn (boss) | Boss only |

```
Mỗi quái thường: 5 clips (Idle, Walk, Attack1, Hit, Death)
Mỗi boss:        8-10 clips
```

---

## 12. QUÁI VẬT MẪU — Nội Dung Game

### Khu vực Khởi Đầu (Level 1-10): Đồng Cỏ Xanh

| Quái | Tier | HP | DMG | VAT clips | Hành vi |
|------|------|-----|-----|-----------|---------|
| Nhớt Xanh (Slime) | Thường | 50 | 5 | 5 | Chậm, cận chiến, bầy 5 con |
| Sói Hoang | Thường | 80 | 8 | 5 | Nhanh, cận chiến, bầy 4 con |
| Yêu Tinh (Goblin) | Thường | 60 | 10 | 6 | Trung bình, ném đá + đánh, bầy 5 con |

### Khu vực Giữa (Level 10-25): Rừng Cổ Thụ

| Quái | Tier | HP | DMG | VAT clips | Hành vi |
|------|------|-----|-----|-----------|---------|
| Thú Nhân Chiến Binh | Thường | 150 | 20 | 5 | Chậm, đòn nặng |
| Tinh Linh Rừng | Thường | 100 | 15 | 5 | Đánh xa, phép thuật |
| Nhện Khổng Lồ | Tinh Anh | 300 | 25 | 6 | Nhanh, DoT độc |

### Khu vực Cuối (Level 25-50): Tàn Tích Cổ Đại

| Quái | Tier | HP | DMG | VAT clips | Hành vi |
|------|------|-----|-----|-----------|---------|
| Hắc Kỵ Sĩ | Tinh Anh | 500 | 40 | 6 | Block+parry, combo |
| Rồng Con | Tiểu Boss | 1.000 | 60 | 8 | AoE lửa, bay lên |
| Cổ Long (Boss) | Boss | 5.000 | 100 | 10 | Nhiều giai đoạn, quét rộng |

### Tổng hợp MVP
```
3 loại quái thường + 1 boss = 4 EnemyData SO
4 VAT prefab (bake từ model)
5×3 + 10 = 25 animation clips cho enemy
```

---

## 13. DANH SÁCH FILE HIỆN TẠI (77 file)

### Giao diện & Enum (6 file)
```
Interfaces/IStatProvider.cs         Chỉ số, modifier
Interfaces/ICombat.cs               Chiến đấu, sát thương, khóa mục tiêu
Interfaces/IWeapon.cs               Vũ khí, animation set
Interfaces/IAnimationController.cs  Điều khiển animation
Enums/GameEnums.cs                  Tất cả enum game (25+ enum)
Data/SharedDataTypes.cs             Class dùng chung (StatBonus, ItemStack...)
```

### Nền Tảng (5 file)
```
Core/Game.cs                                 Facade tĩnh
Core/Player/PlayerCore.cs                    Hub 18 hệ thống
Core/Localization/LocalizationService.cs     Đa ngôn ngữ
Core/Localization/Loc.cs                     Lối tắt Loc.Get()
Core/Localization/LocalizedText.cs           TMP tự cập nhật
```

### Chiến Đấu (12 file)
```
Core/Combat/StateMachine/CombatStateMachine.cs   Máy trạng thái chính
Core/Combat/StateMachine/CombatState.cs          Base class
Core/Combat/StateMachine/CombatStates.cs         Idle, Engaged, Attack, Dodge, HitStun, Dead
Core/Combat/States/SkillChargeState.cs           Tích chiêu (charge)
Core/Combat/States/SkillExecuteState.cs          Thi triển skill + gây damage
Core/Combat/States/ComboReadyState.cs            Cửa sổ combo
Core/Combat/PlayerDamageHandler.cs               Cầu nối damage (player)
Core/Combat/FocusGauge.cs                        Focus gauge (Katana)
Core/Combat/Hitbox/HitboxManager.cs              Quản lý hitbox
Core/Combat/EnemyBase.cs                         Base class quái
Core/Combat/DamagePipeline.cs                    Pipeline tính sát thương
Core/Combat/AutoAttackSystem.cs                  Tự đánh khi lock-on
Core/Combat/CombatLocomotion.cs                  Di chuyển khi chiến đấu
Core/Combat/LockOnSystem.cs                      Khóa mục tiêu
```

### Kỹ Năng (4 file)
```
Core/Skill/PlayerSkillBook.cs     Sách kỹ năng, học/nâng cấp, SP
Core/Skill/SkillBar.cs            4 slot + 2 mặc định (block/parry)
Core/Skill/SkillCaster.cs         Thi triển skill
Core/Skill/ComboTracker.cs        Theo dõi combo
```

### Hệ Thống Lõi (7 file)
```
Core/Health/HealthSystem.cs                HP/MP/Stamina/Chi, hồi phục
Core/Inventory/Inventory.cs                30 slot, xếp chồng, vàng
Core/Inventory/EquipmentSystem.cs          8 slot trang bị
Core/LevelSystem/LevelSystem.cs            EXP, level, điểm chỉ số/SP
Core/StatusEffect/StatusEffectSystem.cs    Buff/debuff, DoT/HoT
Core/Loot/LootSystem.cs                   Xử lý phần thưởng khi quái chết
Weapons/WeaponVisualHandler.cs             Mesh vũ khí trên tay
```

### AI Quái (3 file)
```
Core/AI/EnemyAI.cs               9 trạng thái
Core/AI/PackManager.cs           Quản lý bầy, mức đe dọa
Core/AI/VAT_MobSpawner.cs       Spawn theo bầy
```

### Hệ Thống Thế Giới (8 file)
```
Core/Quest/QuestTracker.cs        Nhiệm vụ
Core/Dialogue/DialogueSystem.cs   Hội thoại
Core/NPC/ShopService.cs           Cửa hàng
Core/Zone/ZoneSystem.cs           Chuyển zone
Core/Zone/Portal.cs               Cổng truyền
Core/Death/DeathSystem.cs         Chết + hồi sinh
Core/SaveLoad/SaveData.cs         Cấu trúc dữ liệu lưu
Core/SaveLoad/SaveLoadSystem.cs   Lưu/tải game
```

### Kỹ Năng Sống (3 file)
```
Core/Crafting/CraftingSystem.cs       Rèn/Pha chế
Core/Crafting/WeaponEnhancement.cs    Nâng cấp +1→+10
Core/Tamer/TamerSystem.cs             Bắt/nuôi/chiến đấu thú cưng
```

### Dữ Liệu ScriptableObject (17 file)
```
Data/WeaponData.cs            Vũ khí
Data/SkillData.cs             Kỹ năng (40+ trường)
Data/SkillTreeData.cs         Cây kỹ năng
Data/ItemData.cs              Vật phẩm
Data/StatusEffectData.cs      Buff/Debuff
Data/EnemyData.cs             Quái vật
Data/LootTable.cs             Bảng drop
Data/QuestData.cs             Nhiệm vụ
Data/DialogueData.cs          Hội thoại
Data/NPCData.cs               NPC
Data/ShopData.cs              Cửa hàng
Data/ZoneData.cs              Khu vực
Data/PetData.cs               Thú cưng
Data/RecipeData.cs            Công thức
Data/LocalizationConfig.cs    Cấu hình ngôn ngữ
Data/ItemDatabase.cs          Tra cứu item bằng ID
Data/SkillDatabase.cs         Tra cứu skill bằng ID
```

### Còn Lại (5 file)
```
Input/PlayerInputHandler.cs              Nhập liệu (skill 1-4 đã thêm)
Core/Animation/AnimationController.cs    Điều khiển Animator
Core/Locomotion/*                        3 file di chuyển khám phá
Core/Stats/CharacterStats.cs             Chỉ số nhân vật
Core/Player/PlayerController.cs          Quản lý mode Exploration↔Combat
Camera/CameraController.cs              Camera
Weapons/WeaponHandler.cs                Quản lý vũ khí
Editor/RPGModularSetupWizard.cs         Setup wizard trong Editor
Resources/Localization/vi.json           Tiếng Việt
Resources/Localization/en.json           Tiếng Anh
```

---

## 14. QUYẾT ĐỊNH THIẾT KẾ QUAN TRỌNG

| Quyết định | Lựa chọn | Lý do |
|-----------|---------|-------|
| Block/Parry | Skill mặc định (không phải state riêng) | Toram-style, dựa trên skill, nâng cấp được qua Thủ Thuật |
| Animation quái | VAT toàn bộ (kể cả boss) | GPU instancing, render hàng trăm con, occlusion culling hoạt động |
| Chi gauge | Tài nguyên thứ 3 (HP/MP/Chi) | Đặc trưng Đạo Thuật, quản lý 2 resource song song |
| Focus gauge | Riêng Katana (0-100) | Bản sắc "kiên nhẫn = sức mạnh" |
| PackManager | Tập trung (không trên từng con) | Ngăn 50 con lao vào cùng lúc |
| Mức đe dọa | 5 cấp theo level gap | Cảm nhận tự nhiên, không cần UI giải thích |
| Kinh tế SP | 1 SP/level, cap=50 → max 2 skill | Buộc chọn build, reset tốn vàng |
| Đa ngôn ngữ | Mọi text qua key, không hardcode | Sẵn sàng đa ngôn ngữ từ ngày đầu |
| Save | ID-based (không reference SO) | Sẵn sàng SpacetimeDB |
| BillGameCore | Dùng Bill.Pool/Audio/Save/UI/Scene | Không duplicate framework |

---

## 15. VIỆC CÒN LẠI

### Phase 7: UI (Bill.UI BasePanel)
```
□ HUDPanel — Thanh HP/MP/Chi/Stamina, skill bar, EXP, vàng, combo, mục tiêu
□ InventoryPanel — Lưới 30 ô, kéo-thả, click phải dùng/trang bị
□ EquipmentPanel — 8 ô trang bị, so sánh chỉ số
□ SkillTreePanel — 14 tab nhánh, node skill, đếm SP
□ QuestPanel — Tab Đang làm/Đã xong, danh sách mục tiêu
□ DialoguePanel — Chân dung NPC, hộp thoại, nút chọn
□ ShopPanel — Lưới mua/bán
□ CraftingPanel — Danh sách công thức, kiểm tra nguyên liệu
□ PetPanel — Kho thú cưng, chỉ số pet
□ DeathPanel — Tùy chọn hồi sinh
□ SettingsPanel — Ngôn ngữ, âm thanh, đồ họa, phím
□ DamagePopup — Số sát thương bay lên
□ LootPopup — Thông báo nhặt đồ
```

### Phase 8: Tích Hợp + Đánh Bóng
```
□ PlayerController.cs — Nối SkillCaster, ComboTracker, WeaponVisualHandler
□ AutoAttackSystem.cs — +MP hồi mỗi đòn (+50 + LUK×2), +Chi (+5)
□ Bill.Audio — SFX đánh/skill, BGM mỗi zone
□ VFX — Hiệu ứng skill, ánh sáng nâng cấp, level up
□ Test toàn bộ gameplay loop
□ Cân bằng (damage, EXP, tỉ lệ drop)
□ MegaSetupScript — Script tự gắn tất cả component lên Player GO
```

### Phase 9: SpacetimeDB (MMORPG tương lai)
```
□ Tích hợp SpacetimeDB SDK
□ Abstraction IGameService
□ Hệ thống lõi online (inventory, equip, level, skill)
□ Chiến đấu online (damage, sync enemy)
□ Xã hội (chat, party, trade)
```

---

*Kết thúc tài liệu tổng quan*
*Bước tiếp: Phase 7 (UI) → Phase 8 (Tích Hợp + MegaSetupScript)*
