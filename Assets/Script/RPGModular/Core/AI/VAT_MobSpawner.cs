using System.Collections.Generic;
using UnityEngine;
using BillInspector;
using BillGameCore;

namespace RPGModular
{
    [BillTitle("VAT Mob Spawner", "Spawn enemy packs using VAT + Bill.Pool")]
    public class VAT_MobSpawner : MonoBehaviour
    {
        [BillBoxGroup("Spawn Config")]
        [BillRequired] [SerializeField] private GameObject vatEnemyPrefab;
        [BillRequired] [SerializeField] private EnemyData enemyData;
        [BillSlider(1, 20)] [SerializeField] private int packSize = 5;
        [BillSlider(5f, 30f)] [SerializeField] private float spawnRadius = 10f;

        [BillBoxGroup("Activation")]
        [BillSlider(20f, 100f)] [SerializeField] private float activationRange = 50f;
        [BillSlider(50f, 150f)] [SerializeField] private float despawnRange = 80f;
        [BillSlider(10f, 120f), BillSuffix("s")] [SerializeField] private float respawnDelay = 30f;

        [BillBoxGroup("Pool")]
        [SerializeField] private string poolKey;  // Bill.Pool key (register prefab in PoolConfig)

        [BillReadOnly, BillShowInInspector]
        private PackManager packManager;

        private List<GameObject> spawnedEnemies = new List<GameObject>();
        private bool isActive;
        private float respawnTimer;
        private Transform playerTransform;

        private void Start()
        {
            packManager = GetComponent<PackManager>();
            if (packManager == null)
                packManager = gameObject.AddComponent<PackManager>();

            if (enemyData != null)
                packManager.EnemyLevel = enemyData.baseLevel;
        }

        private void Update()
        {
            if (playerTransform == null)
            {
                var player = Game.Player;
                if (player != null) playerTransform = player.transform;
                if (playerTransform == null) return;
            }

            float dist = Vector3.Distance(transform.position, playerTransform.position);

            if (!isActive && dist <= activationRange)
            {
                SpawnPack();
                isActive = true;
            }
            else if (isActive && dist > despawnRange)
            {
                DespawnPack();
                isActive = false;
            }

            // Respawn check
            if (isActive && packManager.AliveCount <= 0 && spawnedEnemies.Count > 0)
            {
                respawnTimer -= Time.deltaTime;
                if (respawnTimer <= 0f)
                {
                    DespawnPack();
                    SpawnPack();
                }
            }
        }

        private void SpawnPack()
        {
            if (vatEnemyPrefab == null || enemyData == null) return;

            respawnTimer = respawnDelay;

            for (int i = 0; i < packSize; i++)
            {
                Vector3 offset = Random.insideUnitSphere * spawnRadius;
                offset.y = 0;
                Vector3 spawnPos = transform.position + offset;

                Quaternion rot = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
                GameObject enemy;
                if (Bill.Pool != null && !string.IsNullOrEmpty(poolKey))
                    enemy = Bill.Pool.Spawn(poolKey, spawnPos, rot);
                else
                    enemy = Instantiate(vatEnemyPrefab, spawnPos, rot);

                var ai = enemy.GetComponent<EnemyAI>();
                if (ai != null)
                {
                    ai.Initialize(enemyData);
                    packManager.RegisterEnemy(ai);
                }

                spawnedEnemies.Add(enemy);
            }
        }

        private void DespawnPack()
        {
            foreach (var enemy in spawnedEnemies)
            {
                if (enemy == null) continue;

                var ai = enemy.GetComponent<EnemyAI>();
                if (ai != null) packManager.UnregisterEnemy(ai);

                if (Bill.Pool != null)
                    Bill.Pool.Return(enemy);
                else
                    Destroy(enemy);
            }
            spawnedEnemies.Clear();
        }
    }
}
