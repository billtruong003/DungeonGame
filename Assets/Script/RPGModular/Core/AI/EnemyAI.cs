using UnityEngine;
using UnityEngine.AI;
using BillInspector;

namespace RPGModular
{
    [BillTitle("Enemy AI", "9-state enemy behavior")]
    [RequireComponent(typeof(NavMeshAgent))]
    public class EnemyAI : MonoBehaviour
    {
        [BillBoxGroup("Config")]
        [SerializeField] private EnemyData enemyData;
        [BillBoxGroup("Config")]
        [BillSlider(1f, 30f)] [SerializeField] private float patrolRadius = 8f;
        [BillBoxGroup("Config")]
        [BillSlider(1f, 10f), BillSuffix("s")] [SerializeField] private float patrolWaitTime = 3f;
        [BillBoxGroup("Config")]
        [BillSlider(5f, 30f)] [SerializeField] private float leashRadius = 20f;

        [BillReadOnly, BillShowInInspector]
        public EnemyAIState CurrentState { get; private set; } = EnemyAIState.Idle;

        private NavMeshAgent agent;
        private Transform target;
        private Vector3 spawnPosition;
        private float attackCooldownTimer;
        private float patrolWaitTimer;
        private float alertTimer;
        private float stateTimer;

        public EnemyData Data => enemyData;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            spawnPosition = transform.position;
        }

        public void Initialize(EnemyData data)
        {
            enemyData = data;
            if (agent != null)
                agent.speed = data.moveSpeed;
        }

        public void SetTarget(Transform newTarget) => target = newTarget;

        public void CommandState(EnemyAIState state)
        {
            CurrentState = state;
            stateTimer = 0f;

            switch (state)
            {
                case EnemyAIState.Chase:
                    if (agent != null) agent.isStopped = false;
                    break;
                case EnemyAIState.Alert:
                    if (agent != null) agent.isStopped = true;
                    break;
                case EnemyAIState.Flee:
                    if (agent != null) agent.isStopped = false;
                    break;
                case EnemyAIState.Retreat:
                    if (agent != null)
                    {
                        agent.isStopped = false;
                        agent.SetDestination(spawnPosition);
                    }
                    break;
            }
        }

        private void Update()
        {
            if (CurrentState == EnemyAIState.Dead) return;
            stateTimer += Time.deltaTime;

            switch (CurrentState)
            {
                case EnemyAIState.Idle: UpdateIdle(); break;
                case EnemyAIState.Patrol: UpdatePatrol(); break;
                case EnemyAIState.Alert: UpdateAlert(); break;
                case EnemyAIState.Chase: UpdateChase(); break;
                case EnemyAIState.Attack: UpdateAttack(); break;
                case EnemyAIState.Retreat: UpdateRetreat(); break;
                case EnemyAIState.Flee: UpdateFlee(); break;
                case EnemyAIState.ReactiveDefend: UpdateReactiveDefend(); break;
            }
        }

        private void UpdateIdle()
        {
            patrolWaitTimer -= Time.deltaTime;
            if (patrolWaitTimer <= 0f)
            {
                CurrentState = EnemyAIState.Patrol;
                Vector3 randomPoint = spawnPosition + Random.insideUnitSphere * patrolRadius;
                randomPoint.y = spawnPosition.y;
                if (NavMesh.SamplePosition(randomPoint, out var hit, patrolRadius, NavMesh.AllAreas))
                    agent?.SetDestination(hit.position);
            }
        }

        private void UpdatePatrol()
        {
            if (agent == null || !agent.pathPending && agent.remainingDistance < 0.5f)
            {
                patrolWaitTimer = patrolWaitTime;
                CurrentState = EnemyAIState.Idle;
            }
        }

        private void UpdateAlert()
        {
            // Face player, nervously watch
            if (target != null)
            {
                Vector3 dir = (target.position - transform.position).normalized;
                dir.y = 0;
                if (dir.sqrMagnitude > 0.01f)
                    transform.rotation = Quaternion.Slerp(transform.rotation,
                        Quaternion.LookRotation(dir), Time.deltaTime * 3f);
            }
        }

        private void UpdateChase()
        {
            if (target == null) { CommandState(EnemyAIState.Retreat); return; }

            float dist = Vector3.Distance(transform.position, target.position);

            // Leash check
            float spawnDist = Vector3.Distance(transform.position, spawnPosition);
            if (spawnDist > leashRadius) { CommandState(EnemyAIState.Retreat); return; }

            // In attack range?
            if (dist <= (enemyData != null ? enemyData.attackRange : 2f))
            {
                CurrentState = EnemyAIState.Attack;
                agent.isStopped = true;
                return;
            }

            agent?.SetDestination(target.position);
        }

        private void UpdateAttack()
        {
            if (target == null) { CommandState(EnemyAIState.Retreat); return; }

            float dist = Vector3.Distance(transform.position, target.position);
            float atkRange = enemyData != null ? enemyData.attackRange : 2f;

            // Out of range → chase
            if (dist > atkRange * 1.2f)
            {
                CurrentState = EnemyAIState.Chase;
                if (agent != null) agent.isStopped = false;
                return;
            }

            // Face target
            Vector3 dir = (target.position - transform.position).normalized;
            dir.y = 0;
            if (dir.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.LookRotation(dir);

            // Attack cooldown
            attackCooldownTimer -= Time.deltaTime;
            if (attackCooldownTimer <= 0f)
            {
                PerformAttack();
                attackCooldownTimer = enemyData != null ? enemyData.attackCooldown : 2f;
            }
        }

        private void UpdateRetreat()
        {
            if (agent == null) return;
            float dist = Vector3.Distance(transform.position, spawnPosition);
            if (dist < 1f)
            {
                CurrentState = EnemyAIState.Idle;
                patrolWaitTimer = patrolWaitTime;
                target = null;
            }
        }

        private void UpdateFlee()
        {
            if (target == null) { CommandState(EnemyAIState.Retreat); return; }

            Vector3 fleeDir = (transform.position - target.position).normalized;
            Vector3 fleeTarget = transform.position + fleeDir * 10f;
            if (NavMesh.SamplePosition(fleeTarget, out var hit, 10f, NavMesh.AllAreas))
                agent?.SetDestination(hit.position);

            // Flee for 5s then retreat
            if (stateTimer > 5f) CommandState(EnemyAIState.Retreat);
        }

        private void UpdateReactiveDefend()
        {
            // Hit back 1-2 times then retreat
            if (stateTimer > 3f) CommandState(EnemyAIState.Retreat);
        }

        private void PerformAttack()
        {
            if (target == null || enemyData == null) return;

            float atkRange = enemyData.attackRange;
            var hits = Physics.OverlapSphere(transform.position + transform.forward * atkRange * 0.5f,
                atkRange * 0.5f);

            foreach (var hit in hits)
            {
                if (hit.transform.IsChildOf(transform)) continue;
                var pdh = hit.GetComponent<PlayerDamageHandler>();
                if (pdh != null)
                {
                    pdh.TakeDamage(enemyData.baseDamage);
                    break;
                }
            }
        }

        public void OnDied()
        {
            CurrentState = EnemyAIState.Dead;
            if (agent != null) agent.isStopped = true;
        }
    }
}
