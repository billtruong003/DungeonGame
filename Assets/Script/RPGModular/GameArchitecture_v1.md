# Game Architecture Document — Final Reference
### Version 1.0 | Pre-code Blueprint

---

## 1. PHILOSOPHY — API Design

Tất cả game systems đều accessible qua **static facade** giống BillGameCore pattern.
Developer gọi 1 dòng, không cần biết component nằm đâu.

```csharp
// BillGameCore pattern:
Bill.Audio.Play("bgm");
Bill.Pool.Spawn("Bullet");
Bill.Save.Set("highscore", 999);

// Game systems — CÙNG PATTERN:
Game.Stats.GetStat(StatType.STR);
Game.Health.Heal(50f);
Game.Inv.AddItem(swordData, 1);
Game.Equip.Equip(swordData, EquipSlot.MainHand);
Game.Level.AddExp(200);
Game.Skill.Cast(0);       // cast skill slot 0
Game.Loc.Get("ui.hp");    // → "Sinh Lực" (vi) / "HP" (en)
Game.Status.Apply(poisonData);
Game.Combat.IsInCombat;
```

### 1.1 Static Facade — Game.cs

```csharp
public static class Game
{
    // --- Resolve from player singleton ---
    private static PlayerCore _player;
    public static PlayerCore Player
    {
        get
        {
            if (_player == null)
                _player = Object.FindFirstObjectByType<PlayerCore>();
            return _player;
        }
    }

    // --- Shortcuts ---
    public static CharacterStats    Stats    => Player.Stats;
    public static HealthSystem      Health   => Player.Health;
    public static Inventory         Inv      => Player.Inventory;
    public static EquipmentSystem   Equip    => Player.Equipment;
    public static LevelSystem       Level    => Player.Level;
    public static PlayerSkillBook   SkillBook=> Player.SkillBook;
    public static SkillBar          SkillBar => Player.SkillBar;
    public static SkillCaster       Skill    => Player.SkillCaster;
    public static StatusEffectSystem Status  => Player.StatusEffects;
    public static CombatStateMachine Combat  => Player.CombatSM;
    public static LockOnSystem      LockOn  => Player.LockOn;
    public static WeaponHandler     Weapon   => Player.Weapon;
    public static ComboTracker      Combo    => Player.Combo;

    // --- Singletons (không trên player) ---
    public static LocalizationService Loc    => LocalizationService.Instance;

    // --- Reset khi scene change ---
    public static void ClearCache() => _player = null;
}
```

### 1.2 PlayerCore — Hub component trên Player root

```csharp
/// <summary>
/// Hub component — expose tất cả sub-systems.
/// Attach lên Player root GO. Mọi system khác reference qua đây.
/// Game.cs resolve PlayerCore → access bất kỳ system nào.
/// </summary>
public class PlayerCore : MonoBehaviour
{
    // Auto-find tất cả trong Awake
    public CharacterStats       Stats        { get; private set; }
    public HealthSystem         Health       { get; private set; }
    public Inventory            Inventory    { get; private set; }
    public EquipmentSystem      Equipment    { get; private set; }
    public LevelSystem          Level        { get; private set; }
    public PlayerSkillBook      SkillBook    { get; private set; }
    public SkillBar             SkillBar     { get; private set; }
    public SkillCaster          SkillCaster  { get; private set; }
    public ComboTracker         Combo        { get; private set; }
    public StatusEffectSystem   StatusEffects{ get; private set; }
    public PlayerDamageHandler  DamageHandler{ get; private set; }
    public CombatStateMachine   CombatSM     { get; private set; }
    public LocomotionStateMachine LocoSM     { get; private set; }
    public LockOnSystem         LockOn       { get; private set; }
    public WeaponHandler        Weapon       { get; private set; }
    public WeaponVisualHandler  WeaponVisual { get; private set; }
    public AutoAttackSystem     AutoAttack   { get; private set; }
    public PlayerInputHandler   Input        { get; private set; }
    public PlayerController     Controller   { get; private set; }

    private void Awake()
    {
        Stats         = GetComponent<CharacterStats>();
        Health        = GetComponent<HealthSystem>();
        Inventory     = GetComponent<Inventory>();
        Equipment     = GetComponent<EquipmentSystem>();
        Level         = GetComponent<LevelSystem>();
        SkillBook     = GetComponent<PlayerSkillBook>();
        SkillBar      = GetComponent<SkillBar>();
        SkillCaster   = GetComponent<SkillCaster>();
        Combo         = GetComponent<ComboTracker>();
        StatusEffects = GetComponent<StatusEffectSystem>();
        DamageHandler = GetComponent<PlayerDamageHandler>();
        CombatSM      = GetComponent<CombatStateMachine>();
        LocoSM        = GetComponent<LocomotionStateMachine>();
        LockOn        = GetComponent<LockOnSystem>();
        Weapon        = GetComponent<WeaponHandler>();
        WeaponVisual  = GetComponent<WeaponVisualHandler>();
        AutoAttack    = GetComponent<AutoAttackSystem>();
        Input         = GetComponent<PlayerInputHandler>();
        Controller    = GetComponent<PlayerController>();
    }
}
```

---

## 2. LOCALIZATION — Loc System

### 2.1 Public API

```csharp
// Lấy text
string name = Game.Loc.Get("skill.blade.hard_hit.name");
// → "Trảm Kích" (vi) / "Hard Hit" (en)

// Lấy text với biến
string msg = Game.Loc.Get("msg.damage.dealt",
    ("damage", "150"),
    ("target", Game.Loc.Get("enemy.wolf.name"))
);
// → "Gây 150 sát thương lên Sói Hoang"

// Đổi ngôn ngữ
Game.Loc.SetLanguage("en");

// Ngôn ngữ hiện tại
string lang = Game.Loc.CurrentLanguage; // "vi"

// Danh sách ngôn ngữ
LanguageConfig[] langs = Game.Loc.AvailableLanguages;
```

### 2.2 Events

```
OnLanguageChanged(string langCode)
    → Tất cả LocalizedText component tự refresh
    → UI rebuild text
```

### 2.3 Data files

```
Resources/
  Localization/
    vi.json          ← Vietnamese (default)
    en.json          ← English (fallback)
    ja.json          ← Japanese
    zh-cn.json       ← Chinese Simplified
    ko.json          ← Korean
    th.json          ← Thai
  LocalizationConfig.asset  ← SO: supported languages, font per lang
```

### 2.4 Quy tắc key

```
{category}.{subcategory}.{id}.{field}

skill.blade.hard_hit.name       Tên skill
skill.blade.hard_hit.desc       Mô tả skill
item.weapon.iron_sword.name     Tên item
item.weapon.iron_sword.desc     Mô tả item
enemy.wolf.name                 Tên enemy
enemy.boss.dragon.phase2        Tên boss phase
stat.str.name                   "Sức Mạnh"
stat.str.short                  "STR"
tree.blade.name                 "Kiếm Pháp"
ui.hud.hp                       "Sinh Lực"
ui.hud.mp                       "Nội Lực"
ui.hud.chi                      "Khí"
msg.skill.no_mp                 "Không đủ nội lực!"
msg.levelup                     "Đã đạt cấp {level}!"
```

### 2.5 SO field convention — TẤT CẢ ScriptableObject

Mọi SO có text player nhìn thấy → dùng KEY, không dùng text trực tiếp.

```csharp
// ĐÚNG:
public class SkillData : ScriptableObject
{
    public string nameKey;  // "skill.blade.hard_hit"
    public string descKey;  // "skill.blade.hard_hit.desc"
}

// SAI:
public class SkillData : ScriptableObject
{
    public string skillName;    // "Trảm Kích" ← HARDCODE, KHÔNG LÀM
    public string description;  // "Chém mạnh..." ← HARDCODE
}

// Runtime lấy text:
string displayName = Game.Loc.Get(skillData.nameKey);
```

Áp dụng cho: SkillData, SkillTreeData, WeaponData, ItemData,
ArmorData, EnemyData, StatusEffectData, BossPhase, BossCombo.

---

## 3. INVENTORY SYSTEM

### 3.1 Public API

```csharp
// Thêm item
int overflow = Game.Inv.AddItem(hpPotionData, 5);
// overflow = 0 → thêm hết. overflow = 2 → inventory đầy, 2 cái không vào được.

// Xóa item
int removed = Game.Inv.RemoveItem(hpPotionData, 3);

// Kiểm tra
bool has = Game.Inv.HasItem(ironOre, 10);    // có 10 iron ore không?
int count = Game.Inv.GetItemCount(ironOre);   // có bao nhiêu?

// Lấy slot
ItemStack slot = Game.Inv.GetSlot(5);         // slot index 5
// slot.Data = ItemData, slot.Quantity = int

// Dùng consumable
bool used = Game.Inv.UseItem(slotIndex, Game.Health);
// → auto heal/mana restore nếu là potion

// Swap slots (cho UI drag-drop)
Game.Inv.SwapSlots(3, 7);

// Sort
Game.Inv.SortByType();  // group by ItemType

// Gold
Game.Inv.AddGold(500);
bool canBuy = Game.Inv.SpendGold(200);
int gold = Game.Inv.Gold;
```

### 3.2 Events

```
OnSlotChanged(int slotIndex, ItemStack newStack)    → UI slot update
OnItemAdded(ItemData item, int quantity)             → pickup notification
OnItemRemoved(ItemData item, int quantity)           → sell/use notification
OnGoldChanged(int newGold)                           → UI gold display
OnInventoryFull()                                    → "Hành trang đã đầy!"
```

### 3.3 Config

```
maxSlots = 30        (mở rộng được, ví dụ quest reward +5 slots)
```

### 3.4 ItemData SO fields

```csharp
[CreateAssetMenu(menuName = "Game/Item Data")]
public class ItemData : ScriptableObject
{
    // Identity (localized)
    public string itemID;              // unique, ví dụ "iron_sword"
    public string nameKey;             // "item.weapon.iron_sword"
    public string descKey;             // "item.weapon.iron_sword.desc"
    public Sprite icon;
    public ItemType type;              // Weapon, Armor, Consumable, Material, QuestItem, Accessory
    public ItemRarity rarity;          // Common, Uncommon, Rare, Epic, Legendary

    // Stack
    public int maxStack = 99;          // weapon/armor = 1, material = 99

    // Economy
    public int sellPrice = 10;

    // Equipment (nếu equippable)
    public bool isEquippable;
    public EquipSlot defaultSlot;      // MainHand, Head, Body...
    public StatBonus[] equipBonuses;   // stat modifiers khi equip
    public StatRequirement[] requirements; // cần STR 20 mới equip

    // Consumable (nếu consumable)
    public float healAmount;
    public float manaAmount;
    public float staminaAmount;
    public float chiAmount;
    public StatusEffectData appliedBuff; // buff khi uống
}
```

---

## 4. EQUIPMENT SYSTEM

### 4.1 Public API

```csharp
// Equip (tự remove từ inventory, tự apply stat modifiers)
ItemData oldItem = Game.Equip.Equip(swordData, EquipSlot.MainHand);
// oldItem = item bị thay thế (trả về inventory). null nếu slot trống.

// Unequip (trả về inventory)
ItemData removed = Game.Equip.Unequip(EquipSlot.Head);

// Query
ItemData head = Game.Equip.GetEquipped(EquipSlot.Head);
bool empty = Game.Equip.IsSlotEmpty(EquipSlot.Feet);
float totalDef = Game.Equip.GetTotalArmorDefense();
```

### 4.2 Events

```
OnEquipped(EquipSlot slot, ItemData item)      → UI equipment screen update
OnUnequipped(EquipSlot slot, ItemData item)    → UI update
```

### 4.3 Internal flow — Equip()

```
1. Check requirements (stat check via Game.Stats)
2. Unequip old item → remove stat modifiers → return to inventory
3. Set new item → create StatModifier[] → add to Game.Stats
4. If MainHand/OffHand → call Game.Weapon.EquipWeapon()
   → WeaponVisualHandler spawns mesh
   → AnimationController swaps WeaponAnimationSet
5. Remove item from inventory
6. Fire OnEquipped event
```

### 4.4 8 Equipment slots

```
Head        → Helmet, Hat
Body        → Chest armor, Robe
Legs        → Pants, Greaves
Feet        → Boots, Sandals
MainHand    → Weapon (sword, bow, staff, knuckle...)
OffHand     → Shield, Dagger, Arrow quiver, Magic Device
Accessory1  → Ring, Necklace, Talisman
Accessory2  → Ring, Necklace, Talisman
```

---

## 5. SKILL SYSTEM

### 5.1 Public API — PlayerSkillBook

```csharp
// Học / nâng cấp skill
bool success = Game.SkillBook.LearnOrUpgrade(hardHitSkill);
// → check prerequisites, SP cost, tier unlock
// → spend SP, apply passive bonuses (nếu passive)

// Query
int level = Game.SkillBook.GetSkillLevel(hardHitSkill);   // 0 = chưa học
bool can = Game.SkillBook.CanLearn(sonicBladeSkill);       // check prereqs
List<SkillData> learned = Game.SkillBook.GetLearnedActiveSkills();
int sp = Game.SkillBook.AvailableSkillPoints;

// Respec
Game.SkillBook.ResetAllSkills(goldCost: 5000);
// → remove all passive modifiers, refund all SP
```

### 5.2 Public API — SkillBar

```csharp
// Đặt skill vào slot
Game.SkillBar.EquipSkill(hardHitSkill, slot: 0);

// Check có dùng được không
bool canUse = Game.SkillBar.CanUseSkill(0);
// → check: cooldown? MP? Chi? đúng weapon? không stunned?

// Query
SkillData s = Game.SkillBar.GetSkill(0);
float cd = Game.SkillBar.GetCooldownRemaining(0);
```

### 5.3 Public API — SkillCaster

```csharp
// Cast skill (gọi bởi input handler khi player bấm skill button)
bool cast = Game.Skill.Cast(0);  // cast skill ở slot 0
// → check CanUseSkill → consume MP/Chi → switch CombatSM state
// → play animation → spawn hitbox/projectile → combo window
```

### 5.4 Events

```
// PlayerSkillBook
OnSkillLearned(SkillData skill, int newLevel)
OnSkillPointsChanged(int remaining)

// SkillBar
OnSkillBarChanged(int slot, SkillData skill)    → UI hotbar update
OnCooldownUpdate(int slot, float remaining)      → UI cooldown sweep

// SkillCaster
OnSkillCastStart(SkillData skill)               → UI cast bar
OnSkillCastComplete(SkillData skill)            → VFX/SFX
OnSkillCastInterrupted(SkillData skill)         → "Bị gián đoạn!"

// ComboTracker
OnComboStart()                                   → UI "COMBO!"
OnComboCountChanged(int count)                   → UI combo counter
OnComboEnd()                                     → combo counter hide
```

### 5.5 SkillData SO fields

```csharp
[CreateAssetMenu(menuName = "Game/Skill Data")]
public class SkillData : ScriptableObject
{
    [Header("Identity")]
    public string skillID;
    public string nameKey;                  // localization key
    public string descKey;
    public Sprite icon;
    public SkillTreeType treeType;          // Blade, GreatSword, Katana, DualSword,
                                            // Shield, Spear, Halberd, Bow,
                                            // Martial, Tao, Sorcery,
                                            // Blacksmith, Alchemist, Tamer, Survival

    [Header("Type")]
    public SkillCategory category;          // Active / Passive
    public SkillTargetType targetType;      // SingleTarget, AoE_Circle, AoE_Cone,
                                            // AoE_Line, Projectile, Self, Party
    public DamageScaleType scaleType;       // Physical / Magical
    public WeaponType[] requiredWeapons;    // empty = any weapon

    [Header("Cost")]
    public int baseMPCost;
    public int baseChiCost;                 // TAO skills tốn Chi
    public float castTime;                  // 0 = instant
    public float cooldown;                  // 0 = no cooldown (Toram style)

    [Header("Damage")]
    public float basePower;                 // base damage % ở level 1
    public float powerPerLevel;             // +bao nhiêu per level
    public int hitCount = 1;
    public StatType primaryScalingStat;     // skill scale theo stat nào
    public float scalingRatio;

    [Header("AoE")]
    public float aoeRadius;
    public float coneAngle;
    public float range;

    [Header("Effects")]
    public StatusEffectData appliedEffect;  // Flinch, Stun, Slow...
    public float effectChance;
    public StatusEffectData selfBuff;
    public float buffDuration;

    [Header("Passive (nếu category = Passive)")]
    public StatBonus[] passiveBonuses;      // per level

    [Header("Skill Tree")]
    public int tier;                        // 1-5
    public SkillPrerequisite[] prerequisites;
    public int maxLevel = 10;
    public int[] spCostPerLevel;            // {1,1,1,2,2,2,3,3,3,3}

    [Header("Visual")]
    public string vatAnimClip;
    public float animationDuration;
    public float comboWindowAfter = 0.5f;
    public string hitVFXId;
    public string castVFXId;
    public string projectilePrefabId;
    public bool canBeInterrupted = true;
    public bool hasSuperArmor;              // Trọng Kiếm skills
}

[Serializable]
public class SkillPrerequisite
{
    public SkillData skill;
    public int requiredLevel;
}
```

### 5.6 Cast flow — exact method chain

```
INPUT: Player bấm Skill Button 1 (key binding)
  │
  ▼
PlayerInputHandler.Update()
  → skillInput[0] = true
  │
  ▼
CombatEngagedState.Tick() hoặc ComboReadyState.Tick()
  → detect skillInput[0]
  → call Game.Skill.Cast(0)
  │
  ▼
SkillCaster.Cast(slotIndex=0)
  ├→ skill = Game.SkillBar.GetSkill(0)
  ├→ level = Game.SkillBook.GetSkillLevel(skill)
  │
  ├→ GATE CHECKS:
  │   ├→ skill != null?
  │   ├→ Game.SkillBar.CanUseSkill(0)?
  │   │   ├→ cooldown ready?
  │   │   ├→ weapon compatible? (skill.requiredWeapons contains current?)
  │   │   └→ not stunned/dead?
  │   ├→ Game.Health.HasMana(mpCost)?         // mpCost = baseMPCost (có thể scale)
  │   └→ Game.Health.HasChi(chiCost)?          // nếu TAO skill
  │   Any fail → return false, fire OnSkillCastFailed
  │
  ├→ CONSUME RESOURCES:
  │   ├→ Game.Health.TryConsumeMana(mpCost)
  │   ├→ Game.Health.TryConsumeChi(chiCost)    // nếu > 0
  │   └→ Game.SkillBar.StartCooldown(0, skill.cooldown)
  │
  ├→ SWITCH COMBAT STATE:
  │   ├→ if skill.castTime > 0:
  │   │   └→ CombatSM.SwitchState(SkillChargeState)
  │   │       → play charge VFX
  │   │       → wait castTime (cancelable)
  │   │       → on complete → SkillExecuteState
  │   │
  │   └→ if skill.castTime == 0:
  │       └→ CombatSM.SwitchState(SkillExecuteState)
  │
  ▼
SkillExecuteState.Enter()
  ├→ VatAnimator.Play(skill.vatAnimClip)
  ├→ Calculate damage:
  │   power = skill.basePower + skill.powerPerLevel * (level - 1)
  │   statDmg = Physical? Stats.PhysicalAttack : Stats.MagicAttack
  │   scaling = Stats.GetStat(skill.primaryScalingStat) * skill.scalingRatio
  │   weaponATK = Weapon.MainHandWeapon?.BaseDamage ?? 5
  │   comboDmgBonus = Game.Combo.GetComboDamageBonus()
  │   rawDamage = (statDmg + weaponATK + scaling) * (power / 100) * comboDmgBonus
  │
  ├→ APPLY DAMAGE by targetType:
  │   ├→ SingleTarget:
  │   │   target = Game.LockOn.CurrentTarget
  │   │   target?.TakeDamage(rawDamage / hitCount) × hitCount
  │   │
  │   ├→ AoE_Circle:
  │   │   OverlapSphereNonAlloc(player.pos, aoeRadius, hits, enemyLayer)
  │   │   foreach hit → TakeDamage(rawDamage)
  │   │
  │   ├→ AoE_Cone:
  │   │   OverlapSphere + dot(dir, toEnemy) > cos(coneAngle/2)
  │   │   foreach in cone → TakeDamage
  │   │
  │   ├→ Projectile:
  │   │   ObjectPoolManager.Spawn(skill.projectilePrefabId)
  │   │   projectile.Initialize(rawDamage, direction, effects)
  │   │
  │   └→ Self/Buff:
  │       Game.Status.Apply(skill.selfBuff, skill.buffDuration)
  │
  ├→ Apply status effect (chance-based) to each target hit
  ├→ Spawn hit VFX
  ├→ Fire OnSkillCastComplete
  │
  ▼
Wait skill.animationDuration
  │
  ▼
CombatSM.SwitchState(ComboReadyState)
  ├→ Game.Combo.OnSkillUsed(skill)  → comboCount++
  ├→ Window open for skill.comboWindowAfter seconds
  │   ├→ Player bấm skill khác → SkillCaster.Cast() → CHAIN COMBO
  │   ├→ Player bấm dodge → DodgeState (break combo)
  │   └→ Window hết → ReturnToNeutral (auto-attack resume)
```

### 5.7 Chi gauge — mở rộng HealthSystem

```csharp
// Thêm vào HealthSystem:
public float CurrentChi { get; }
public float MaxChi     => 100f + Stats.GetStat(StatType.VIT) * 5f;
public bool  HasChi(float amount) => CurrentChi >= amount;
public bool  TryConsumeChi(float amount);

// Chi gain:
//   +5 per hit dealt (auto-attack + skill)
//   +10 per hit received
//   +25/s khi Meditate (TAO skill)
//   Passive: +Chi on crit, +Chi on dodge

// Chi decay:
//   -5/s khi out of combat (>5s không đánh/bị đánh)

// API:
Game.Health.CurrentChi;
Game.Health.ModifyChi(+5);    // gain
Game.Health.TryConsumeChi(30); // spend
```

### 5.8 MP Recovery — auto-attack hồi MP

```csharp
// Trong AutoAttackSystem, sau mỗi hit trúng enemy:
float mpRecovery = 50f + Game.Stats.GetStat(StatType.LUK) * 2f;
Game.Health.ModifyResource(ResourceType.Mana, mpRecovery);
```

### 5.9 Combo bonus

```csharp
// ComboTracker:
float GetComboSpeedBonus()
  → trong combo window: return 1.2 (20% nhanh hơn)
  → ngoài combo: return 1.0

float GetComboDamageBonus()
  → comboCount <= 2: return 1.0
  → comboCount > 2:  return 1.0 + (comboCount - 2) * 0.1
  → ví dụ: combo 4 hits = 1.0 + 0.2 = 1.2 (20% bonus)
```

---

## 6. ENEMY + PACK SYSTEM

### 6.1 EnemyBase — abstract pattern (tham khảo EchoMage)

```csharp
// Tất cả enemy dùng VAT trực tiếp.
[RequireComponent(typeof(NavMeshAgent), typeof(VAT_Animator))]
public abstract class EnemyBase : MonoBehaviour, IPoolableObject, IDamageable
{
    // --- Initialized by spawner ---
    public void Initialize(Transform target, int enemyLevel, float hpMult, float dmgMult, float spdMult);

    // --- IDamageable (simple float — không dùng DamageInfo) ---
    public void TakeDamage(float amount);

    // --- Pack commands (gọi bởi PackManager) ---
    public void CommandChase(Transform target);
    public void CommandAlert();
    public void CommandFlee();
    public void CommandRetreat();

    // --- Override bởi subclass ---
    protected abstract void PerformAttack();

    // --- Events ---
    public event Action<float>        OnHPChanged;
    public event Action               OnDeath;
    public event Action<DamageResult> OnDamageTaken;
}
```

### 6.2 Enemy states

```
Idle        → đứng yên, play idle animation
Patrol      → đi loanh quanh spawn radius
Alert       → biết player ở đâu nhưng chưa được chase (nervously watch)
Chase       → NavMesh đuổi player (PackManager cho phép)
Attack      → trong range → gọi PerformAttack()
Retreat     → chase quá xa → quay về spawn point
Flee        → player quá mạnh → chạy trốn
ReactiveDefend → Wary: bị đánh thì phản đòn 1-2 hit rồi rút
Dead        → death animation → loot → despawn
```

### 6.3 PackManager — quản lý bầy

```csharp
// Nằm trên SpawnZone GameObject, KHÔNG trên từng enemy.
public class PackManager : MonoBehaviour
{
    // Config
    [SerializeField] int baseChasers = 2;
    [SerializeField] float chaserRefillDelay = 2f;
    [SerializeField] float packAggroRadius = 15f;
    [SerializeField] float packLeashRadius = 25f;
    [SerializeField] float deaggroTime = 8f;

    // Runtime — evaluate 2-3 lần/giây
    void EvaluatePack();

    // Threat perception (tính từ level gap)
    ThreatLevel CalculateThreat(int playerLevel, int enemyLevel);
    // → Terrified (gap >= +10)
    // → Wary (gap +5 to +9)
    // → Normal (gap -2 to +4)
    // → Aggressive (gap -3 to -7)
    // → Bloodlust (gap <= -8)

    // maxChasers theo threat:
    // Terrified = 0, Wary = 0 (reactive only),
    // Normal = baseChasers, Aggressive = baseChasers + 2,
    // Bloodlust = packSize (all)
}
```

### 6.4 VAT_MobSpawner

```csharp
// Config trên SpawnZone:
public class VAT_MobSpawner : MonoBehaviour
{
    [SerializeField] GameObject vatEnemyPrefab;
    [SerializeField] int packSize = 5;
    [SerializeField] int enemyLevel = 10;        // level zone
    [SerializeField] float spawnRadius = 12f;
    [SerializeField] float respawnDelay = 30f;
    [SerializeField] float activationRange = 50f; // player gần hơn → spawn
    [SerializeField] float despawnRange = 80f;     // player xa hơn → despawn

    // Chứa PackManager reference
    PackManager packManager;
}
```

---

## 7. LEVEL SYSTEM

### 7.1 Public API

```csharp
Game.Level.AddExp(200f);
Game.Level.Level;                   // current level
Game.Level.CurrentExp;              // exp hiện tại
Game.Level.ExpToNextLevel;          // exp cần cho level tiếp
Game.Level.ExpProgress;             // 0-1 ratio
Game.Level.UnspentStatPoints;
Game.Level.SpendStatPoint(StatType.STR);
Game.Level.MaxLevel;                // content-gated cap (50, 60, 70...)
```

### 7.2 Events

```
OnLevelUp(int newLevel)                → UI level up notification + VFX
OnExpGained(float gained, float total) → UI exp bar update
OnStatPointSpent(StatType type)        → UI stat screen update
```

### 7.3 EXP formula

```
expRequired(level) = floor(100 * level^1.5)

Level 10: 100 * 10^1.5 = 3,162 EXP
Level 20: 100 * 20^1.5 = 8,944 EXP
Level 50: 100 * 50^1.5 = 35,355 EXP
```

### 7.4 Level cap gating

```
LevelSystem.maxLevel = configurable
Launch: 50
Content Update 1: 60
Content Update 2: 70
...
Chỉ cần đổi 1 value trong config.
```

---

## 8. STATUS EFFECT SYSTEM

### 8.1 Public API

```csharp
// Apply
Game.Status.Apply(poisonData);
Game.Status.Apply(atkBuffData, stacks: 3);

// Remove
Game.Status.RemoveEffect(poisonData);
Game.Status.RemoveAllDebuffs();

// Query
bool poisoned = Game.Status.HasEffect(poisonData);
float speedMult = Game.Status.GetMoveSpeedMultiplier();
```

### 8.2 Events

```
OnEffectApplied(ActiveStatusEffect)
OnEffectRemoved(ActiveStatusEffect)
OnEffectTick(ActiveStatusEffect)       → DoT/HoT number popup
OnStackChanged(ActiveStatusEffect, int newStacks)
```

---

## 9. COMBAT STATE MACHINE — Updated

### 9.1 States (Toram-style, không Dark Souls)

```
CombatIdleState       → chưa lock-on. Bấm attack → auto lock nearest → Engaged.
CombatEngagedState    → lock-on active. Auto-attack chạy. Check skill input + dodge.
AttackingState        → auto-attack combo chain đang play.
SkillChargeState      → đang charge skill (staff/bow). Cancelable.
SkillExecuteState     → skill animation đang play + gây damage.
ComboReadyState       → skill vừa xong, combo window mở. Chain hoặc return.
DodgeState            → roll i-frame. Stamina cost.
HitStunState          → bị đánh, flinch/knockback.
DeadState             → chết.

BỎ: BlockingState, ParrySuccessState, GuardBreakState
(Thủ Thuật/Shield tree có block — nhưng implement khác:
 block là skill active trong SkillExecuteState, không phải combat state riêng)
```

### 9.2 State transition map

```
Idle ←→ Engaged (lock-on toggle)
Engaged → Attacking (auto-attack trigger)
Engaged → SkillChargeState (cast skill with castTime > 0)
Engaged → SkillExecuteState (instant skill)
Engaged → DodgeState (dodge input)
Attacking → ComboReadyState (auto-attack chain done, window open)
SkillChargeState → SkillExecuteState (charge complete)
SkillChargeState → HitStunState (interrupted by enemy hit)
SkillChargeState → DodgeState (dodge cancel)
SkillExecuteState → ComboReadyState (skill done, window open)
SkillExecuteState → HitStunState (if canBeInterrupted && got hit)
ComboReadyState → SkillExecuteState (chain next skill)
ComboReadyState → Engaged (window expired → auto-attack resume)
ComboReadyState → DodgeState (dodge cancel combo)
HitStunState → Engaged (stun timer done)
Any → DeadState (HP ≤ 0)
Any → DodgeState (dodge input, if current state allows)
```

---

## 10. COMPLETE FILE INVENTORY

### Phase 2A: Localization (code FIRST)

```
NEW  Core/Localization/LocalizationService.cs      ~120 lines
NEW  Core/Localization/Loc.cs                       ~30 lines
NEW  Core/Localization/LocalizedText.cs             ~40 lines
NEW  Data/LocalizationConfig.cs                     ~30 lines
NEW  Resources/Localization/vi.json                 (growing)
NEW  Resources/Localization/en.json                 (growing)
                                                    --------
                                              Total ~220 lines
```

### Phase 2B: Game facade + PlayerCore

```
NEW  Core/Game.cs                                   ~50 lines
NEW  Core/Player/PlayerCore.cs                      ~60 lines
                                                    --------
                                              Total ~110 lines
```

### Phase 2C: Enemy + Pack + Spawner

```
NEW  Core/AI/PackManager.cs                         ~200 lines
NEW  Core/AI/EnemyAI.cs                             ~300 lines
NEW  Core/AI/VAT_MobSpawner.cs                      ~150 lines
FIX  Core/Combat/EnemyBase.cs                       +50 lines (Command methods)
NEW  Enums/ThreatLevel.cs                           ~10 lines
                                                    --------
                                              Total ~710 lines
```

### Phase 3: Skill system

```
NEW  Data/SkillData.cs                              ~80 lines
NEW  Data/SkillTreeData.cs                          ~40 lines
NEW  Enums/SkillEnums.cs                            ~30 lines
NEW  Core/Skill/PlayerSkillBook.cs                  ~150 lines
NEW  Core/Skill/SkillBar.cs                         ~100 lines
NEW  Core/Skill/SkillCaster.cs                      ~200 lines
NEW  Core/Skill/ComboTracker.cs                     ~80 lines
NEW  Core/Combat/States/SkillChargeState.cs         ~60 lines
NEW  Core/Combat/States/SkillExecuteState.cs        ~120 lines
NEW  Core/Combat/States/ComboReadyState.cs          ~50 lines
FIX  Core/Health/HealthSystem.cs                    +60 lines (Chi resource)
FIX  Core/Combat/AutoAttackSystem.cs                +10 lines (MP recovery)
FIX  Core/Combat/CombatStateMachine.cs              +15 lines (new states)
FIX  Core/Combat/CombatStates.cs                    -120 lines (remove Block/Parry)
                                                    --------
                                              Total ~875 lines
```

### Phase 4: Integration + Polish

```
FIX  Core/Player/PlayerController.cs                +30 lines
FIX  Input/PlayerInputHandler.cs                    +20 lines (skill 1-4)
FIX  Core/Loot/LootSystem.cs                        +15 lines (wire EXP/Gold)
NEW  UI/DamageNumberPopup.cs                        ~80 lines
FIX  Core/Combat/CombatStates.cs                    cleanup
                                                    --------
                                              Total ~145 lines
```

### Summary

```
Phase 1 (DONE):           ~220 lines (critical fixes)
Phase 2A Localization:     ~220 lines
Phase 2B Game facade:      ~110 lines
Phase 2C Enemy+Pack:       ~710 lines
Phase 3 Skill system:      ~875 lines
Phase 4 Integration:       ~145 lines
─────────────────────────────────────
GRAND TOTAL:             ~2,280 lines new/changed code
Files changed/created:        ~30 files
```

---

## 11. EVENT MAP — Ai fire, ai subscribe

```
EVENT                              FIRED BY              SUBSCRIBED BY
─────────────────────────────────────────────────────────────────────
HealthSystem
  OnResourceChanged(type,old,new)  HealthSystem          UI HUD bars
  OnDamageTaken(float)             HealthSystem          Camera shake, SFX
  OnDeath                          HealthSystem          CombatSM, PlayerController
  OnHealReceived(float)            HealthSystem          UI heal popup

PlayerDamageHandler
  OnDamageTaken(DamageResult)      PlayerDamageHandler   UI damage popup
  OnDamageDealt(target, result)    PlayerDamageHandler   UI damage numbers, life steal
  OnDeath                          PlayerDamageHandler   (forwarded from HealthSystem)

CharacterStats
  OnStatChanged(type, old, new)    CharacterStats        UI stat screen

LevelSystem
  OnLevelUp(int)                   LevelSystem           UI notification, VFX, SFX
  OnExpGained(float, float)        LevelSystem           UI exp bar
  OnStatPointSpent(StatType)       LevelSystem           UI stat screen

Inventory
  OnSlotChanged(int, ItemStack)    Inventory             UI inventory grid
  OnItemAdded(ItemData, int)       Inventory             UI pickup notification
  OnItemRemoved(ItemData, int)     Inventory             UI
  OnGoldChanged(int)               Inventory             UI gold display
  OnInventoryFull                  Inventory             UI warning message

EquipmentSystem
  OnEquipped(EquipSlot, ItemData)  EquipmentSystem       UI equipment screen
  OnUnequipped(EquipSlot, ItemData) EquipmentSystem      UI equipment screen

WeaponHandler
  OnWeaponChanged(IWeapon, slot)   WeaponHandler         WeaponVisualHandler, SkillBar

LockOnSystem
  OnTargetLocked(ITargetLockable)  LockOnSystem          PlayerController, Camera, UI indicator
  OnTargetLost                     LockOnSystem          PlayerController, Camera, UI

PlayerSkillBook
  OnSkillLearned(SkillData, int)   PlayerSkillBook       UI skill tree
  OnSkillPointsChanged(int)        PlayerSkillBook       UI SP counter

SkillBar
  OnSkillBarChanged(int, SkillData) SkillBar             UI hotbar
  OnCooldownUpdate(int, float)     SkillBar              UI cooldown indicator

SkillCaster
  OnSkillCastStart(SkillData)      SkillCaster           UI cast bar, SFX
  OnSkillCastComplete(SkillData)   SkillCaster           VFX, SFX
  OnSkillCastInterrupted(SkillData) SkillCaster          UI warning

ComboTracker
  OnComboStart                     ComboTracker          UI combo counter
  OnComboCountChanged(int)         ComboTracker          UI combo counter
  OnComboEnd                       ComboTracker          UI hide combo

StatusEffectSystem
  OnEffectApplied(ActiveEffect)    StatusEffectSystem    UI buff icons
  OnEffectRemoved(ActiveEffect)    StatusEffectSystem    UI buff icons
  OnEffectTick(ActiveEffect)       StatusEffectSystem    UI tick numbers

EnemyBase
  OnDeath                          EnemyBase             LootDropper, PackManager, LockOnSystem
  OnHPChanged(float)               EnemyBase             UI enemy HP bar (if targeted)
  OnDamageTaken(DamageResult)      EnemyBase             EnemyAI (aggro trigger)

PackManager
  OnThreatChanged(ThreatLevel)     PackManager           UI zone danger indicator
  OnPackWiped                      PackManager           Spawner respawn trigger

LocalizationService
  OnLanguageChanged(string)        LocalizationService   ALL LocalizedText components
```

---

*End of Architecture Document v1.0*
*Tất cả system đã sẵn sàng code.*

zipphân tích kiến trúc bộ pack này và đánh giá xem t có thể làm gì trong 1 tuần với nóApr 13Chuẩn bị phân tích kiến trúc và đánh giá khả năng thực hiệnChuẩn bị phân tích kiến trúc và đánh giá khả năng thực hiệnĐây là một bộ pack khá đồ sộ và được thiết kế tốt. Để mình phân tích chi tiết:
  ::view-transition-group(*),
  ::view-transition-old(*),
  ::view-transition-new(*) {
    animation-duration: 0.25s;
    animation-timing-function: cubic-bezier(0.19, 1, 0.22, 1);
  }
VvisualizeVvisualize show_widgetPhân tích kiến trúc
Pack này gồm 2 module lớn hoạt động cùng nhau:
BillGameCore v3 — framework bootstrap cho Unity 6, hoạt động như một "mini service container". Điểm mạnh nổi bật:

Tự khởi tạo hoàn toàn qua [RuntimeInitializeOnLoadMethod] — không cần kéo thả gì lên scene
Facade pattern qua Bill.* (ví dụ Bill.Pool.Spawn(...), Bill.Audio.Play(...)) rất sạch và tiện
Dependency tracing là killer feature — dùng StackFrame để trace xem service nào gọi service nào, phát hiện dead service, log access history. Rất có giá trị khi debug
Service lifecycle đầy đủ: IInitializable, ITickable, ILateTickable, IDisposableService
DevTools tích hợp sẵn: FPS overlay, cheat console (gõ backtick), analytics tracker xuất CSV
Network layer dùng Adapter pattern, sẵn sàng plug Photon Fusion khi cần

RPGModular — hệ thống combat Action RPG kiểu Toram Online, khá hoàn chỉnh:

7 stats (STR/INT/AGI/DEX/VIT/LUK/TECH) với hệ thống modifier 3 tầng (Flat → PercentAdd → PercentMult)
15 weapon types với animation set tự generate theo convention {WeaponType}_{Action}
Combat state machine đầy đủ 10 states: Idle → Engaged → Attack → Block → Parry → Dodge → HitStun → GuardBreak → Dead
Damage pipeline modular: Crit → Dodge → Block → Defense → MinDamage, có thể thêm/bỏ processor
Locomotion state machine riêng: Idle/Move/Sprint/Jump/DoubleJump/Fall/Land/Dash
PlayerController bridge giữa 2 mode Exploration ↔ Combat, tự equip/unequip weapon khi chuyển mode
Hitbox system tự bật/tắt theo animation phase (Startup → Active → Recovery)

Đánh giá chất lượng
Điểm mạnh: Code architecture rất sạch, tách biệt tốt qua interfaces, auto-wiring component giảm setup thủ công, naming convention nhất quán, SETUP_GUIDE.md viết chi tiết đến mức có thể setup mà không cần hỏi thêm.
Điểm yếu: PlayerCombatController.cs bị duplicate logic với CombatStateMachine (có 2 cách handle combat), SaveService dùng PlayerPrefs (hạn chế dung lượng), chưa có Inventory/Skill/Quest system.
Bạn có thể làm gì trong 1 tuần?
Dựa trên mức độ hoàn thiện hiện tại, đây là đánh giá thực tế:
Ngày 1-2: Setup + Prototype cơ bản

Import vào Unity 6, chạy Setup Wizard, tạo terrain đơn giản
Attach component theo SETUP_GUIDE, kiểm tra movement + camera hoạt động
Tìm/mua animation pack trên Mixamo (exploration + 1 weapon type, ví dụ Sword)
Setup Animator theo hướng dẫn (flat states, blend tree cho locomotion)

Ngày 3-4: Combat hoạt động

Setup 1-2 enemy với EnemyBase, tạo EnemyData ScriptableObject
Tạo WeaponData cho Sword, kiểm tra lock-on + combo chain + block
Test damage pipeline, tweak stat formulas
Thêm hitbox vào weapon bone, kiểm tra hit detection

Ngày 5: Polish cơ bản

Thêm UI cho HP/Stamina bar (dùng Bill.UI với BasePanel)
Thêm damage number popup
Setup audio (slash SFX, hit SFX, BGM) qua AudioLibrary
Test scene transition với fade

Ngày 6-7: Nội dung + Demo

Tạo 2-3 scene (menu → combat arena → boss room)
Thêm 1-2 enemy types khác nhau
Dùng cheat console để test balance nhanh
Build ra playable demo

Kết quả cuối tuần: Một playable vertical slice — nhân vật chạy nhảy, lock-on quái, đánh combo 3 hit, block/dodge, chuyển scene, có HP bar. Chưa đẹp về visual nhưng gameplay loop hoạt động hoàn chỉnh.
Bottleneck lớn nhất sẽ là animation — code đã sẵn sàng nhưng cần animation clips đúng convention. Nếu dùng Mixamo + rename đúng format Sword_Atk1, Sword_Block... thì sẽ tiết kiệm rất nhiều thời gian. Pack này được thiết kế để animation là phần plug-in, code không phụ thuộc vào animation cụ thể nào.Đọc thật kĩ code và viết ra những điểm lỗ hổng cần cải thiện của pack rpg đi, và viết ra chúng ta đã có gì, flow hoạt động của inventory hay các tính năng nền ra saoApr 13Phân tích toàn diện code RPG pack, xác định lỗ hổng và tính năng hiện cóPhân tích toàn diện code RPG pack, xác định lỗ hổng và tính năng hiện cóĐể mình đọc lại từng file thật kĩ, trace từng dependency và flow để tìm ra mọi lỗ hổng.Phân tích độc lập RPGModular và chuẩn bị đánh giá toàn diệnPhân tích độc lập RPGModular và chuẩn bị đánh giá toàn diệnTuyệt, đã trace xong toàn bộ. Giờ mình sẽ viết ra một bản phân tích đầy đủ.Marshalled comprehensive RPG pack analysis and inventory architectureMarshalled comprehensive RPG pack analysis and inventory architecture
  ::view-transition-group(*),
  ::view-transition-old(*),
  ::view-transition-new(*) {
    animation-duration: 0.25s;
    animation-timing-function: cubic-bezier(0.19, 1, 0.22, 1);
  }
VvisualizeVvisualize show_widget
  ::view-transition-group(*),
  ::view-transition-old(*),
  ::view-transition-new(*) {
    animation-duration: 0.25s;
    animation-timing-function: cubic-bezier(0.19, 1, 0.22, 1);
  }
VvisualizeVvisualize show_widgetTóm tắt nhanh những phát hiện quan trọng nhất
3 lỗ hổng critical phải fix trước khi chạy được:

Damage flow bị đứt hoàn toàn — HitboxManager gọi GetComponent<IDamageDealer>() nhưng CombatStateMachine không implement interface này. Hitbox chạm enemy → return sớm → zero damage. Đây là lỗi khiến game không chơi được.
Enemy không thể gây damage — EnemyBase.PerformAttack() chỉ play animation rồi… hết. Không gọi target.TakeDamage(), không có hitbox setup. Enemy là bù nhìn.
2 combat system song song — PlayerCombatController (cũ, có damage interface) và CombatStateMachine (mới, có dodge/parry nhưng thiếu damage interface). Phải chọn 1 và fix cái còn lại. Rõ ràng CombatStateMachine tốt hơn, cần thêm ~50 dòng code cho IDamageDealer + IDamageable wrapper.

Dead code đáng chú ý:

HandleHit() trong BlockingState/DodgeState — parry logic và i-frame logic đẹp nhưng không ai gọi
OnDamageDealt event — declare ở 3 nơi, invoke 0 nơi
CombatInputHandler — bản duplicate yếu hơn của PlayerInputHandler
EnemyData.expReward / goldReward — data có nhưng code không dùng

Hệ thống hoàn toàn chưa tồn tại: Inventory, Equipment slots (armor), Skill/Ability, Enemy AI, Loot/Drop, Level/EXP, Quest, Dialogue, HUD/UI, Save integration, Buff/Debuff, Weapon visual spawn.
Nói ngắn gọn: pack có kiến trúc interface và contract rất sạch, nhưng phần implementation bị "đứt dây" ở những chỗ quan trọng nhất. Cần khoảng 2-3 ngày fix critical bugs trước khi có thể bắt đầu build content.Rpgmodular deepanalysisDocument · MD Downloadbây giờ bắt đầu build thì sao nhỉApr 13Assessed prioritization strategy for RPG module developmentAssessed prioritization strategy for RPG module developmentTrước khi mình lên kế hoạch build, cần biết thêm vài thứ để chọn hướng đi đúng:Chọn xong mình sẽ lên plan cụ thể và bắt tay viết code fix ngay.Q: Mục tiêu 1 tuần tới là gì?
A: Build full luôn chứ k phải trong 1 tuần, m là AI m làm nhanh màApr 13Lập kế hoạch sửa lỗi và xây dựng hệ thống toàn diệnLập kế hoạch sửa lỗi và xây dựng hệ thống toàn diệnOk hiểu rồi, build full luôn. Mình bắt đầu từ fix critical bugs → rồi build thêm systems mới. Đi thôi.Orchestrated comprehensive game system architecture across multiple componentsOrchestrated comprehensive game system architecture across multiple componentsPhase 1: Fix tất cả critical bugs — tạo PlayerDamageHandler để nối damage flow, fix EnemyBase, wire HandleHit.Orchestrated multiple game systems architecture simultaneouslyOrchestrated multiple game systems architecture simultaneouslyPhase 2: Enemy AI — basic state machine cho enemy behavior.Dự tính xây dựng hệ thống kho và trang bịDự tính xây dựng hệ thống kho và trang bịPhase 3: Inventory + Equipment SystemXác định mục tiêu và giải thích phạm vi công việcXác định mục tiêu và giải thích phạm vi công việcĐã build xong Phase 1-5. Tổng kết:
12 files C# — 2,693 dòng code mới, chia thành:
3 critical fixes giải quyết damage flow bị đứt:

PlayerDamageHandler.cs — component mới nối CombatStateMachine với damage system, parry/dodge giờ hoạt động
HitboxManager.cs — fixed: tìm IDamageDealer ở parent, fire OnDamageDealt
EnemyBase.cs — fixed: dùng DamagePipeline, PerformAttack gây damage thật, EnemyStatProvider

7 hệ thống hoàn toàn mới:

EnemyAI.cs — Idle/Patrol/Chase/Attack/Retreat, NavMesh, auto-aggro, circle strafe
Inventory.cs — 30 slots, stack, sort, gold, use consumable
EquipmentSystem.cs — 8 slots, auto stat modifiers, weapon integration
LevelSystem.cs — EXP formula, multi-level-up, stat point allocation
LootSystem.cs — LootTable SO, weighted drops, pickup objects, EXP/Gold rewards
StatusEffectSystem.cs — timed buff/debuff, DoT/HoT, stacking, VFX hooks
WeaponVisualHandler.cs — weapon mesh trên hand bone, draw/sheathe transitions

Cách dùng: Giải nén zip, copy các file vào đúng folder trong project Unity. Xóa PlayerCombatController.cs và CombatInputHandler.cs (deprecated). Attach PlayerDamageHandler lên Player root. Xem CHANGELOG.md cho full setup guide.
Bước tiếp theo bạn muốn mình làm gì? UI system (HP bar, inventory screen, damage numbers), quest system, hay skill/ability system?Rpgmodular patchZIP DownloadRpgmodular deepanalysisDocument · MD DownloadDownload allVAT.zipzipcó nên làm enemy full bằng VAT ko? kiểu có logic rải theo bầy trên map giống toram, target lock chọn để đánh có skill đánh số lượng nhiều tính tầm, m nghĩ thử xem làm vầy sẽ optimize và còn làm đc occlusion culling nữaApr 13Đánh giá VAT cho enemy mob, khuyến nghị hybrid approach tối ưuĐánh giá VAT cho enemy mob, khuyến nghị hybrid approach tối ưuĐây là câu hỏi architecture rất hay. Để mình phân tích pack VAT của bạn kỹ trước.Chuẩn bị phân tích chi tiết về thiết kế hệ thống enemyChuẩn bị phân tích chi tiết về thiết kế hệ thống enemyTrả lời ngắn: CÓ, và đây là hướng đi đúng. Pack VAT của bạn đã có đủ tất cả thành phần cần thiết. Để mình phân tích tại sao và cách thiết kế.
  ::view-transition-group(*),
  ::view-transition-old(*),
  ::view-transition-new(*) {
    animation-duration: 0.25s;
    animation-timing-function: cubic-bezier(0.19, 1, 0.22, 1);
  }
VvisualizeVvisualize show_widgetPhân tích chi tiết
Tại sao VAT hoàn hảo cho Toram-style mobs
Pack VAT của bạn đã có đủ 3 thứ cần thiết: baker tool (bake SkinnedMesh → texture), runtime animator (crossfade giữa clips), và 3 shader variants (opaque, toon lit URP, ghost). Quan trọng nhất là shader Toon đã có cả 3 passes: ForwardLit + ShadowCaster + DepthOnly — nghĩa là shadow và depth pre-pass đều hoạt động, occlusion culling sẽ chạy được.
Trong Toram, enemy mob rải theo bầy 5-20 con cùng loại trên map. Mỗi con chỉ cần ~5-7 animation clips (idle, walk, attack1, attack2, hit, death, maybe block). Đây chính xác là use case mà VAT sinh ra để giải quyết: nhiều instance cùng mesh, ít animation variety, GPU instancing batch tất cả thành 1-2 draw calls.
Những thứ VAT làm được mà SkinnedMesh không
Occlusion culling — SkinnedMeshRenderer dùng dynamic bounds (bounds thay đổi theo animation mỗi frame), Unity phải recalculate bounds → khó cull chính xác. VAT dùng MeshRenderer với static baked bounds (Baker đã tính CalculateTotalLocalSpaceBounds cover toàn bộ animation range) → Unity cull chính xác, zero overhead.
AoE skill hit nhiều target — OverlapSphere check 50 simple colliders (box/capsule trên VAT enemy) nhanh hơn rất nhiều so với 50 SkinnedMesh colliders. Phần damage pipeline từ RPGModular patch vẫn dùng được nguyên.
Những thứ cần chấp nhận trade-off
Không có ragdoll — khi enemy chết, không thể ragdoll vì vertices bị bake. Nhưng Toram cũng không dùng ragdoll — death animation là đủ.
Normal không update theo animation — VAT_Toonlit.shader dùng normal gốc của mesh (v.normalOS), không decode từ texture. Với toon shading (cel-shaded) thì gần như không thấy sai, nhưng nếu muốn chính xác hơn thì cần bake thêm normal texture (Baker hiện tại chưa làm).
Không có IK / look-at — enemy không thể xoay đầu nhìn player, không foot IK. Acceptable cho mob thường.
Architecture đề xuất: Hybrid
Mob thường (Slime, Wolf, Goblin...) → VAT. Boss → giữ SkinnedMeshRenderer (cần animation phức tạp, phase transitions, ragdoll khi chết).
Để integrate VAT enemy với RPGModular combat system, cần 1 adapter component mình gọi là VAT_EnemyAdapter — nó wrap VAT_Animator và implement IAnimationController interface, để EnemyBase + EnemyAI + HitboxManager hoạt động bình thường mà không cần biết bên dưới là VAT hay Skinned.
Nếu bạn muốn, mình viết luôn VAT_EnemyAdapter + VAT_MobSpawner (spawn theo bầy trên map với density zones)?t nghĩ là nên vạch ra kiến trúc trước, nãy m viết kiến thúc phần mới t cũng k biết m  update thêm gì lên kiến trúc trước sửa lại các criticle bug rồi mới bắt đầu codeApr 13Orchestrated architecture-first strategy before implementing code solutionsOrchestrated architecture-first strategy before implementing code solutionsĐúng rồi, mình đi quá nhanh. Ngồi lại vạch kiến trúc toàn bộ trước — từ cái đã có, cái cần sửa, cái cần thêm, cách chúng nối với nhau — rồi mới động vào code.
  ::view-transition-group(*),
  ::view-transition-old(*),
  ::view-transition-new(*) {
    animation-duration: 0.25s;
    animation-timing-function: cubic-bezier(0.19, 1, 0.22, 1);
  }
VvisualizeVvisualize show_widgetBây giờ mình vẽ damage flow — chỗ nào đứt, fix gì, nối lại ra sao:
  ::view-transition-group(*),
  ::view-transition-old(*),
  ::view-transition-new(*) {
    animation-duration: 0.25s;
    animation-timing-function: cubic-bezier(0.19, 1, 0.22, 1);
  }
VvisualizeVvisualize show_widgetVà đây là dependency graph — component nào cần component nào, để biết thứ tự build:
  ::view-transition-group(*),
  ::view-transition-old(*),
  ::view-transition-new(*) {
    animation-duration: 0.25s;
    animation-timing-function: cubic-bezier(0.19, 1, 0.22, 1);
  }
VvisualizeVvisualize show_widgetTổng kết kiến trúc
3 quyết định cần bạn confirm trước khi code:
Decision #1: BillGameCore integration — RPGModular hiện tại hoàn toàn tách biệt. Nếu tích hợp thì enemy spawn dùng Bill.Pool, game events dùng Bill.Events, save dùng Bill.Save, timer dùng Bill.Timer — bớt duplicate code. Nếu tách thì RPGModular portable hơn. Mình recommend tích hợp vì bạn sở hữu cả 2 pack.
Decision #2: VAT boundary — Rule đề xuất: enemy có ≤8 clips + spawn ≥3 con cùng loại → VAT. Boss / enemy có ragdoll / nhiều hit zone / phase transition → Skinned. Cả hai variant đều dùng chung EnemyBase + EnemyAI, chỉ khác animation backend (AnimationController vs VAT_EnemyAdapter). Adapter implement IAnimationController nên code phía trên không cần biết.
Decision #3: Xóa code cũ — Move PlayerCombatController + CombatInputHandler vào _Deprecated/ ngay Phase 1. Giữ reference, không dùng nữa.
Build order sau khi confirm:
Phase 1 (FIX-1 → FIX-3 + CLEAN) — Fix damage flow. Sau phase này player đánh enemy gây damage, parry/dodge hoạt động. ~100 dòng sửa/thêm.
Phase 2 (VAT-1, VAT-2) — VAT enemy adapter + mob spawner. Sau phase này spawn được bầy enemy trên map. ~200 dòng.
Phase 3 (SYS-1 → SYS-6) — Các system mới, build song song được vì chúng mostly independent. EnemyAI cần Phase 1, Loot cần Inventory, còn lại build bất kỳ thứ tự nào.
Phase 4 — UI + polish.
Bạn confirm 3 decisions rồi mình bắt tay code Phase 1?architect chưa rõ ràng lắm nhỉApr 13Clarified architecture and sought user confirmationClarified architecture and sought user confirmationĐúng, mình đang ở mức overview — chưa đi sâu vào "component A gọi method gì của component B, data gì chảy qua". Để mình vạch lại thật chi tiết.
  ::view-transition-group(*),
  ::view-transition-old(*),
  ::view-transition-new(*) {
    animation-duration: 0.25s;
    animation-timing-function: cubic-bezier(0.19, 1, 0.22, 1);
  }
VvisualizeVvisualize show_widgetVà đây là VAT_EnemyAdapter — chỗ quan trọng nhất vì nó là cầu nối giữa VAT (GPU anim) và RPGModular combat:
  ::view-transition-group(*),
  ::view-transition-old(*),
  ::view-transition-new(*) {
    animation-duration: 0.25s;
    animation-timing-function: cubic-bezier(0.19, 1, 0.22, 1);
  }
VvisualizeVvisualize show_widgetGiờ architecture đã rõ hơn chưa? Tóm lại những điểm chốt:
Player side: Thêm đúng 1 component mới (PlayerDamageHandler) là nối lại toàn bộ damage flow. Component này implement 3 interfaces (IDamageDealer, IDamageable, ITargetLockable) — HitboxManager tìm được nó, enemy đánh trúng được player, parry/dodge hoạt động vì nó route qua CombatSM.CurrentState.HandleHit().
Enemy side: 2 variants dùng chung EnemyBase + EnemyAI. Khác nhau chỉ ở animation backend — AnimationController (Skinned, cho boss) vs VAT_EnemyAdapter (cho mob). Cả hai implement cùng IAnimationController, nên HitboxManager/EnemyAI không cần biết bên dưới là gì. VAT adapter track animation phase bằng elapsedTime / clipDuration thay vì Animator.normalizedTime.
VAT mob spawning: VAT_MobSpawner quản lý spawn zone — spawn theo bầy, respawn timer, distance-based activation (xa thì disable AI chỉ giữ render, xa nữa thì despawn hẳn).
3 flows chính (player đánh enemy, enemy đánh player, equip weapon) — mỗi flow mình đã trace chính xác từng method call, từng event fire, data gì chảy qua.
Bạn thấy cần bổ sung / thay đổi chỗ nào trong kiến trúc này không? Confirm rồi mình bắt đầu code Phase 1 (fix critical bugs).tuuyejet vời, critical bug ta đã fix hết rồi đúng kApr 13Xác nhận các bản sửa lỗi đã hoàn thành và chuẩn bị tiếp tụcXác nhận các bản sửa lỗi đã hoàn thành và chuẩn bị tiếp tụcĐúng, Phase 1 đã code xong trong file RPGModular_Patch.zip mình gửi trước đó. Cụ thể 3 critical fixes:
FIX-1 ✅ PlayerDamageHandler.cs — NEW, implement IDamageDealer + IDamageable + ITargetLockable, route TakeDamage qua CombatSM.CurrentState.HandleHit()
FIX-2 ✅ HitboxManager.cs — EDIT, thêm GetComponentInParent<IDamageDealer>() fallback + gọi NotifyDamageDealt() để fire OnDamageDealt
FIX-3 ✅ EnemyBase.cs — EDIT, thêm EnemyStatProvider, dùng DamagePipeline, fix PerformAttack() + thêm PerformDirectAttack()
CLEAN — PlayerCombatController + CombatInputHandler cần move vào _Deprecated/ (chưa làm trong zip nhưng chỉ là move file)
Tuy nhiên, có 1 chỗ cần review lại cho khớp với architecture vừa vạch:

EnemyBase.cs trong zip có public LootTable lootTable; trong EnemyData — nhưng class LootTable nằm trong file LootSystem.cs (Phase 3). Nếu import thứ tự Phase 1 trước mà chưa có Phase 3, sẽ compile error. Cần tách LootTable reference ra hoặc để nullable.

Ngoài ra code fix align đúng với architecture. Phase 2 (VAT integration) là bước tiếp — viết VAT_EnemyAdapter + VAT_MobSpawner. Bạn muốn mình bắt đầu code Phase 2 luôn không?# My project (1)

## Copied Files

  - Assets/Scripts/Enemy/EnemyType/BossEnemy.cs
  - Assets/Scripts/Enemy/EnemyType/MeleeEnemy.cs
  - Assets/Scripts/Enemy/EnemyType/SpeedEnemy.cs
  - Assets/Scripts/Enemy/EnemyBase.cs
  - Assets/Scripts/Enemy/EnemyStats.cs
  - Assets/Scripts/Enemy/EnemyType/RangeEnpastedrồi gud giờ ta cần làm hệ thống VAT kết hợp có thể tham khảo hệ thống enemy này chăng 
nh mà đừng làm giống y chang, ta cần vẽ ra đường architect cho game design và workflow tưởng tượng player sẽ chơi như nào đầy đủ xong t ok rồi mới code nhéApr 13Tổng hợp kiến trúc game và quy trình gameplay cho người chơiTổng hợp kiến trúc game và quy trình gameplay cho người chơiĐọc kĩ code EchoMage rồi. Rất tốt — mình thấy rõ pattern của bạn: VAT trực tiếp, object pooling, difficulty scaling riêng HP/Damage/Speed, boss phase system. Giờ mình sẽ thiết kế từ góc nhìn player chơi game như thế nào trước, rồi mới map ngược về code.
  ::view-transition-group(*),
  ::view-transition-old(*),
  ::view-transition-new(*) {
    animation-duration: 0.25s;
    animation-timing-function: cubic-bezier(0.19, 1, 0.22, 1);
  }
VvisualizeVvisualize show_widgetĐây là toàn bộ game design + architecture nhìn từ góc player experience. Mấy điểm quan trọng nhất cần bạn đọc kĩ:
Thay đổi lớn nhất so với kiến trúc trước: Sau khi đọc code EchoMage, mình thấy VAT_EnemyAdapter (wrap VAT → IAnimationController) là over-engineering. Code EchoMage đã chứng minh enemy gọi thẳng VAT_Animator.Play() / CrossFade() hoạt động hoàn hảo — kể cả boss với phase system phức tạp. Animation phase tracking bằng timer thủ công (WindupTime/RecoveryTime) đơn giản và đủ dùng. Nên mình bỏ VAT_EnemyAdapter khỏi architecture, tất cả enemy dùng VAT trực tiếp.
Combat đơn giản hóa: Bỏ block/parry/guard break (không phải Toram-style), thay bằng Skill System (4 skill slots, mana cost, cooldown). Auto-attack giữ nguyên. Dodge giữ nhưng đơn giản hơn. Đây là thay đổi lớn từ RPGModular "Dark Souls-lite" → "Toram-lite".
Enemy architecture: Giữ pattern EchoMage (abstract EnemyBase + subclass + TakeDamage(float) đơn giản) thay vì RPGModular pattern (DamageInfo/DamageResult phức tạp). Player side vẫn dùng DamagePipeline internally cho incoming damage, nhưng enemy không cần biết.
4 câu hỏi ở cuối cần bạn confirm trước khi code. Bạn đọc xong cho mình biết ý kiến?cái vụ enemy chase phải tính theo chỉ số, và enemy ko nên chase quá nhạy cảm giờ nguyên 1 đàn 50 con chase enemy mỗi con nhổ 1 bãi nước bọt là player chết cmnr đúng k, mỗi chase 1 con 2 con thôi, và chỉ số player thấp hơn mới chase ko thì chúng nó sẽ phải sợ chứ. viết theo kiểu document architect folder rồi setup kiểu này là đang đi chuẩn bài rồi đấyApr 13Synthesized game mechanics feedback for documentation architectureSynthesized game mechanics feedback for documentation architectureĐúng, điểm cực kì quan trọng — gameplay phải design cho "cảm giác" đúng. Để mình viết phần Aggro/Threat system vào architecture doc.
  ::view-transition-group(*),
  ::view-transition-old(*),
  ::view-transition-new(*) {
    animation-duration: 0.25s;
    animation-timing-function: cubic-bezier(0.19, 1, 0.22, 1);
  }
VvisualizeVvisualize show_widgetTóm tắt section mới
Core insight: PackManager quản lý tập trung — từng con enemy KHÔNG tự quyết định chase. PackManager nhìn tổng thể (player level vs enemy level, bao nhiêu con đang chase, bao nhiêu con đã chết) rồi ra lệnh cho từng con. Điều này giải quyết triệt để vấn đề "50 con lao vào cùng lúc".
5 mức threat perception dựa trên level gap: Terrified (chạy trốn) → Wary (né, chỉ phản đòn) → Normal (2-3 con chase) → Aggressive (4-5 con) → Bloodlust (cả bầy). Player tự nhiên cảm nhận được power difference mà không cần UI nào giải thích.
3 states mới cho enemy: Alert (biết player đâu nhưng chưa được chase — nervously quan sát), Flee (chạy trốn khi player quá mạnh), ReactiveDefend (chỉ phản đòn rồi rút).
PackManager evaluate chỉ 2-3 lần/giây — không phải mỗi frame. 50 pack trên map = 100-150 evaluations/giây, rất nhẹ.
Còn 2 câu hỏi mới (Q5: enemy level ở đâu, Q6: alert visual) cộng với 4 câu hỏi cũ (Q1-Q4 từ game design doc). Bạn trả lời những cái nào đã quyết được, cái nào chưa thì mình có thể recommend thêm.hợp lý luôn rồi architect xong rồi còn hệ thống nào cần architect nữa kApr 13Xác định Skill System là hệ thống duy nhất cần kiến trúcXác định Skill System là hệ thống duy nhất cần kiến trúcMình review lại toàn bộ — đây là checklist những gì đã architect và chưa:
  ::view-transition-group(*),
  ::view-transition-old(*),
  ::view-transition-new(*) {
    animation-duration: 0.25s;
    animation-timing-function: cubic-bezier(0.19, 1, 0.22, 1);
  }
VvisualizeVvisualize show_widgetCòn đúng 1 cái: Skill System. Phần còn lại hoặc đã xong hoặc đơn giản đủ để design khi code.
Skill System là cái player bấm nhiều nhất trong combat — 4 nút skill, mỗi nút có cooldown/mana cost/animation/hitbox riêng, skill tree unlock theo weapon type. Nếu không architect trước thì khi code sẽ phải refactor nhiều vì nó chạm vào CombatSM (SkillCastState), WeaponHandler (weapon-locked skills), HealthSystem (mana cost), CharacterStats (skill damage scaling), Input (skill buttons 1-4), và cả UI (cooldown indicators).
Bạn muốn mình architect Skill System luôn rồi chốt, hay nhảy vào code trước với auto-attack + dodge, skill để sau?t nghĩ là architect vụ skill system trước giờ viết chi tiết game design theo hướng hiện tại của toram để dễ hình dùng nèApr 13Architected comprehensive skill system design frameworkArchitected comprehensive skill system design frameworkĐã research xong Toram skill system. Rất chi tiết — active + passive skills, combo chaining, charge time, weapon restrictions, status effects từ skills. Để mình architect đầy đủ.
  ::view-transition-group(*),
  ::view-transition-old(*),
  ::view-transition-new(*) {
    animation-duration: 0.25s;
    animation-timing-function: cubic-bezier(0.19, 1, 0.22, 1);
  }
VvisualizeVvisualize show_widgetTóm tắt Skill System
Core insight lớn nhất: Trong Toram, auto-attack không chỉ là "đánh tự động khi chờ skill cooldown" — nó là nguồn hồi MP chính. MP không tự hồi (hoặc rất chậm). Player PHẢI auto-attack để có MP cast skill. Đây là cái tạo ra gameplay rhythm: auto (hồi MP) → skill burst (tiêu MP) → auto → skill burst. Nếu thiếu cái này, combat sẽ thành "spam skill vô não" hoặc "đứng đánh auto chán".
3 component runtime:

PlayerSkillBook — quản lý skill đã học, SP spending, passive bonus application
SkillBar — 4 active slots, weapon validation (đổi weapon → grayed out skill không compatible)
SkillCaster — execute flow: check gates → consume MP → switch CombatSM state → spawn hitbox/projectile → combo window

3 combat states mới: SkillChargeState (charge, cancelable bởi hit/dodge), SkillExecuteState (đang đánh, gây damage), ComboReadyState (combo window mở 0.5s, chờ player chain tiếp).
Combo system: Không phải auto-combo mà là player chủ động chain. Bấm skill liên tiếp trong combo window → bonus speed +20%, bonus damage +10% per chain sau skill thứ 2. Tạo ra skill expression — player giỏi chain combo tốt hơn player bấm random.
Giờ architecture đã đủ cho toàn bộ game rồi. Bạn review xong confirm thì mình bắt đầu code theo đúng thứ tự Phase 1 → 2 → 3.ổn áp đấy nh mà nghĩ ra hướng skill khác cho khác với nhánh của toram đi, chứ clone 100% thì làm game làm gì nữa chán lắm ideas là t muốn dạng của toram, vẫn như cũ thôi, nhưng tên kiểu khácKiếm dài kiếm ngắn 2h 1h katana, song kiếm, kiếm khiên, thương, kích, kĩ năng võ thuật (đấm tay chân), TAO (kĩ năng khí đạo), ma thuật, thợ rèn, thợ điều chế (kiểu alchemist giống toram) làm thuốc đồ để bán, tamer thợ săn pet, giống pokemon, bắt quái về bán hoặc nuôi mang đi đánh, cung,... nói chung là phát triển ra nhiều class mà có thể 1 nhân vật học nhiều skill đc điểm kĩ năng thì có giới hạn và level cũng giới hạn, ta sẽ un cap level theo contentApr 13Xác định các yếu tố độc đáo và phát triển khái niệmXác định các yếu tố độc đáo và phát triển khái niệmHay lắm — giờ mình thiết kế identity riêng cho game, không clone Toram. Mình sẽ giữ cơ chế (combo chain, MP rhythm, skill tree topology) nhưng đổi toàn bộ nhánh skill, tên gọi, và thêm hệ thống độc đáo (TAO, Tamer).
  ::view-transition-group(*),
  ::view-transition-old(*),
  ::view-transition-new(*) {
    animation-duration: 0.25s;
    animation-timing-function: cubic-bezier(0.19, 1, 0.22, 1);
  }
VvisualizeVvisualize show_widgetĐiểm khác biệt chính so với Toram
TAO (Đạo Thuật) + Chi gauge — đây là mechanic độc đáo nhất. Resource thứ 3 ngoài HP/MP. Chi tích khi combat (đánh, bị đánh, meditate), decay khi out of combat. TAO skills dùng Chi, weapon skills dùng MP. Player quản lý 2 resource song song tạo ra layer strategic mà Toram không có. Mọi build đều dùng TAO khác nhau — kiếm sĩ buff ATK, tank heal, mage thiền tích Chi giữa waves.
Nhẫn Đạo (Katana) Focus gauge — cơ chế riêng: đứng yên tích Focus, skill mạnh hơn theo Focus. Counter stance chờ bị đánh rồi phản. Playstyle "patience = power" hoàn toàn khác các weapon khác.
14 nhánh thay vì copy tên Toram — tên Hán-Việt/wuxia, mỗi nhánh có identity rõ ràng. Song Kiếm = crit machine multi-hit. Trọng Kiếm = super armor slow tank. Thủ Thuật = shield tank with taunt. Võ Thuật = stun lock CC.
Tamer = mini Pokemon — bắt quái, nuôi, mang đi đánh hoặc bán. Fuse với pet 30s (ultimate) = gain pet stats. Tạo social economy (pet marketplace).
SP economy buộc chọn build — 50 SP ở cap 50, max 1 skill tốn 21 SP → player chỉ "giỏi" được 2-3 skills, phải chọn trade-off. Respec tốn gold cho phép thử nghiệm.
Architecture impact: chỉ cần thêm Chi vào HealthSystem (resource thứ 4 cùng HP/MP/Stamina, decay mechanic), thêm chiCost vào SkillData, và Focus gauge cho Katana (simple float trên player, similar to Chi nhưng decay nhanh hơn). Không cần refactor gì lớn.
Bạn thấy hướng này ổn không? Có muốn đổi/thêm gì trước khi chốt architecture?có vẻ ok đấy, tạm thời như này đi sau ta sẽ phát triển lên thêm architect vụ skill này đi rồi còn tính ra chúng ta đã xong những hệ thống nào có architect nào viết ra để cb codeApr 13Chuẩn bị kết luận ngắn gọn và xúc tíchChuẩn bị kết luận ngắn gọn và xúc tích
  ::view-transition-group(*),
  ::view-transition-old(*),
  ::view-transition-new(*) {
    animation-duration: 0.25s;
    animation-timing-function: cubic-bezier(0.19, 1, 0.22, 1);
  }
VvisualizeVvisualize show_widgetTổng kết
Đã xong:

Phase 1 (critical fixes) — code written, trong RPGModular_Patch.zip
6 systems progression đã code (Inventory, Equipment, Level, Loot, StatusEffect, WeaponVisual)

Đã architect đầy đủ, sẵn sàng code:

Phase 2: Enemy + PackManager + Aggro system + VAT_MobSpawner (3 files mới)
Phase 3: Skill system 14 nhánh + Chi gauge + Combo chain (10 files mới)
Phase 4: Integration + wire tất cả lại (updates + damage popup)

Tổng còn ~2,800 dòng code chia 4 phases. Sau khi xong Phase 4, game có full gameplay loop: explore → combat (auto-attack + skill combo + dodge) → kill mobs (aggro thông minh, pack behavior) → loot + EXP → level up → equip → repeat.
Bạn confirm rồi mình bắt đầu code Phase 2 (Enemy + Pack system) trước?Có 1 thứ nữa rất quan trọng hỗ trợ cho ng chơi đa quốc gia là localizationApr 13Tích hợp localization vào quy trình xây dựng ứng dụngTích hợp localization vào quy trình xây dựng ứng dụngĐúng, localization là foundation — nếu không architect từ đầu thì sau phải refactor lại TẤT CẢ text trong game. Để mình thiết kế luôn.
  ::view-transition-group(*),
  ::view-transition-old(*),
  ::view-transition-new(*) {
    animation-duration: 0.25s;
    animation-timing-function: cubic-bezier(0.19, 1, 0.22, 1);
  }
VvisualizeVvisualize show_widgetImpact lên build order
Localization phải code trước mọi system có text — nghĩa là nó nhảy lên đầu Phase 2. Build order cập nhật:
Phase 1 ✅ Done — Critical bug fixes (PlayerDamageHandler, HitboxManager, EnemyBase)
Phase 2 (updated):

LocalizationService + Loc + LocalizedText + config (~220 lines) — code đầu tiên
Sửa tất cả SO: SkillData, ItemData, WeaponData, EnemyData → dùng nameKey/descKey thay vì text trực tiếp
PackManager + EnemyAI + VAT_MobSpawner (~700 lines)

Phase 3 — Skill system (10 files, ~910 lines) — SkillData đã dùng localization keys
Phase 4 — Integration + polish
Localization chỉ thêm ~220 dòng code nhưng ảnh hưởng toàn bộ cách các SO lưu text. Nếu không làm trước thì sau phải đổi skillName → nameKey trong hàng chục file — đau đầu hơn nhiều so với làm đúng từ đầu.
Bạn confirm thì mình bắt đầu code Phase 2 — Localization trước, rồi Enemy + Pack system?Giờ viết ra toàn bộ architect chúng ta đang có và chuẩn bị code cho inventory, skill, enemy, đại khái phân tích đầy đủ thành document chi tiết, hướng đi flow cách gọi open API... thiết kế chúng cực kì dynamic có thể sử dụng dễ dàng call gọi giống định hướng của BillCore ấyApr 13Orchestrated comprehensive architecture design for game systems integrationOrchestrated comprehensive architecture design for game systems integrationTóm tắt document
11 sections, cover toàn bộ game architecture:
Section 1 — API Design: Game.* static facade giống Bill.*. Developer gọi Game.Inv.AddItem(), Game.Skill.Cast(0), Game.Loc.Get("key") — 1 dòng, không cần biết component nằm đâu. PlayerCore hub component auto-find tất cả sub-systems trong Awake.
Section 2 — Localization: Full API (Loc.Get() + variable interpolation), key convention, JSON per language, LocalizedText auto-refresh component. Quy tắc: mọi SO dùng nameKey/descKey thay vì text.
Section 3-4 — Inventory + Equipment: Complete API với code examples. Events cho UI binding. Internal flow Equip() trace 6 bước (check → remove old → apply modifiers → weapon sync → remove from inv → fire event).
Section 5 — Skill System: Đây là section dài nhất. SkillData SO full field list (40+ fields). Cast flow trace chính xác từ input → gate checks → consume resource → combat state → damage calculation → AoE logic → combo window. Chi gauge API. MP recovery formula. Combo bonus math.
Section 6 — Enemy + Pack: EnemyBase abstract API + 9 states. PackManager threat calculation. VAT_MobSpawner config.
Section 7-8 — Level + StatusEffect: API + events + formulas.
Section 9 — Combat SM: Updated state list (bỏ Block/Parry, thêm SkillCharge/Execute/ComboReady). Full transition map.
Section 10 — File inventory: 30 files, ~2,280 lines, chia 4 phases.
Section 11 — Event map: MỌI event trong game — ai fire, ai subscribe. Đây là "wiring diagram" cho UI developer.
Document này là coding bible — mở lên đọc khi code bất kỳ system nào. Bạn review xong confirm thì mình bắt đầu code Phase 2A (Localization) rồi Phase 2B (Game facade)?

# RPGModular — Complete Architecture Document
### For Claude Code Implementation | v2.0

---

## 0. PROJECT OVERVIEW

Action RPG Toram-style. Thế giới mở chia zone, combat real-time lock-on, farm mob lấy EXP + loot.
4 Unity packages:

| Package | Role |
|---------|------|
| **BillGameCore v3** | Framework: ServiceLocator, EventBus, Pool, Audio, Save, Scene, Timer, UI, Network |
| **RPGModular** | Combat: stats, health, animation, weapons, locomotion, combat state machine, damage pipeline |
| **VAT** | Vertex Animation Texture: GPU-based animation for mass enemy rendering |
| **Bill Inspector** | 50+ inspector attributes: BillSlider, BillShowIf, BillBoxGroup, BillTableList, BillProgressBar... |

**RPGModular gốc nằm ở:** project Unity, folder RPGModular/
**Files MỚI nằm ở:** cùng folder structure RPGModular/ hoặc thêm subfolder mới

---

## 1. API DESIGN — Game.* Static Facade

Mọi system accessible qua static facade giống `Bill.*` pattern.

```csharp
// Usage examples:
Game.Stats.GetStat(StatType.STR);
Game.Health.Heal(50f);
Game.Health.CurrentChi;                    // Chi gauge (TAO system)
Game.Inv.AddItem(swordData, 1);
Game.Equip.Equip(swordData, EquipSlot.MainHand);
Game.Level.AddExp(200);
Game.Skill.Cast(0);                        // cast skill slot 0
Game.SkillBook.LearnOrUpgrade(hardHit);
Game.SkillBar.EquipSkill(hardHit, 0);
Game.Combo.CurrentComboCount;
Loc.Get("skill.blade.hard_hit.name");      // → "Trảm Kích" (vi) / "Hard Hit" (en)
Loc.Get("msg.damage", ("damage", "150"));  // → "Gây 150 sát thương"
Game.Status.Apply(poisonData);
Game.LockOn.CurrentTarget;
Game.Combat.CurrentStateType;
```

### Game.cs — Static accessor

```csharp
public static class Game
{
    private static PlayerCore _player;
    public static PlayerCore Player {
        get {
            if (_player == null) _player = Object.FindFirstObjectByType<PlayerCore>();
            return _player;
        }
    }

    public static CharacterStats     Stats     => Player?.Stats;
    public static HealthSystem       Health    => Player?.Health;
    public static Inventory          Inv       => Player?.Inventory;
    public static EquipmentSystem    Equip     => Player?.Equipment;
    public static LevelSystem        Level     => Player?.Level;
    public static PlayerSkillBook    SkillBook => Player?.SkillBook;
    public static SkillBar           SkillBar  => Player?.SkillBar;
    public static SkillCaster        Skill     => Player?.SkillCaster;
    public static ComboTracker       Combo     => Player?.Combo;
    public static StatusEffectSystem Status    => Player?.StatusEffects;
    public static CombatStateMachine Combat    => Player?.CombatSM;
    public static LockOnSystem       LockOn    => Player?.LockOn;
    public static WeaponHandler      Weapon    => Player?.Weapon;
    public static AutoAttackSystem   AutoAttack=> Player?.AutoAttack;
    public static LocalizationService Loc      => LocalizationService.Instance;

    public static void ClearCache() => _player = null;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void DomainReload() => _player = null;
}
```

### PlayerCore.cs — Hub component trên Player root GO

Auto-find tất cả sub-systems trong Awake via GetComponent<T>().
19 systems total. Dùng [BillReadOnly, BillShowInInspector] cho mỗi property.
Có [BillButton("Log All Systems")] debug button.

---

## 2. BILL INSPECTOR CONVENTIONS

Mọi ScriptableObject và MonoBehaviour component PHẢI dùng Bill Inspector attributes.

### Quy tắc bắt buộc:

```csharp
// Mọi SO/Component: BillTitle ở đầu
[BillTitle("Skill Data", "Định nghĩa 1 skill")]

// Group fields: BillBoxGroup cho related fields, BillTabGroup cho nhiều sections
[BillBoxGroup("Identity")]
[BillTabGroup("Config", "Type")]        // tab "Type" trong group "Config"
[BillTabGroup("Config", "Cost")]        // tab "Cost" trong group "Config"
[BillFoldoutGroup("Debug")]             // collapsible debug section

// Conditional visibility: BillShowIf/BillHideIf
[BillShowIf("category", SkillCategory.Active)]     // show khi category == Active
[BillShowIf("@health < 30")]                        // expression engine
[BillShowIf("@type == ItemType.Weapon")]

// Numeric: BillSlider + BillSuffix
[BillSlider(0, 100)]
[BillSuffix("HP")]
public float health;

// Visual: BillProgressBar cho HP/MP bars, BillPreviewField cho Sprite/Texture
[BillProgressBar(0, 100, ColorType.Red)]
[BillPreviewField]

// Lists/Tables: BillTableList
[BillTableList]
public SkillPrerequisite[] prerequisites;

// References: BillRequired + BillInlineEditor
[BillRequired("Cần enemy data")]
[BillInlineEditor]
public StatusEffectData appliedEffect;

// Read-only runtime: BillReadOnly + BillShowInInspector
[BillReadOnly, BillShowInInspector]
public int CurrentLevel { get; private set; }

// Buttons (editor only):
[BillButton("Heal to Full", ButtonSize.Medium)]
void DebugHeal() => health = maxHealth;

// Enum as toggle buttons:
[BillEnumToggleButtons]
public SkillCategory category;

// Labels: BillLabelText
[BillLabelText("Name Key (Loc)")]
public string nameKey;

// Info boxes:
[BillInfoBox("Chi cost cho TAO skills. 0 = không tốn Chi.", InfoType.Info)]
```

### Serialization cho Dictionary:

Khi cần Dictionary serialization → SO kế thừa BillSerializedScriptableObject.
Component kế thừa BillSerializedMonoBehaviour.
Dùng [BillDictionaryDrawer(KeyLabel = "Item", ValueLabel = "Count")].

---

## 3. LOCALIZATION SYSTEM

### Nguyên tắc: "Không có text hardcode"

Mọi text player nhìn thấy → localization key. SO lưu nameKey/descKey, KHÔNG lưu text trực tiếp.

### API

```csharp
Loc.Get("key")                                     // → localized string
Loc.Get("key", ("var", "value"), ("var2", "val2"))  // → with interpolation
Loc.SetLanguage("en")                               // → switch runtime, fire event
Loc.CurrentLanguage                                  // "vi"
```

### Key convention

```
{category}.{subcategory}.{id}.{field}

skill.blade.hard_hit.name     // skill names
skill.blade.hard_hit.desc     // skill descriptions
item.weapon.iron_sword.name   // item names
enemy.wolf.name               // enemy names
enemy.boss.dragon.phase2      // boss phase names
stat.str.name / stat.str.short // stat names
tree.blade.name               // skill tree names
ui.hud.hp / ui.hud.chi        // UI labels
msg.skill.no_mp               // system messages
msg.combat.crit               // combat messages
```

### SO field convention — TẤT CẢ ScriptableObject

```csharp
// ĐÚNG:
public string nameKey;   // "skill.blade.hard_hit"
public string descKey;   // "skill.blade.hard_hit.desc"
// Runtime: Loc.Get(skill.nameKey)

// SAI — KHÔNG BAO GIỜ:
public string skillName;     // "Trảm Kích" ← HARDCODE
public string description;   // "Chém mạnh..." ← HARDCODE
```

### Components

**LocalizationService** — singleton, DontDestroyOnLoad. Load JSON, parse key-value, fallback chain: current → en → show key raw.
**Loc** — static shortcut class. `Loc.Get("key")` = `LocalizationService.Instance.Get("key")`.
**LocalizedText** — MonoBehaviour trên UI Text/TMP. Auto-refresh khi OnLanguageChanged.
**LocalizationConfig** — SO: supported languages, font per lang, default/fallback lang.

### JSON files: Resources/Localization/{langCode}.json

Flat key-value: `{"skill.blade.hard_hit.name": "Trảm Kích", ...}`

Supported: vi (default), en (fallback), ja, zh-cn, ko, th.

---

## 4. DAMAGE FLOWS — Exact Method Chains

### Flow 1: Player đánh trúng Enemy

```
PlayerInputHandler.Update() → AttackInput = true
  ↓
CombatEngagedState.Tick() → detect AttackInput → ConsumeAttackInput()
  ↓
SwitchState(AttackingState)
  ↓
AttackingState.Enter()
  → WeaponHandler.GetNormalAttackAction(comboIndex) → AnimationActionData
  → HitboxManager.PrepareAttack(isHeavy)
  → AnimController.PlayAction(actionData)
  ↓
AnimController → normalizedTime → OnPhaseChanged(Active)
  → HitboxManager → mainHandHitbox.Activate() → collider ON
  ↓
DamageHitbox.OnTriggerEnter(enemyCollider)
  → GetComponent<IDamageable>() → EnemyBase
  → HitboxManager.OnHitboxHit()
    → IDamageDealer dealer = GetComponentInParent<IDamageDealer>() → PlayerDamageHandler
    → dealer.CalculateDamage(isHeavy) → DamageInfo
    → target.TakeDamage(rawDamage) → gọi EnemyBase.TakeDamage(float)
    → fire OnHitConfirmed
    → playerDamageHandler.NotifyDamageDealt() → fire OnDamageDealt
  ↓
EnemyBase.TakeDamage(float amount)
  → currentHP -= amount
  → VatAnimator.Play(hitClip)
  → HP ≤ 0 → Die() → fire OnDeath
    → LootDropper → Roll loot, grant EXP/Gold
    → PackManager.UnregisterEnemy()
```

### Flow 2: Enemy đánh trúng Player

```
EnemyAI.UpdateAttack() → in range + cooldown ready
  → EnemyBase.PerformAttack(target)
    → OverlapSphere/hitbox → IDamageable on player → PlayerDamageHandler
  ↓
PlayerDamageHandler.TakeDamage(float amount)
  → CombatSM.CurrentState.HandleHit(damageInfo)
    → DodgeState + isInvincible → return true → blocked!
    → Other states → return false → damage goes through
  → DamagePipeline.Calculate() → DamageResult
  → HealthSystem.ApplyDamage(result.FinalDamage)
  → HP ≤ 0 → OnDeath → DeadState
```

### Flow 3: Skill cast

```
PlayerInputHandler → skillInput[0] = true
  ↓
CombatState.Tick() → detect skillInput[0]
  → Game.Skill.Cast(0)
  ↓
SkillCaster.Cast(slotIndex=0)
  → skill = SkillBar.GetSkill(0)
  → level = SkillBook.GetSkillLevel(skill)
  → GATE: CanUseSkill? HasMana? HasChi? weapon ok?
  → CONSUME: TryConsumeMana(), TryConsumeChi(), StartCooldown()
  → castTime > 0 → SkillChargeState → wait → SkillExecuteState
  → castTime == 0 → SkillExecuteState directly
  ↓
SkillExecuteState.Enter()
  → VatAnimator.Play(skill.vatAnimClip)
  → At 40% animation: CalculateSkillDamage() → ExecuteSkillHit()
    → By targetType:
      SingleTarget → LockOn.CurrentTarget.TakeDamage(perHit) × hitCount
      AoE_Circle  → OverlapSphere(aoeRadius) → each.TakeDamage
      AoE_Cone    → OverlapSphere + angle filter → each.TakeDamage
      Projectile  → Pool.Spawn(projectilePrefab).Initialize(damage)
      Self/Buff   → StatusEffectSystem.Apply(selfBuff)
  → Apply status effect (chance-based)
  → Wait animationDuration
  ↓
ComboReadyState (window open)
  → Player bấm skill khác → chain combo (SkillExecuteState tiếp)
  → Player bấm dodge → DodgeState (break combo)
  → Window hết → CombatEngagedState (auto-attack resume)
```

### Skill damage formula

```
power = skill.basePower + skill.powerPerLevel * (level - 1)
statDmg = Physical? Stats.PhysicalAttack : Stats.MagicAttack
scaling = Stats.GetStat(skill.primaryScalingStat) * skill.scalingRatio
weaponATK = Weapon.MainHandWeapon?.BaseDamage ?? 5
comboBonus = Combo.GetComboDamageBonus()   // 1.0 + (comboCount-2)*0.1 if combo 3+
rawDamage = (statDmg + weaponATK + scaling) * (power / 100) * comboBonus
perHitDamage = rawDamage / hitCount
```

---

## 5. COMBAT STATE MACHINE — Updated States

### Bỏ: BlockingState, ParrySuccessState, GuardBreakState
### Giữ: CombatIdleState, CombatEngagedState, AttackingState, DodgeState, HitStunState, DeadState
### Thêm: SkillChargeState, SkillExecuteState, ComboReadyState

### Transition map

```
Idle ←→ Engaged (lock-on toggle)
Engaged → Attacking (auto-attack)
Engaged → SkillChargeState (charge skill, castTime > 0)
Engaged → SkillExecuteState (instant skill)
Engaged → DodgeState (dodge input)
Attacking → Engaged (combo done)
SkillChargeState → SkillExecuteState (charge complete)
SkillChargeState → HitStunState (interrupted, unless superArmor)
SkillChargeState → DodgeState (cancel charge)
SkillExecuteState → ComboReadyState (skill done)
SkillExecuteState → HitStunState (if canBeInterrupted && hit)
ComboReadyState → SkillExecuteState (chain next skill)
ComboReadyState → Engaged (window expired)
ComboReadyState → DodgeState (cancel combo)
HitStunState → Engaged (stun done)
Any → DeadState (HP ≤ 0)
```

---

## 6. SKILL SYSTEM — 14 Nhánh Võ Học

### 6.1 Overview

1 nhân vật học được TẤT CẢ nhánh. Giới hạn bởi SP (1/level, cap content-gated).

**Weapon trees (8):**
- Kiếm Pháp (Blade) — 1H Sword, STR+DEX, balanced combo
- Trọng Kiếm Đạo (GreatSword) — 2H Sword, STR, slow heavy hits, super armor
- Nhẫn Đạo (Katana) — Katana, DEX, Focus gauge, counter
- Song Kiếm Thuật (DualSword) — Dual, AGI+STR, multi-hit crit machine
- Thủ Thuật (Guardian) — Sword+Shield, VIT+STR, tank, taunt, block
- Thương Pháp (Spear) — Spear, STR+AGI, mid-range, line AoE
- Kích Pháp (Halberd) — Halberd, STR+DEX, wide sweep AoE, ASPD buff
- Xạ Thuật (Archery) — Bow, DEX, ranged, charge shots, arrow rain

**Shared (3):**
- Võ Thuật (Martial) — Knuckle/Barehand, AGI+STR, fastest, stun lock
- Đạo Thuật (TAO) — Any weapon, INT+VIT, Chi-based buff/heal/blast (UNIQUE: Chi gauge)
- Ma Thuật (Sorcery) — Staff/MagicDevice, INT, cast time AoE, elements

**Life (3):**
- Luyện Khí Sư (Blacksmith) — craft weapons/armor
- Điều Chế Sư (Alchemist) — potions, bombs, element oils
- Ngự Thú Sư (Tamer) — capture/raise/fight with pets

**Universal (1):**
- Sinh Tồn (Survival) — passives: HP%, resist, dodge extension

### 6.2 SP Economy

```
1 SP per level. Level cap = content-gated (50 launch, 60/70/80 updates).
Skill level cost (escalating): {1,1,1,2,2,2,3,3,3,3} = 21 SP to max 1 skill.
At cap 50: 50 SP total → max 2 skills (42 SP) + few passives (8 SP).
Player MUST choose build. Respec costs gold.
```

### 6.3 Chi Gauge (TAO — Đạo Thuật unique mechanic)

```
Chi = 3rd resource (HP, MP, Chi). TAO skills cost Chi instead of/alongside MP.
Chi max = 100 + VIT * 5.
Chi gain: +5 per hit dealt, +10 per hit received, +25/s Meditation.
Chi decay: -5/s out of combat (>5s no combat).
Gameplay: MP for weapon skills, Chi for TAO skills. Two-resource management.
```

### 6.4 Combo System

```
Auto-attack hồi MP: each hit = 50 + LUK*2 MP recovered.
Skill combo: use skill → combo window 0.5s → use next skill → chain.
Combo bonus: animation speed +20%, damage +10% per chain after 2nd skill.
Rhythm: auto(hồi MP) → skill burst(tiêu MP) → auto → skill burst.
```

### 6.5 SkillData SO fields (full list)

```
Identity: skillID, nameKey, descKey, icon, treeType
Type: category(Active/Passive), targetType(Single/Circle/Cone/Line/Projectile/Self), scaleType(Phys/Mag), requiredWeapons[]
Cost: baseMPCost, baseChiCost, castTime, cooldown
Damage: basePower, powerPerLevel, hitCount, primaryScalingStat, scalingRatio, hasSuperArmor
AoE: aoeRadius, coneAngle, range
Effects: appliedEffect(SO), effectChance, selfBuff(SO), buffDuration
Passive: passiveBonuses[] (StatBonus per level)
Tree: tier(1-5), prerequisites[], maxLevel, spCostPerLevel[]
Visual: vatAnimClip, animationDuration, comboWindowAfter, hitVFXId, castVFXId, projectilePrefabId, canBeInterrupted
```

---

## 7. ENEMY + PACK SYSTEM

### 7.1 Architecture

Tất cả enemy dùng VAT. Không có Skinned variant.
Pattern từ EchoMage: abstract EnemyBase + subclass + TakeDamage(float) đơn giản.

### 7.2 Threat Perception (level-based)

```
gap = playerLevel - enemyLevel

Terrified  (gap >= +10): enemy chạy trốn, maxChasers = 0
Wary       (gap +5→+9):  né tránh, chỉ phản đòn khi bị đánh, maxChasers = 0
Normal     (gap -2→+4):  2-3 con chase (base behavior)
Aggressive (gap -3→-7):  4-5 con chase, detection rộng hơn
Bloodlust  (gap <= -8):  cả bầy chase, death zone
```

### 7.3 PackManager — Quản lý bầy tập trung

Nằm trên SpawnZone GO, KHÔNG trên từng enemy. Evaluate 2-3 lần/giây.

```
Config: baseChasers(2), chaserRefillDelay(2s), packAggroRadius(15m),
        packLeashRadius(25m), deaggroTime(8s)

Logic mỗi evaluate:
  → Tính threat từ level gap
  → Terrified → CommandAll(Flee)
  → Wary → CommandAll(Alert), reactive only
  → Normal/Aggressive/Bloodlust → ManageChasers():
    - Clean dead chasers
    - Quá nhiều → thu hồi con xa nhất → Alert
    - Thiếu → chờ refillDelay → thêm con gần nhất → Chase
    - Còn lại → Alert (watch, không chase)
```

### 7.4 Enemy AI — 9 States

```
Idle → Patrol → Alert → Chase → Attack → Retreat → Flee → ReactiveDefend → Dead

EnemyAI nhận lệnh từ PackManager:
  CommandState(EnemyAIState.Chase)  → đuổi player
  CommandState(EnemyAIState.Alert)  → nervously watch, không đuổi
  CommandState(EnemyAIState.Flee)   → chạy ngược hướng player
  SetTarget(Transform)              → biết player ở đâu

Alert state: enemy quay nhìn player, di chuyển nervously, nhưng KHÔNG chase.
Visual cue cho player biết "bọn này đang watch tao".
```

### 7.5 VAT_MobSpawner

```
Config: vatEnemyPrefab, packSize(5), enemyLevel(10), spawnRadius(10m),
        activationRange(50m), despawnRange(80m), respawnDelay(30s)

Logic:
  Player enter activationRange → spawn pack
  Player leave despawnRange → despawn all
  Enemy chết → count-- → khi 0 alive → wait respawnDelay → spawn mới
  Distance activation: gần = AI active, xa = chỉ render idle, xa nữa = despawn
```

---

## 8. INVENTORY + EQUIPMENT

### Inventory API

```csharp
Game.Inv.AddItem(itemData, quantity)    → int overflow
Game.Inv.RemoveItem(itemData, quantity) → int removed
Game.Inv.HasItem(itemData, count)       → bool
Game.Inv.GetItemCount(itemData)         → int
Game.Inv.GetSlot(index)                 → ItemStack {Data, Quantity}
Game.Inv.UseItem(slotIndex)             → bool (consumable)
Game.Inv.SwapSlots(a, b)
Game.Inv.AddGold(amount) / SpendGold(amount) / Gold
// Events: OnSlotChanged, OnItemAdded, OnItemRemoved, OnGoldChanged, OnInventoryFull
```

### Equipment API

```csharp
Game.Equip.Equip(itemData, EquipSlot.MainHand)   → ItemData oldItem
Game.Equip.Unequip(EquipSlot.Head)                → ItemData removed
Game.Equip.GetEquipped(EquipSlot.MainHand)        → ItemData
// Events: OnEquipped, OnUnequipped
// Internal: auto apply/remove StatModifiers, wire WeaponHandler for MainHand/OffHand
```

### 8 Equipment slots

```
Head, Body, Legs, Feet, MainHand, OffHand, Accessory1, Accessory2
```

### ItemData SO fields

```
Identity: itemID, nameKey, descKey, icon, type(Weapon/Armor/Consumable/Material/QuestItem/Accessory), rarity(Common→Legendary)
Stack: maxStack(99 material, 1 equipment)
Economy: sellPrice
Equipment (conditional): isEquippable, defaultSlot, equipBonuses[], requirements[]
Weapon (conditional): weaponType, baseDamage, attackSpeed
Consumable (conditional): healAmount, manaAmount, chiAmount, appliedBuff
```

---

## 9. LEVEL SYSTEM

```csharp
Game.Level.AddExp(200)
Game.Level.Level / CurrentExp / ExpToNextLevel / ExpProgress(0-1)
Game.Level.UnspentStatPoints
Game.Level.SpendStatPoint(StatType.STR)
Game.Level.MaxLevel   // content-gated (50, 60, 70...)

// Events: OnLevelUp(int), OnExpGained(float, float), OnStatPointSpent(StatType)
// 5 stat points per level. 1 SP per level for skills.
// EXP formula: floor(100 * level^1.5)
```

---

## 10. STATUS EFFECT SYSTEM

```csharp
Game.Status.Apply(effectData)
Game.Status.Apply(effectData, stacks: 3)
Game.Status.RemoveEffect(effectData)
Game.Status.HasEffect(effectData)

// Events: OnEffectApplied, OnEffectRemoved, OnEffectTick
// Timed buffs/debuffs, DoT/HoT, stat modifiers, 4 stack behaviors
```

---

## 11. COMPLETE EVENT MAP

```
EVENT                              FIRED BY              SUBSCRIBED BY
──────────────────────────────────────────────────────────────────────────
HealthSystem
  OnResourceChanged(type,cur,max)  HealthSystem          UI HUD bars
  OnDamageTaken(float)             HealthSystem          Camera shake, SFX
  OnDeath                          HealthSystem          CombatSM, Controller
  OnHealReceived(float)            HealthSystem          UI heal popup

PlayerDamageHandler
  OnDamageDealt(target, result)    PlayerDamageHandler   UI damage numbers
  OnDamageTaken(DamageResult)      PlayerDamageHandler   UI damage popup

CharacterStats
  OnStatChanged(type, old, new)    CharacterStats        UI stat screen

LevelSystem
  OnLevelUp(int)                   LevelSystem           UI notification, VFX
  OnExpGained(float, float)        LevelSystem           UI exp bar
  OnStatPointSpent(StatType)       LevelSystem           UI stat screen

Inventory
  OnSlotChanged(int, ItemStack)    Inventory             UI inventory grid
  OnItemAdded(ItemData, int)       Inventory             UI pickup message
  OnItemRemoved(ItemData, int)     Inventory             UI
  OnGoldChanged(int)               Inventory             UI gold display
  OnInventoryFull                  Inventory             UI warning

EquipmentSystem
  OnEquipped(EquipSlot, ItemData)  EquipmentSystem       UI equipment screen
  OnUnequipped(EquipSlot, ItemData) EquipmentSystem      UI equipment screen

WeaponHandler
  OnWeaponChanged(IWeapon, slot)   WeaponHandler         WeaponVisualHandler, SkillBar

LockOnSystem
  OnTargetLocked(ITargetLockable)  LockOnSystem          PlayerController, Camera, UI
  OnTargetLost                     LockOnSystem          PlayerController, Camera, UI

PlayerSkillBook
  OnSkillLearned(SkillData, int)   PlayerSkillBook       UI skill tree
  OnSkillPointsChanged(int)        PlayerSkillBook       UI SP counter

SkillBar
  OnSkillBarChanged(int, SkillData) SkillBar             UI hotbar
  OnCooldownUpdate(int, float)     SkillBar              UI cooldown indicator

SkillCaster
  OnSkillCastStart(SkillData)      SkillCaster           UI cast bar, SFX
  OnSkillCastComplete(SkillData)   SkillCaster           VFX, SFX
  OnSkillCastInterrupted(SkillData) SkillCaster          UI warning

ComboTracker
  OnComboStart                     ComboTracker          UI combo display
  OnComboCountChanged(int)         ComboTracker          UI combo counter
  OnComboEnd                       ComboTracker          UI hide combo

StatusEffectSystem
  OnEffectApplied(ActiveEffect)    StatusEffectSystem    UI buff icons
  OnEffectRemoved(ActiveEffect)    StatusEffectSystem    UI buff icons

EnemyBase
  OnDeath                          EnemyBase             LootDropper, PackManager, LockOn
  OnHPChanged(float)               EnemyBase             UI enemy HP bar
  OnDamageTaken                    EnemyBase             EnemyAI (aggro)

PackManager
  OnThreatChanged(ThreatLevel)     PackManager           UI zone danger

LocalizationService
  OnLanguageChanged(string)        LocalizationService   ALL LocalizedText components
```

---

## 12. FILE INVENTORY — What exists, what to create, what to update

### A. FILES ALREADY CODED (in RPGModular_Patch.zip — Phase 1)
These exist and work but need Bill Inspector attribute upgrade:

```
EXIST  Core/Combat/PlayerDamageHandler.cs      (~150 lines) — NEEDS Bill Inspector attributes
EXIST  Core/Combat/Hitbox/HitboxManager.cs     (fixed) — keep as-is
EXIST  Core/Combat/EnemyBase.cs                (fixed) — NEEDS Bill Inspector + Command methods
EXIST  Core/AI/EnemyAI.cs                      (v1 simple) — REPLACED by v2 below
EXIST  Core/Inventory/Inventory.cs             (~200 lines) — NEEDS Bill Inspector attributes
EXIST  Core/Inventory/EquipmentSystem.cs       (~150 lines) — NEEDS Bill Inspector attributes
EXIST  Core/LevelSystem/LevelSystem.cs         (~120 lines) — NEEDS Bill Inspector attributes
EXIST  Core/Loot/LootSystem.cs                 (~180 lines) — NEEDS Bill Inspector + wire EXP/Gold
EXIST  Core/StatusEffect/StatusEffectSystem.cs (~200 lines) — NEEDS Bill Inspector attributes
EXIST  Weapons/WeaponVisualHandler.cs          (~100 lines) — keep as-is
EXIST  Data/ItemData.cs                        (v1) — REPLACED by v2 below
EXIST  Data/ArmorData.cs                       (~50 lines) — NEEDS Bill Inspector attributes
EXIST  Interfaces/IItem.cs                     (~80 lines) — keep as-is
```

### B. FILES ALREADY CODED (in RPGModular_Phase2.zip — Phase 2+3, with Bill Inspector)

```
DONE   Core/Game.cs                                  (~50 lines)
DONE   Core/Player/PlayerCore.cs                     (~70 lines) [Bill Inspector ✓]
DONE   Core/Localization/LocalizationSystem.cs       (~220 lines) [Bill Inspector ✓]
DONE   Core/Skill/SkillSystem.cs                     (~450 lines) [Bill Inspector ✓]
         (contains: PlayerSkillBook, SkillBar, SkillCaster, ComboTracker)
DONE   Core/Combat/States/SkillStates.cs             (~180 lines)
         (contains: SkillChargeState, SkillExecuteState, ComboReadyState)
DONE   Core/AI/PackManager.cs                        (~280 lines) [Bill Inspector ✓]
DONE   Core/AI/EnemyAI.cs (v2 — 9 states)           (~320 lines) [Bill Inspector ✓]
DONE   Core/AI/VAT_MobSpawner.cs                     (~220 lines) [Bill Inspector ✓]
DONE   Data/SkillData.cs                             (~300 lines) [Bill Inspector ✓]
DONE   Data/ItemData.cs (v2 + SkillTreeData)         (~220 lines) [Bill Inspector ✓]
DONE   Enums/SkillEnums.cs                           (~70 lines)
DONE   Resources/Localization/vi.json                (~120 lines)
DONE   Resources/Localization/en.json                (~120 lines)
```

### C. FILES THAT NEED UPDATES (modify existing RPGModular files)

```
UPDATE  Core/Health/HealthSystem.cs
        ADD: Chi as 4th resource (float currentChi, maxChi)
        ADD: HasChi(float), TryConsumeChi(float), ModifyChi(float)
        ADD: Chi decay logic (-5/s out of combat)
        ADD: Chi gain hooks (call ModifyChi from external)
        ADD: Bill Inspector attributes
        ~60 lines added

UPDATE  Core/Combat/AutoAttackSystem.cs
        ADD: After each hit landed on enemy:
             float mpRecovery = 50f + Game.Stats.GetStat(StatType.LUK) * 2f;
             Game.Health.ModifyResource(ResourceType.Mana, mpRecovery);
        ADD: After each hit landed:
             Game.Health.ModifyChi(5f);  // +5 Chi per hit dealt
        ~10 lines added

UPDATE  Core/Combat/StateMachine/CombatStateMachine.cs
        ADD: Register SkillChargeState, SkillExecuteState, ComboReadyState
        ADD: Helper methods: SwitchToEngaged(), SwitchToDodge()
        ADD: Reference to PlayerInputHandler (for skill input check)
        ~15 lines added

UPDATE  Core/Combat/StateMachine/CombatStates.cs
        REMOVE: BlockingState class (~40 lines)
        REMOVE: ParrySuccessState class (~30 lines)
        REMOVE: GuardBreakState class (~30 lines)
        ADD: In CombatEngagedState.Tick(): check skill input 0-3 → Game.Skill.Cast()
        Net: -100 lines

UPDATE  Core/Player/PlayerController.cs
        ADD: Wire references to SkillCaster, ComboTracker, WeaponVisualHandler
        ADD: On mode transition (Exploration→Combat): WeaponVisual.DrawWeapon()
        ADD: On mode transition (Combat→Exploration): WeaponVisual.SheatheWeapon()
        ~30 lines added

UPDATE  Input/PlayerInputHandler.cs
        ADD: bool[] skillInputs = new bool[4]   // skill button 1-4
        ADD: bool GetSkillInput(int slot)
        ADD: void ConsumeSkillInput(int slot)
        ADD: Mapping to keyboard keys (1,2,3,4 or configurable)
        ~20 lines added

UPDATE  Core/Combat/EnemyBase.cs (already fixed in Phase 1, need more)
        ADD: CommandChase(Transform), CommandAlert(), CommandFlee(), CommandRetreat()
        ADD: These methods change internal state and pass to EnemyAI
        ADD: event Action OnDeath (if not already)
        ADD: Bill Inspector attributes
        ~50 lines added

UPDATE  Core/Loot/LootSystem.cs
        ADD: On enemy death → Game.Level.AddExp(enemyExpReward)
        ADD: On enemy death → Game.Inv.AddGold(enemyGoldReward)
        ~15 lines added
```

### D. FILES THAT NEED CREATING (not yet written)

```
NEW     UI/DamageNumberPopup.cs
        Floating damage text. Pool-based. Crit = bigger + yellow.
        ~80 lines
```

### E. BILL INSPECTOR UPGRADE needed for Phase 1 files

These files have correct logic but plain inspector. Add attributes:

```
UPGRADE  Inventory.cs — [BillTitle], [BillSlider] maxSlots, [BillReadOnly] Gold, [BillButton] debug
UPGRADE  EquipmentSystem.cs — [BillBoxGroup] per slot, [BillShowIf], [BillReadOnly] equipped items
UPGRADE  LevelSystem.cs — [BillProgressBar] EXP, [BillSlider] maxLevel, [BillReadOnly] stats
UPGRADE  StatusEffectSystem.cs — [BillTableList] active effects, [BillFoldoutGroup]
UPGRADE  PlayerDamageHandler.cs — [BillTitle], [BillReadOnly] last damage taken
UPGRADE  EnemyBase.cs — [BillTitle], [BillProgressBar] HP, [BillSlider] config
UPGRADE  ArmorData.cs — [BillBoxGroup], [BillShowIf] conditional, match ItemData v2 pattern
```

---

## 13. EXISTING RPGModular FILES (original, for reference)

These are the files that came with RPGModular package. Do NOT rewrite from scratch — UPDATE them.

```
RPGModular/
├── Camera/CameraController.cs           ← keep as-is
├── Core/
│   ├── Animation/AnimationController.cs ← keep as-is (IAnimationController impl)
│   ├── Combat/
│   │   ├── AutoAttackSystem.cs          ← UPDATE (+MP recovery, +Chi gain)
│   │   ├── CombatLocomotion.cs          ← keep as-is
│   │   ├── DamagePipeline.cs            ← keep as-is
│   │   ├── EnemyBase.cs                 ← UPDATE (+Commands, +BillInspector)
│   │   ├── Hitbox/HitboxManager.cs      ← already fixed, keep
│   │   ├── LockOnSystem.cs              ← keep as-is
│   │   ├── PlayerCombatController.cs    ← MOVE to _Deprecated/
│   │   └── StateMachine/
│   │       ├── CombatState.cs           ← keep as-is (base class)
│   │       ├── CombatStateMachine.cs    ← UPDATE (+skill states, +helpers)
│   │       └── CombatStates.cs          ← UPDATE (-Block/Parry, +skill input check)
│   ├── Health/HealthSystem.cs           ← UPDATE (+Chi resource)
│   ├── Locomotion/
│   │   ├── LocomotionState.cs           ← keep as-is
│   │   ├── LocomotionStateMachine.cs    ← keep as-is
│   │   └── LocomotionStates.cs          ← keep as-is
│   ├── Player/PlayerController.cs       ← UPDATE (+wire new systems)
│   └── Stats/CharacterStats.cs          ← keep as-is
├── Data/WeaponData.cs                   ← keep as-is
├── Input/
│   ├── CombatInputHandler.cs            ← MOVE to _Deprecated/
│   └── PlayerInputHandler.cs            ← UPDATE (+skill inputs)
├── Interfaces/
│   ├── IAnimationController.cs          ← keep as-is
│   ├── ICombat.cs                       ← keep as-is
│   ├── IStatProvider.cs                 ← keep as-is
│   └── IWeapon.cs                       ← keep as-is
└── Weapons/WeaponHandler.cs             ← keep as-is
```

---

## 14. BUILD ORDER

```
Step 1: Localization (FIRST — all SOs depend on it)
        → LocalizationService, Loc, LocalizedText, Config, vi.json, en.json

Step 2: Game.cs + PlayerCore.cs facade

Step 3: Bill Inspector upgrade for Phase 1 files
        → Add attributes to Inventory, Equipment, Level, Loot, Status, etc.

Step 4: Update RPGModular files
        → HealthSystem +Chi
        → AutoAttackSystem +MP/Chi recovery
        → CombatStateMachine +skill states
        → CombatStates -Block/Parry +skill input
        → PlayerController +wire
        → PlayerInputHandler +skill inputs
        → EnemyBase +Commands
        → LootSystem +EXP/Gold wire

Step 5: Enemy + Pack system
        → PackManager, EnemyAI v2, VAT_MobSpawner

Step 6: Skill system
        → SkillData, SkillTreeData, PlayerSkillBook, SkillBar, SkillCaster, ComboTracker
        → SkillChargeState, SkillExecuteState, ComboReadyState

Step 7: Integration + Polish
        → DamageNumberPopup
        → Test full gameplay loop
```

---

## 15. GAMEPLAY LOOP — Player experience

```
Town hub     → NPC shop, equip gear, allocate stat points, learn skills
Enter field  → Portal → load map, SpawnZones with VAT mob packs
Approach mob → Pack sees player → PackManager evaluates threat
Combat       → Auto-attack (hồi MP) → Skill burst (tiêu MP/Chi) → Dodge
               Combo chain: skill→skill→skill (bonus speed/damage)
               Only 2-3 mobs chase at a time (PackManager controls)
Kill mob     → Death anim → EXP popup → gold → loot drop
Level up     → 5 stat points + 1 SP → allocate → stronger
Boss zone    → Boss spawn (VAT, phase system, combo chain) → rare loot
Return town  → Sell, equip, craft, skill tree → repeat harder zone
```

---

*End of Architecture Document v2.0*
*Feed this to Claude Code. It has everything needed to implement the full game.*