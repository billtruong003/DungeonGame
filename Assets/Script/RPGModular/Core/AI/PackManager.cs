using System;
using System.Collections.Generic;
using UnityEngine;
using BillInspector;

namespace RPGModular
{
    [BillTitle("Pack Manager", "Manages enemy pack behavior and threat")]
    public class PackManager : MonoBehaviour
    {
        [BillBoxGroup("Config")]
        [BillSlider(1, 5)] [SerializeField] private int baseChasers = 2;
        [BillBoxGroup("Config")]
        [BillSlider(0.5f, 5f), BillSuffix("s")] [SerializeField] private float chaserRefillDelay = 2f;
        [BillBoxGroup("Config")]
        [BillSlider(5f, 30f)] [SerializeField] private float packAggroRadius = 15f;
        [BillBoxGroup("Config")]
        [BillSlider(10f, 50f)] [SerializeField] private float packLeashRadius = 25f;
        [BillBoxGroup("Config")]
        [BillSlider(3f, 15f), BillSuffix("s")] [SerializeField] private float deaggroTime = 8f;

        [BillReadOnly, BillShowInInspector]
        public ThreatLevel CurrentThreat { get; private set; } = ThreatLevel.Normal;

        [BillReadOnly, BillShowInInspector]
        public int EnemyLevel { get; set; } = 10;

        private List<EnemyAI> enemies = new List<EnemyAI>();
        private List<EnemyAI> activeChasers = new List<EnemyAI>();
        private float evaluateTimer;
        private float chaserRefillTimer;
        private float lastAggroTime;
        private Transform playerTarget;

        public event Action<ThreatLevel> OnThreatChanged;
        public event Action OnPackWiped;

        public int AliveCount { get { int c = 0; foreach (var e in enemies) if (e != null && e.CurrentState != EnemyAIState.Dead) c++; return c; } }

        public void RegisterEnemy(EnemyAI enemy)
        {
            if (!enemies.Contains(enemy))
                enemies.Add(enemy);
        }

        public void UnregisterEnemy(EnemyAI enemy)
        {
            enemies.Remove(enemy);
            activeChasers.Remove(enemy);

            if (AliveCount <= 0)
                OnPackWiped?.Invoke();
        }

        private void Update()
        {
            evaluateTimer -= Time.deltaTime;
            if (evaluateTimer <= 0f)
            {
                evaluateTimer = 0.4f; // evaluate ~2.5 times per second
                EvaluatePack();
            }
        }

        private void EvaluatePack()
        {
            // Find player
            if (playerTarget == null)
            {
                var player = Game.Player;
                if (player != null) playerTarget = player.transform;
            }
            if (playerTarget == null) return;

            float distToPlayer = Vector3.Distance(transform.position, playerTarget.position);

            // Not in aggro range
            if (distToPlayer > packAggroRadius)
            {
                if (activeChasers.Count > 0 && Time.time - lastAggroTime > deaggroTime)
                {
                    foreach (var chaser in activeChasers)
                        chaser?.CommandState(EnemyAIState.Retreat);
                    activeChasers.Clear();
                }
                return;
            }

            lastAggroTime = Time.time;

            // Calculate threat
            int playerLevel = Game.Level?.Level ?? 1;
            int gap = playerLevel - EnemyLevel;
            ThreatLevel newThreat = CalculateThreat(gap);

            if (newThreat != CurrentThreat)
            {
                CurrentThreat = newThreat;
                OnThreatChanged?.Invoke(CurrentThreat);
            }

            // Set target for all enemies
            foreach (var e in enemies)
                if (e != null) e.SetTarget(playerTarget);

            // Manage chasers by threat level
            int maxChasers = GetMaxChasers(CurrentThreat);

            switch (CurrentThreat)
            {
                case ThreatLevel.Terrified:
                    CommandAll(EnemyAIState.Flee);
                    break;

                case ThreatLevel.Wary:
                    CommandAll(EnemyAIState.Alert);
                    break;

                default:
                    ManageChasers(maxChasers);
                    break;
            }
        }

        private void ManageChasers(int maxChasers)
        {
            // Clean dead/null chasers
            activeChasers.RemoveAll(c => c == null || c.CurrentState == EnemyAIState.Dead);

            // Too many → recall furthest
            while (activeChasers.Count > maxChasers && activeChasers.Count > 0)
            {
                float maxDist = 0;
                int farthestIdx = 0;
                for (int i = 0; i < activeChasers.Count; i++)
                {
                    float d = Vector3.Distance(activeChasers[i].transform.position, playerTarget.position);
                    if (d > maxDist) { maxDist = d; farthestIdx = i; }
                }
                activeChasers[farthestIdx].CommandState(EnemyAIState.Alert);
                activeChasers.RemoveAt(farthestIdx);
            }

            // Not enough → add nearest (with refill delay)
            if (activeChasers.Count < maxChasers)
            {
                chaserRefillTimer -= Time.deltaTime;
                if (chaserRefillTimer <= 0f)
                {
                    chaserRefillTimer = chaserRefillDelay;
                    AddNearestChaser();
                }
            }

            // Non-chasers → alert
            foreach (var e in enemies)
            {
                if (e == null || e.CurrentState == EnemyAIState.Dead) continue;
                if (!activeChasers.Contains(e) &&
                    e.CurrentState != EnemyAIState.Alert &&
                    e.CurrentState != EnemyAIState.Retreat)
                {
                    e.CommandState(EnemyAIState.Alert);
                }
            }
        }

        private void AddNearestChaser()
        {
            EnemyAI nearest = null;
            float nearestDist = float.MaxValue;

            foreach (var e in enemies)
            {
                if (e == null || e.CurrentState == EnemyAIState.Dead) continue;
                if (activeChasers.Contains(e)) continue;

                float d = Vector3.Distance(e.transform.position, playerTarget.position);
                if (d < nearestDist) { nearestDist = d; nearest = e; }
            }

            if (nearest != null)
            {
                nearest.CommandState(EnemyAIState.Chase);
                activeChasers.Add(nearest);
            }
        }

        private void CommandAll(EnemyAIState state)
        {
            activeChasers.Clear();
            foreach (var e in enemies)
                if (e != null && e.CurrentState != EnemyAIState.Dead)
                    e.CommandState(state);
        }

        private ThreatLevel CalculateThreat(int gap)
        {
            if (gap >= 10) return ThreatLevel.Terrified;
            if (gap >= 5) return ThreatLevel.Wary;
            if (gap >= -2) return ThreatLevel.Normal;
            if (gap >= -7) return ThreatLevel.Aggressive;
            return ThreatLevel.Bloodlust;
        }

        private int GetMaxChasers(ThreatLevel threat)
        {
            return threat switch
            {
                ThreatLevel.Terrified => 0,
                ThreatLevel.Wary => 0,
                ThreatLevel.Normal => baseChasers,
                ThreatLevel.Aggressive => baseChasers + 2,
                ThreatLevel.Bloodlust => enemies.Count,
                _ => baseChasers,
            };
        }
    }
}
