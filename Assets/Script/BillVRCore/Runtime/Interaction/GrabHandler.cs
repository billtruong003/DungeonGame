using UnityEngine;
using BillVRCore.Input;
using BillVRCore.Interaction;

namespace BillVRCore.Hand
{
    [RequireComponent(typeof(VRHand))]
    public class GrabHandler : MonoBehaviour
    {
        [Header("Detection")]
        [SerializeField] private float grabRadius = 0.08f;
        [SerializeField] private LayerMask grabLayers = ~0;
        [SerializeField] private int maxOverlapResults = 10;
        [SerializeField] [Range(0f, 1f)] private float palmDirectionBias = 0.65f;

        [Header("Grab Mode")]
        [SerializeField] private GrabMode defaultGrabMode = GrabMode.Hold;

        [Header("Velocity Tracking")]
        [SerializeField] private float maxLinearVelocity = 60f;
        [SerializeField] private float maxAngularVelocity = 20f;
        [SerializeField] private float snapSpeed = 15f;

        [Header("Break")]
        [SerializeField] private float breakDistance = 0.5f;
        [SerializeField] private int breakFrameThreshold = 5;

        [Header("Grab Modes")]
        [SerializeField] private float objectToHandPullSpeed = 12f;
        [SerializeField] private float objectToHandArriveDistance = 0.1f;

        public bool IsHolding => _heldGrabbable != null;
        public Grabbable HeldObject => _heldGrabbable;
        public Grabbable HoveredObject => _hoveredGrabbable;
        public GrabState State { get; private set; } = GrabState.Empty;

        private VRHand _hand;
        private Grabbable _heldGrabbable;
        private Grabbable _hoveredGrabbable;
        private Collider[] _overlapBuffer;

        // Velocity tracking — object offset in follow-target local space
        private Vector3 _grabPosOffset;
        private Quaternion _grabRotOffset;
        private int _breakFrameCount;

        // Collision-aware tracking
        private GrabCollisionTracker _collisionTracker;

        // Saved object physics state
        private RigidbodyInterpolation _savedInterpolation;
        private CollisionDetectionMode _savedCollisionMode;

        // Pull-to-hand state
        private bool _isPulling;
        private Grabbable _pullingTarget;
        private Vector3 _pullStartPos;
        private float _pullProgress;
        private float _savedPullDrag;
        private bool _savedPullGravity;

        private void Awake()
        {
            _hand = GetComponent<VRHand>();
            _overlapBuffer = new Collider[maxOverlapResults];
        }

        private void FixedUpdate()
        {
            IVRInput input = _hand.GetInput();
            if (input == null) return;

            if (_isPulling)
            {
                UpdatePullToHand(input);
                return;
            }

            if (IsHolding)
            {
                TrackHeldObject();
                HandleHeldState(input);
                return;
            }

            ScanForGrabbables();
            HandleEmptyState(input);
        }

        // ─────────────────────────────────────────────────────────
        //  Velocity Tracking (collision-aware)
        // ─────────────────────────────────────────────────────────

        private void TrackHeldObject()
        {
            if (_heldGrabbable == null || _heldGrabbable.Rb == null) return;

            // Gadgets with their own joints (buttons, levers, dials) handle
            // their own physics — skip velocity tracking to avoid fighting.
            if (_heldGrabbable.DisableGrabTracking) return;

            Transform follow = _hand.FollowTarget ?? _hand.transform;
            Rigidbody objRb = _heldGrabbable.Rb;
            float dt = Time.fixedDeltaTime;

            if (objRb.IsSleeping()) objRb.WakeUp();

            // Smooth snap for non-maintain grabs — reduce offset toward zero
            if (!_heldGrabbable.MaintainGrabOffset)
            {
                _grabPosOffset = Vector3.MoveTowards(_grabPosOffset, Vector3.zero, snapSpeed * dt);
                _grabRotOffset = Quaternion.RotateTowards(_grabRotOffset, Quaternion.identity, snapSpeed * 120f * dt);
            }

            Vector3 targetPos = follow.position + follow.rotation * _grabPosOffset;
            Quaternion targetRot = follow.rotation * _grabRotOffset;

            // ── Position ──
            Vector3 posDelta = targetPos - objRb.position;
            Vector3 targetVel = posDelta / dt;

            float velMag = targetVel.magnitude;
            if (velMag > maxLinearVelocity)
                targetVel *= maxLinearVelocity / velMag;

            // Collision-aware: if the object is touching a surface, strip the
            // velocity component that pushes INTO the surface. The object can
            // still slide along the wall but won't clip through it.
            if (_collisionTracker != null && _collisionTracker.HasContact)
            {
                Vector3 normal = _collisionTracker.AverageNormal;
                float pushInto = Vector3.Dot(targetVel, -normal);
                if (pushInto > 0f)
                    targetVel += normal * pushInto;

                _collisionTracker.ConsumeContacts();
            }

            objRb.linearVelocity = targetVel;

            // ── Rotation ──
            Quaternion rotDelta = targetRot * Quaternion.Inverse(objRb.rotation);
            rotDelta.ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 180f) angle -= 360f;

            if (!float.IsInfinity(axis.x) && !float.IsNaN(axis.x))
            {
                Vector3 targetAngVel = axis * (angle * Mathf.Deg2Rad / dt);
                float angMag = targetAngVel.magnitude;
                if (angMag > maxAngularVelocity)
                    targetAngVel *= maxAngularVelocity / angMag;
                objRb.angularVelocity = targetAngVel;
            }
            else
            {
                objRb.angularVelocity = Vector3.zero;
            }

            // ── Break detection ──
            float dist = posDelta.magnitude;
            if (dist > breakDistance && !float.IsInfinity(_heldGrabbable.JointBreakForce))
            {
                _breakFrameCount++;
                if (_breakFrameCount > breakFrameThreshold)
                    ReleaseInternal(false);
            }
            else
            {
                _breakFrameCount = 0;
            }
        }

        // ─────────────────────────────────────────────────────────
        //  Detection & Input
        // ─────────────────────────────────────────────────────────

        private void ScanForGrabbables()
        {
            Vector3 palmPos = _hand.PalmTransform != null ? _hand.PalmTransform.position : transform.position;
            Vector3 palmForward = _hand.PalmTransform != null ? _hand.PalmTransform.forward : transform.forward;
            Vector3 palmRight = _hand.PalmTransform != null ? _hand.PalmTransform.right : transform.right;

            int count = Physics.OverlapSphereNonAlloc(palmPos, grabRadius, _overlapBuffer, grabLayers);

            Grabbable closest = null;
            float bestScore = float.MaxValue;

            Vector3 biasDirection = Vector3.Lerp(palmForward, palmRight, palmDirectionBias);

            for (int i = 0; i < count; i++)
            {
                var grabbable = _overlapBuffer[i].GetComponentInParent<Grabbable>();
                if (grabbable == null || !grabbable.CanBeGrabbedBy(_hand)) continue;

                Vector3 closestPoint = _overlapBuffer[i].ClosestPoint(palmPos);
                float distance = Vector3.Distance(palmPos, closestPoint);

                Vector3 toObject = (closestPoint - palmPos).normalized;
                float dot = Vector3.Dot(biasDirection, toObject);
                float score = distance - dot * grabRadius * 0.5f;

                if (score < bestScore)
                {
                    bestScore = score;
                    closest = grabbable;
                }
            }

            if (closest != _hoveredGrabbable)
            {
                if (_hoveredGrabbable != null)
                    _hoveredGrabbable.NotifyUnhighlight(_hand);

                _hoveredGrabbable = closest;

                if (_hoveredGrabbable != null)
                    _hoveredGrabbable.NotifyHighlight(_hand);
            }

            State = _hoveredGrabbable != null ? GrabState.Hovering : GrabState.Empty;
        }

        private void HandleEmptyState(IVRInput input)
        {
            if (_hoveredGrabbable == null || !input.GrabPressed(_hand.Side)) return;

            switch (_hoveredGrabbable.GrabType)
            {
                case GrabType.Default:
                case GrabType.HandToGrabbable:
                    ExecuteGrab(_hoveredGrabbable);
                    break;
                case GrabType.GrabbableToHand:
                    StartPullToHand(_hoveredGrabbable);
                    break;
                case GrabType.Instant:
                    ExecuteInstantGrab(_hoveredGrabbable);
                    break;
            }
        }

        private void HandleHeldState(IVRInput input)
        {
            GrabMode mode = GetEffectiveGrabMode(_heldGrabbable);

            bool shouldRelease = mode == GrabMode.Toggle
                ? input.GrabPressed(_hand.Side)
                : input.GrabReleased(_hand.Side);

            if (shouldRelease)
                ReleaseInternal(true);
            else if (input.TriggerPressed(_hand.Side))
                _heldGrabbable.OnSqueeze(_hand);
        }

        private GrabMode GetEffectiveGrabMode(Grabbable target)
        {
            if (target == null) return defaultGrabMode;
            return target.GrabMode == GrabMode.Default ? defaultGrabMode : target.GrabMode;
        }

        // ─────────────────────────────────────────────────────────
        //  Pull-to-Hand
        // ─────────────────────────────────────────────────────────

        private void StartPullToHand(Grabbable target)
        {
            _pullingTarget = target;
            _pullStartPos = target.transform.position;
            _pullProgress = 0f;
            _isPulling = true;
            _savedPullDrag = target.Rb.linearDamping;
            _savedPullGravity = target.Rb.useGravity;
            target.Rb.useGravity = false;
            target.Rb.linearDamping = 5f;
        }

        private void UpdatePullToHand(IVRInput input)
        {
            if (_pullingTarget == null) { _isPulling = false; return; }

            Vector3 handPos = _hand.PalmTransform != null ? _hand.PalmTransform.position : _hand.transform.position;
            float totalDist = Mathf.Max(Vector3.Distance(_pullStartPos, handPos), 0.01f);

            _pullProgress += Time.fixedDeltaTime * objectToHandPullSpeed / totalDist;
            _pullProgress = Mathf.Clamp01(_pullProgress);

            float t = _pullProgress * _pullProgress * (3f - 2f * _pullProgress);
            Vector3 targetPos = Vector3.Lerp(_pullStartPos, handPos, t);
            _pullingTarget.Rb.linearVelocity = (targetPos - _pullingTarget.transform.position) / Time.fixedDeltaTime * 0.5f;

            float distToHand = Vector3.Distance(_pullingTarget.transform.position, handPos);
            if (distToHand <= objectToHandArriveDistance || _pullProgress >= 1f)
            {
                CompletePullToHand();
                return;
            }

            if (input.GrabReleased(_hand.Side))
                CancelPull();
        }

        private void CompletePullToHand()
        {
            var target = _pullingTarget;
            target.Rb.useGravity = _savedPullGravity;
            target.Rb.linearDamping = _savedPullDrag;
            target.Rb.linearVelocity = Vector3.zero;

            _pullingTarget = null;
            _isPulling = false;

            ExecuteGrab(target);
        }

        private void CancelPull()
        {
            if (_pullingTarget == null) return;
            _pullingTarget.Rb.useGravity = _savedPullGravity;
            _pullingTarget.Rb.linearDamping = _savedPullDrag;
            _pullingTarget = null;
            _isPulling = false;
        }

        // ─────────────────────────────────────────────────────────
        //  Grab Execution
        // ─────────────────────────────────────────────────────────

        public bool TryGrab(Grabbable target)
        {
            if (target == null || IsHolding) return false;
            if (!target.CanBeGrabbedBy(_hand)) return false;
            ExecuteGrab(target);
            return true;
        }

        public bool TryGrab(Grabbable target, Vector3 grabPoint)
        {
            if (target == null || IsHolding) return false;
            if (!target.CanBeGrabbedBy(_hand)) return false;
            ExecuteGrabAtPoint(target, grabPoint);
            return true;
        }

        public void ForceGrab(Grabbable target)
        {
            if (IsHolding) ForceRelease();
            if (target != null) ExecuteGrab(target);
        }

        public void ForceRelease() => ReleaseInternal(false);
        public void ReleaseWithThrow() => ReleaseInternal(true);
        public void Release(bool applyThrow = true) => ReleaseInternal(applyThrow);

        private void ExecuteGrab(Grabbable target)
        {
            _heldGrabbable = target;
            _hoveredGrabbable = null;
            _breakFrameCount = 0;

            Transform follow = _hand.FollowTarget ?? _hand.transform;

            _grabPosOffset = Quaternion.Inverse(follow.rotation) * (target.transform.position - follow.position);
            _grabRotOffset = Quaternion.Inverse(follow.rotation) * target.transform.rotation;

            _hand.GrabPositionOffset = Vector3.zero;
            _hand.GrabRotationOffset = Quaternion.identity;

            // Grabbable.OnGrab saves original drag/gravity, disables gravity
            target.OnGrab(_hand);

            // Velocity tracking setup — skip for gadgets that manage their own physics
            if (!target.DisableGrabTracking)
            {
                SetHandObjectCollision(target, true);
                AttachCollisionTracker(target);

                target.Rb.linearDamping = 0f;
                target.Rb.angularDamping = 0f;

                _savedInterpolation = target.Rb.interpolation;
                target.Rb.interpolation = RigidbodyInterpolation.Interpolate;

                _savedCollisionMode = target.Rb.collisionDetectionMode;
                target.Rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            }

            _hand.Haptics.PlayGrabHaptic();
            _hand.ThrowTracker.Clear();
            State = GrabState.Grabbing;
        }

        private void ExecuteInstantGrab(Grabbable target)
        {
            Vector3 handPos = _hand.PalmTransform != null ? _hand.PalmTransform.position : transform.position;
            target.Rb.position = handPos;
            target.Rb.linearVelocity = Vector3.zero;
            target.Rb.angularVelocity = Vector3.zero;
            ExecuteGrab(target);
        }

        private void ExecuteGrabAtPoint(Grabbable target, Vector3 worldPoint)
        {
            _heldGrabbable = target;
            _hoveredGrabbable = null;
            _breakFrameCount = 0;

            Transform follow = _hand.FollowTarget ?? _hand.transform;

            _grabRotOffset = Quaternion.Inverse(follow.rotation) * target.transform.rotation;
            Vector3 grabPointLocal = target.transform.InverseTransformPoint(worldPoint);
            _grabPosOffset = -(_grabRotOffset * grabPointLocal);

            _hand.GrabPositionOffset = Vector3.zero;
            _hand.GrabRotationOffset = Quaternion.identity;

            target.OnGrab(_hand);

            if (!target.DisableGrabTracking)
            {
                SetHandObjectCollision(target, true);
                AttachCollisionTracker(target);

                target.Rb.linearDamping = 0f;
                target.Rb.angularDamping = 0f;
                _savedInterpolation = target.Rb.interpolation;
                target.Rb.interpolation = RigidbodyInterpolation.Interpolate;
                _savedCollisionMode = target.Rb.collisionDetectionMode;
                target.Rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            }

            _hand.Haptics.PlayGrabHaptic();
            _hand.ThrowTracker.Clear();
            State = GrabState.Grabbing;
        }

        // ─────────────────────────────────────────────────────────
        //  Release
        // ─────────────────────────────────────────────────────────

        private void ReleaseInternal(bool applyThrow)
        {
            if (_heldGrabbable == null) return;

            var released = _heldGrabbable;
            _heldGrabbable = null;
            _breakFrameCount = 0;

            if (!released.DisableGrabTracking)
            {
                SetHandObjectCollision(released, false);
                DetachCollisionTracker(released);
                released.Rb.interpolation = _savedInterpolation;
                released.Rb.collisionDetectionMode = _savedCollisionMode;
            }

            if (applyThrow)
            {
                released.ApplyThrowVelocity(
                    _hand.GetSmoothedThrowVelocity(),
                    _hand.GetSmoothedThrowAngularVelocity());
            }

            State = GrabState.Empty;
            _hand.Haptics.PlayReleaseHaptic();
            _hand.ThrowTracker.Clear();

            // Grabbable.OnRelease restores drag, angular drag, gravity, parent
            released.OnRelease(_hand);
        }

        // ─────────────────────────────────────────────────────────
        //  Collision Helpers
        // ─────────────────────────────────────────────────────────

        private void SetHandObjectCollision(Grabbable target, bool ignore)
        {
            var handCols = _hand.GetComponentsInChildren<Collider>();
            var objCols = target.GetComponentsInChildren<Collider>();
            foreach (var hc in handCols)
                foreach (var oc in objCols)
                    if (hc != null && oc != null)
                        Physics.IgnoreCollision(hc, oc, ignore);
        }

        private void AttachCollisionTracker(Grabbable target)
        {
            _collisionTracker = target.GetComponent<GrabCollisionTracker>();
            if (_collisionTracker == null)
                _collisionTracker = target.gameObject.AddComponent<GrabCollisionTracker>();
        }

        private void DetachCollisionTracker(Grabbable released)
        {
            if (_collisionTracker != null)
            {
                Destroy(_collisionTracker);
                _collisionTracker = null;
            }
        }

        // ─────────────────────────────────────────────────────────
        //  Public API
        // ─────────────────────────────────────────────────────────

        public void SetGrabRadius(float radius) => grabRadius = radius;
        public void SetPalmDirectionBias(float bias) => palmDirectionBias = Mathf.Clamp01(bias);
        public void SetBreakDistance(float distance) => breakDistance = distance;
        public void SetMaxTrackingVelocity(float velocity) => maxLinearVelocity = velocity;
        public void SetDefaultGrabMode(GrabMode mode) => defaultGrabMode = mode;

        private void OnDrawGizmosSelected()
        {
            Vector3 palmPos = (Application.isPlaying && _hand != null && _hand.PalmTransform != null)
                ? _hand.PalmTransform.position
                : transform.position;

            Gizmos.color = IsHolding ? Color.green : (_hoveredGrabbable != null ? Color.yellow : Color.cyan);
            Gizmos.DrawWireSphere(palmPos, grabRadius);
        }
    }
}
