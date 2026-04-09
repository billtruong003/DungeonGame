using UnityEngine;
using VRCore.Input;
using VRCore.Interaction;

namespace VRCore.Hand
{
    [RequireComponent(typeof(VRHand))]
    public class GrabHandler : MonoBehaviour
    {
        [Header("Detection")]
        [SerializeField] private float grabRadius = 0.08f;
        [SerializeField] private LayerMask grabLayers = ~0;
        [SerializeField] private int maxOverlapResults = 10;

        [Header("Joint Settings")]
        [SerializeField] private float spring = 5000f;
        [SerializeField] private float damper = 200f;
        [SerializeField] private float maxForce = 2000f;

        public bool IsHolding => _heldGrabbable != null;
        public Grabbable HeldObject => _heldGrabbable;
        public Grabbable HoveredObject => _hoveredGrabbable;
        public GrabState State { get; private set; } = GrabState.Empty;

        private VRHand _hand;
        private Grabbable _heldGrabbable;
        private Grabbable _hoveredGrabbable;
        private ConfigurableJoint _grabJoint;
        private Collider[] _overlapBuffer;

        private void Awake()
        {
            _hand = GetComponent<VRHand>();
            _overlapBuffer = new Collider[maxOverlapResults];
        }

        private void FixedUpdate()
        {
            IVRInput input = _hand.GetInput();
            if (input == null) return;

            if (IsHolding)
            {
                HandleHeldState(input);
                return;
            }

            ScanForGrabbables();
            HandleEmptyState(input);
        }

        private void ScanForGrabbables()
        {
            Vector3 palmPos = _hand.PalmTransform != null
                ? _hand.PalmTransform.position
                : transform.position;

            int count = Physics.OverlapSphereNonAlloc(palmPos, grabRadius, _overlapBuffer, grabLayers);

            Grabbable closest = null;
            float closestDist = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                var grabbable = _overlapBuffer[i].GetComponentInParent<Grabbable>();
                if (grabbable == null || !grabbable.CanBeGrabbedBy(_hand)) continue;

                float dist = Vector3.Distance(palmPos, _overlapBuffer[i].ClosestPoint(palmPos));
                if (dist < closestDist)
                {
                    closestDist = dist;
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
            if (_hoveredGrabbable != null && input.GrabPressed(_hand.Side))
                ExecuteGrab(_hoveredGrabbable);
        }

        private void HandleHeldState(IVRInput input)
        {
            if (_grabJoint == null)
            {
                ReleaseInternal(false);
                return;
            }

            if (input.GrabReleased(_hand.Side))
                ReleaseInternal(true);
            else if (input.TriggerPressed(_hand.Side))
                _heldGrabbable.OnSqueeze(_hand);
        }

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

        public void ForceRelease()
        {
            ReleaseInternal(false);
        }

        public void ReleaseWithThrow()
        {
            ReleaseInternal(true);
        }

        public void Release(bool applyThrow = true)
        {
            ReleaseInternal(applyThrow);
        }

        private void ExecuteGrab(Grabbable target)
        {
            _heldGrabbable = target;
            _hoveredGrabbable = null;

            if (target.MaintainGrabOffset)
                CreateJointWithOffset(target);
            else
                CreateJointSnapped(target);

            if (target.ParentOnGrab)
                target.transform.SetParent(_hand.FollowTarget);

            target.OnGrab(_hand);
            _hand.Haptics.PlayGrabHaptic();
            State = GrabState.Grabbing;
        }

        private void ExecuteGrabAtPoint(Grabbable target, Vector3 worldPoint)
        {
            _heldGrabbable = target;
            _hoveredGrabbable = null;

            _grabJoint = gameObject.AddComponent<ConfigurableJoint>();
            _grabJoint.connectedBody = target.Rb;
            _grabJoint.autoConfigureConnectedAnchor = false;
            _grabJoint.anchor = Vector3.zero;
            _grabJoint.connectedAnchor = target.Rb.transform.InverseTransformPoint(worldPoint);
            ConfigureJointDrives(_grabJoint);
            _grabJoint.breakForce = target.JointBreakForce;
            _grabJoint.breakTorque = target.JointBreakForce;

            target.OnGrab(_hand);
            _hand.Haptics.PlayGrabHaptic();
            State = GrabState.Grabbing;
        }

        private void CreateJointSnapped(Grabbable target)
        {
            _grabJoint = gameObject.AddComponent<ConfigurableJoint>();
            _grabJoint.connectedBody = target.Rb;
            _grabJoint.autoConfigureConnectedAnchor = false;
            _grabJoint.anchor = Vector3.zero;
            _grabJoint.connectedAnchor = target.Rb.transform.InverseTransformPoint(
                _hand.PalmTransform != null ? _hand.PalmTransform.position : transform.position);
            ConfigureJointDrives(_grabJoint);
            _grabJoint.breakForce = target.JointBreakForce;
            _grabJoint.breakTorque = target.JointBreakForce;
        }

        private void CreateJointWithOffset(Grabbable target)
        {
            _grabJoint = gameObject.AddComponent<ConfigurableJoint>();
            _grabJoint.connectedBody = target.Rb;
            _grabJoint.autoConfigureConnectedAnchor = true;
            ConfigureJointDrives(_grabJoint);
            _grabJoint.breakForce = target.JointBreakForce;
            _grabJoint.breakTorque = target.JointBreakForce;
        }

        private void ConfigureJointDrives(ConfigurableJoint joint)
        {
            joint.xMotion = ConfigurableJointMotion.Free;
            joint.yMotion = ConfigurableJointMotion.Free;
            joint.zMotion = ConfigurableJointMotion.Free;
            joint.angularXMotion = ConfigurableJointMotion.Free;
            joint.angularYMotion = ConfigurableJointMotion.Free;
            joint.angularZMotion = ConfigurableJointMotion.Free;

            var posDrive = new JointDrive
            {
                positionSpring = spring,
                positionDamper = damper,
                maximumForce = maxForce
            };
            joint.xDrive = posDrive;
            joint.yDrive = posDrive;
            joint.zDrive = posDrive;

            var rotDrive = new JointDrive
            {
                positionSpring = spring * 0.8f,
                positionDamper = damper * 0.8f,
                maximumForce = maxForce
            };
            joint.angularXDrive = rotDrive;
            joint.angularYZDrive = rotDrive;
        }

        private void ReleaseInternal(bool applyThrow)
        {
            if (_heldGrabbable == null) return;

            var released = _heldGrabbable;

            if (_grabJoint != null)
                Destroy(_grabJoint);
            _grabJoint = null;

            if (applyThrow)
                released.ApplyThrowVelocity(_hand.GetSmoothedThrowVelocity(), _hand.AngularVelocity);

            _heldGrabbable = null;
            State = GrabState.Empty;
            _hand.Haptics.PlayReleaseHaptic();
            released.OnRelease(_hand);
        }

        private void OnJointBreak(float breakForce)
        {
            if (_heldGrabbable != null) ReleaseInternal(false);
        }

        public void SetGrabRadius(float radius) => grabRadius = radius;
        public void SetJointStrength(float newSpring, float newDamper)
        {
            spring = newSpring;
            damper = newDamper;
        }

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
