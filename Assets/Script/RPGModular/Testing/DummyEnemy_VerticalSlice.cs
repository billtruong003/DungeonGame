using UnityEngine;
using UnityEngine.AI;

namespace RPGModular.Testing
{
    /// <summary>
    /// Dummy Enemy cho Vertical Slice test.
    /// Gắn script này lên 1 GameObject trong scene, chạy Play, đánh thử.
    ///
    /// SETUP NHANH:
    /// 1. Tạo GameObject "DummyEnemy" trong scene
    /// 2. Gắn script này + NavMeshAgent + CapsuleCollider
    /// 3. Tag = "Enemy", Layer = "Enemy"
    /// 4. Tạo EnemyData asset: Right-click > Create > Game > Enemy Data
    /// 5. Kéo EnemyData vào field "data" trong Inspector
    /// 6. Bake NavMesh (Window > AI > Navigation > Bake)
    /// 7. Play & test combat
    ///
    /// MODES:
    /// - Standing Dummy: Đứng yên, chịu đòn, không đánh lại (test damage output)
    /// - Passive AI: Đi patrol, chạy khi bị đánh, không đánh lại (test lock-on, chase)
    /// - Aggressive AI: Đầy đủ AI, đánh player khi đến gần (test full combat loop)
    /// - Boss Test: HP cao, damage cao, attack pattern phức tạp (test endurance)
    /// </summary>
    [RequireComponent(typeof(CapsuleCollider))]
    public class DummyEnemy_VerticalSlice : EnemyBase
    {
        public enum DummyMode
        {
            StandingDummy,
            PassiveAI,
            AggressiveAI,
            BossTest
        }

        [Header("=== DUMMY CONFIG ===")]
        [SerializeField] private DummyMode mode = DummyMode.AggressiveAI;
        [SerializeField] private bool autoRespawn = true;
        [SerializeField] private float respawnDelay = 3f;
        [SerializeField] private bool showDamageLog = true;
        [SerializeField] private bool infiniteHP = false;

        [Header("=== VISUAL FEEDBACK (No Model Needed) ===")]
        [SerializeField] private Color normalColor = Color.red;
        [SerializeField] private Color hitColor = Color.white;
        [SerializeField] private Color deadColor = Color.gray;
        [SerializeField] private float hitFlashDuration = 0.15f;

        [Header("=== BOSS MODE OVERRIDES ===")]
        [SerializeField] private float bossHP = 5000f;
        [SerializeField] private float bossDamage = 50f;
        [SerializeField] private float bossAttackCooldown = 1.5f;

        [Header("=== DEBUG INFO (Read Only) ===")]
        [SerializeField] private int totalHitsTaken;
        [SerializeField] private float totalDamageReceived;
        [SerializeField] private float dpsTracker;
        [SerializeField] private string lastSkillHit;

        private NavMeshAgent agent;
        private Renderer meshRenderer;
        private MaterialPropertyBlock propBlock;
        private EnemyAI enemyAI;
        private Vector3 spawnPosition;
        private Quaternion spawnRotation;
        private float hitFlashTimer;
        private float dpsTimer;
        private float dpsDamageWindow;
        private float lastHP;

        protected override void Awake()
        {
            base.Awake();

            agent = GetComponent<NavMeshAgent>();
            meshRenderer = GetComponentInChildren<Renderer>();
            propBlock = new MaterialPropertyBlock();
            spawnPosition = transform.position;
            spawnRotation = transform.rotation;

            // Tự tạo EnemyData nếu chưa có
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<EnemyData>();
                data.enemyID = "dummy_enemy";
                data.nameKey = "Dummy Enemy";
                data.baseHP = 500f;
                data.baseDamage = 15f;
                data.moveSpeed = 3.5f;
                data.attackRange = 2.5f;
                data.attackCooldown = 2f;
                data.detectionRange = 15f;
                data.physicalDefense = 10f;
                data.magicDefense = 5f;
                data.expReward = 100f;
                data.goldReward = 50;
            }

            ApplyModeOverrides();
            SetupAI();
        }

        protected override void Start()
        {
            base.Start();
            lastHP = currentHP;
            SetColor(normalColor);

            if (showDamageLog)
                Debug.Log($"[DummyEnemy] Spawned: {mode} | HP: {currentHP}/{MaxHP} | DMG: {data.baseDamage} | DEF: {data.physicalDefense}/{data.magicDefense}");
        }

        protected override void Update()
        {
            base.Update();

            // Hit flash
            if (hitFlashTimer > 0)
            {
                hitFlashTimer -= Time.deltaTime;
                if (hitFlashTimer <= 0)
                    SetColor(normalColor);
            }

            // DPS tracker
            dpsTimer += Time.deltaTime;
            if (dpsTimer >= 1f)
            {
                dpsTracker = dpsDamageWindow / dpsTimer;
                dpsDamageWindow = 0f;
                dpsTimer = 0f;
            }

            // Standing dummy: face player
            if (mode == DummyMode.StandingDummy && IsAlive)
            {
                var player = Game.Player;
                if (player != null)
                {
                    Vector3 dir = (player.transform.position - transform.position);
                    dir.y = 0;
                    if (dir.sqrMagnitude > 1f)
                        transform.rotation = Quaternion.Slerp(transform.rotation,
                            Quaternion.LookRotation(dir.normalized), Time.deltaTime * 2f);
                }
            }
        }

        // ═══════════════════════════════════════════════════════
        // Mode Setup
        // ═══════════════════════════════════════════════════════

        private void ApplyModeOverrides()
        {
            switch (mode)
            {
                case DummyMode.StandingDummy:
                    data.baseHP = 99999f;
                    data.baseDamage = 0f;
                    data.dodgeChance = 0f;
                    data.blockChance = 0f;
                    data.physicalDefense = 0f;
                    data.magicDefense = 0f;
                    infiniteHP = true;
                    break;

                case DummyMode.PassiveAI:
                    data.baseDamage = 0f;
                    break;

                case DummyMode.AggressiveAI:
                    // Use default EnemyData values
                    break;

                case DummyMode.BossTest:
                    data.baseHP = bossHP;
                    data.baseDamage = bossDamage;
                    data.attackCooldown = bossAttackCooldown;
                    data.tier = EnemyTier.Boss;
                    data.physicalDefense = 30f;
                    data.magicDefense = 20f;
                    data.dodgeChance = 0.1f;
                    break;
            }
        }

        private void SetupAI()
        {
            enemyAI = GetComponent<EnemyAI>();
            if (enemyAI == null && mode != DummyMode.StandingDummy)
            {
                enemyAI = gameObject.AddComponent<EnemyAI>();
            }

            if (enemyAI != null)
            {
                enemyAI.Initialize(data);

                if (mode == DummyMode.StandingDummy)
                {
                    enemyAI.CommandState(EnemyAIState.Idle);
                    enemyAI.enabled = false;
                }
            }

            if (agent != null)
            {
                agent.speed = data.moveSpeed;
                agent.stoppingDistance = data.attackRange * 0.8f;

                if (mode == DummyMode.StandingDummy)
                    agent.enabled = false;
            }
        }

        // ═══════════════════════════════════════════════════════
        // Override Damage — log + visual feedback
        // ═══════════════════════════════════════════════════════

        public new DamageResult TakeDamage(DamageInfo damageInfo)
        {
            float hpBefore = currentHP;

            var result = base.TakeDamage(damageInfo);

            if (infiniteHP && IsAlive)
            {
                currentHP = MaxHP;
            }

            // Stats tracking
            totalHitsTaken++;
            totalDamageReceived += result.FinalDamage;
            dpsDamageWindow += result.FinalDamage;

            // Visual flash
            if (result.FinalDamage > 0)
            {
                SetColor(hitColor);
                hitFlashTimer = hitFlashDuration;
            }

            // Debug log
            if (showDamageLog)
            {
                string flags = "";
                if (result.WasCrit) flags += " [CRIT]";
                if (result.WasBlocked) flags += " [BLOCKED]";
                if (result.WasDodged) flags += " [DODGED]";
                if (result.WasParried) flags += " [PARRIED]";

                string srcName = "Unknown";
                if (damageInfo.Source is MonoBehaviour mb) srcName = mb.gameObject.name;

                Debug.Log($"[DummyEnemy] HIT #{totalHitsTaken}: {result.FinalDamage:F1} dmg{flags} " +
                          $"| Type: {damageInfo.Type} | Heavy: {damageInfo.IsHeavyAttack} " +
                          $"| HP: {currentHP:F0}/{MaxHP:F0} ({(currentHP / MaxHP * 100):F0}%) " +
                          $"| From: {srcName} | DPS: {dpsTracker:F1}");
            }

            // Loot on death
            if (!IsAlive && result.FinalDamage > 0)
            {
                ProcessDeathRewards();
            }

            return result;
        }

        // ═══════════════════════════════════════════════════════
        // Death & Respawn
        // ═══════════════════════════════════════════════════════

        protected override void HandleDeath()
        {
            SetColor(deadColor);

            if (showDamageLog)
            {
                Debug.Log($"[DummyEnemy] DEAD! Total hits: {totalHitsTaken} | Total damage: {totalDamageReceived:F0} | Avg DPS: {(totalDamageReceived > 0 && dpsTimer > 0 ? totalDamageReceived / dpsTimer : 0):F1}");
            }

            if (autoRespawn)
            {
                // Skip base.HandleDeath() to avoid Destroy(gameObject, 5f)
                // Rewards already processed in TakeDamage override
                if (enemyAI != null)
                    enemyAI.OnDied();

                currentCombatState = ECombatState.Dead;

                var col = GetComponent<Collider>();
                if (col != null) col.enabled = false;

                Invoke(nameof(Respawn), respawnDelay);
            }
            else
            {
                // Non-respawn: let base handle everything (fires OnDeath, Destroy after 5s)
                base.HandleDeath();
            }
        }

        private void Respawn()
        {
            // Reset state
            transform.position = spawnPosition;
            transform.rotation = spawnRotation;
            currentHP = MaxHP;
            currentCombatState = ECombatState.Idle;
            totalHitsTaken = 0;
            totalDamageReceived = 0;
            dpsTracker = 0;
            dpsDamageWindow = 0;
            dpsTimer = 0;

            var col = GetComponent<Collider>();
            if (col != null) col.enabled = true;

            if (agent != null)
            {
                agent.enabled = true;
                agent.Warp(spawnPosition);
            }

            if (enemyAI != null)
            {
                enemyAI.enabled = mode != DummyMode.StandingDummy;
                enemyAI.CommandState(EnemyAIState.Idle);
            }

            SetColor(normalColor);

            if (showDamageLog)
                Debug.Log($"[DummyEnemy] RESPAWNED at {spawnPosition}");
        }

        private void ProcessDeathRewards()
        {
            if (data == null) return;

            // Grant EXP
            if (Game.Level != null && data.expReward > 0)
            {
                Game.Level.AddExp(data.expReward);
                if (showDamageLog) Debug.Log($"[DummyEnemy] +{data.expReward} EXP");
            }

            // Grant Gold
            if (Game.Inv != null && data.goldReward > 0)
            {
                Game.Inv.AddGold(data.goldReward);
                if (showDamageLog) Debug.Log($"[DummyEnemy] +{data.goldReward} Gold");
            }

            // Roll loot table
            if (data.lootTable != null)
            {
                var drops = data.lootTable.Roll();
                foreach (var (item, qty) in drops)
                {
                    if (Game.Inv != null)
                    {
                        Game.Inv.AddItem(item, qty);
                        if (showDamageLog) Debug.Log($"[DummyEnemy] Loot: {item.nameKey} x{qty}");
                    }
                }
            }
        }

        // ═══════════════════════════════════════════════════════
        // Visual Helpers
        // ═══════════════════════════════════════════════════════

        private void SetColor(Color color)
        {
            if (meshRenderer == null) return;
            propBlock.SetColor("_Color", color);
            propBlock.SetColor("_BaseColor", color); // URP
            meshRenderer.SetPropertyBlock(propBlock);
        }

        // ═══════════════════════════════════════════════════════
        // Editor Gizmos
        // ═══════════════════════════════════════════════════════

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            // Detection range
            Gizmos.color = new Color(1f, 1f, 0f, 0.08f);
            float detRange = data != null ? data.detectionRange : 15f;
            Gizmos.DrawWireSphere(transform.position, detRange);

            // Attack range
            Gizmos.color = new Color(1f, 0f, 0f, 0.15f);
            float atkRange = data != null ? data.attackRange : 2.5f;
            Gizmos.DrawWireSphere(transform.position, atkRange);

            // Mode label
            UnityEditor.Handles.Label(transform.position + Vector3.up * 2.5f,
                $"[{mode}] HP: {(Application.isPlaying ? $"{currentHP:F0}/{MaxHP:F0}" : (data != null ? data.baseHP.ToString() : "500"))}",
                new GUIStyle { normal = { textColor = Color.yellow }, fontSize = 12, fontStyle = FontStyle.Bold });
        }
#endif

        // ═══════════════════════════════════════════════════════
        // Context Menu — Quick Actions
        // ═══════════════════════════════════════════════════════

        [ContextMenu("Reset Stats")]
        private void ResetStats()
        {
            totalHitsTaken = 0;
            totalDamageReceived = 0;
            dpsTracker = 0;
            Debug.Log("[DummyEnemy] Stats reset.");
        }

        [ContextMenu("Force Kill")]
        private void ForceKill()
        {
            currentHP = 0;
            HandleDeath();
        }

        [ContextMenu("Heal Full")]
        private void HealFull()
        {
            currentHP = MaxHP;
            Debug.Log("[DummyEnemy] Healed to full.");
        }
    }
}
