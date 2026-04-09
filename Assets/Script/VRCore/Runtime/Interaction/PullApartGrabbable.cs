using System;
using UnityEngine;
using UnityEngine.Events;
using VRCore.Hand;

namespace VRCore.Interaction
{
    [RequireComponent(typeof(Grabbable))]
    public class PullApartGrabbable : MonoBehaviour
    {
        [Header("Pull Apart")]
        [SerializeField] private float breakDistance = 0.4f;
        [SerializeField] private float breakForce = 50f;
        [SerializeField] private float resistanceForce = 30f;

        [Header("Break Result")]
        [SerializeField] private GameObject[] spawnOnBreak;
        [SerializeField] private bool destroyOnBreak = true;
        [SerializeField] private float breakDelay = 0.05f;

        [Header("Events")]
        [SerializeField] private UnityEvent onBreak;

        public float PullDistance { get; private set; }
        public float PullPercent => Mathf.Clamp01(PullDistance / breakDistance);
        public bool IsTwoHandPulling => _grabbable.HeldHandCount >= 2;
        public event Action<PullApartGrabbable> OnPullApart;

        private Grabbable _grabbable;
        private Rigidbody _rb;
        private float _initialHandDistance;
        private bool _broken;

        private void Awake()
        {
            _grabbable = GetComponent<Grabbable>();
            _rb = GetComponent<Rigidbody>();

            var baseGrabbable = _grabbable;
            baseGrabbable.SetSingleHandOnly(false);
        }

        private void FixedUpdate()
        {
            if (_broken) return;
            if (_grabbable.HeldHandCount < 2)
            {
                PullDistance = 0f;
                return;
            }

            var hands = _grabbable.HoldingHands;
            Vector3 hand0Pos = hands[0].FollowTarget != null ? hands[0].FollowTarget.position : hands[0].transform.position;
            Vector3 hand1Pos = hands[1].FollowTarget != null ? hands[1].FollowTarget.position : hands[1].transform.position;

            float currentDist = Vector3.Distance(hand0Pos, hand1Pos);

            if (_initialHandDistance < 0.01f)
                _initialHandDistance = currentDist;

            PullDistance = Mathf.Max(0f, currentDist - _initialHandDistance);

            if (PullDistance > 0.01f)
                ApplyResistance(hands[0], hands[1]);

            if (PullDistance >= breakDistance)
                ExecuteBreak(hand0Pos, hand1Pos);
        }

        private void ApplyResistance(VRHand hand0, VRHand hand1)
        {
            float strength = PullPercent * resistanceForce;
            Vector3 center = (hand0.transform.position + hand1.transform.position) * 0.5f;

            Vector3 dir0 = (center - hand0.transform.position).normalized;
            Vector3 dir1 = (center - hand1.transform.position).normalized;

            _rb.AddForceAtPosition(dir0 * strength, hand0.transform.position, ForceMode.Force);
            _rb.AddForceAtPosition(dir1 * strength, hand1.transform.position, ForceMode.Force);

            float haptic = PullPercent * 0.3f;
            hand0.Haptics.PlayHaptic(haptic, 0.02f);
            hand1.Haptics.PlayHaptic(haptic, 0.02f);
        }

        private void ExecuteBreak(Vector3 pos0, Vector3 pos1)
        {
            _broken = true;

            _grabbable.ForceRelease();

            Vector3 breakCenter = (pos0 + pos1) * 0.5f;
            Vector3 breakDir = (pos1 - pos0).normalized;

            if (spawnOnBreak != null)
            {
                for (int i = 0; i < spawnOnBreak.Length; i++)
                {
                    if (spawnOnBreak[i] == null) continue;

                    float side = (i % 2 == 0) ? -1f : 1f;
                    Vector3 spawnPos = breakCenter + breakDir * side * 0.1f;

                    var spawned = Instantiate(spawnOnBreak[i], spawnPos, transform.rotation);
                    var spawnedRb = spawned.GetComponent<Rigidbody>();
                    if (spawnedRb != null)
                        spawnedRb.AddForce(breakDir * side * breakForce, ForceMode.Impulse);
                }
            }

            onBreak?.Invoke();
            OnPullApart?.Invoke(this);

            if (destroyOnBreak)
                Destroy(gameObject, breakDelay);
        }

        public void SetBreakDistance(float dist) => breakDistance = dist;
        public void SetBreakForce(float force) => breakForce = force;
        public void SetResistance(float force) => resistanceForce = force;
    }
}
