using System;
using UnityEngine;

namespace VRCore.Locomotion
{
    [CreateAssetMenu(fileName = "LocomotionProfile", menuName = "VRCore/Locomotion Profile")]
    public class LocomotionProfile : ScriptableObject
    {
        public string profileName;

        [Header("Movement")]
        public bool enableJoystickMove = true;
        public bool enableGorillaMove;
        public float moveSpeed = 3f;

        [Header("Turning")]
        public bool useSnapTurn = true;
        public float snapAngle = 45f;
        public float smoothTurnSpeed = 90f;

        [Header("Teleport")]
        public bool enableTeleport;
        public float teleportMaxDistance = 15f;

        [Header("Advanced")]
        public bool enableClimbing = true;
        public bool enablePushing;
        public bool enableParkour;
        public bool enableSwimming;

        [Header("Comfort")]
        public bool vignetteOnMove;
        public float vignetteIntensity = 0.5f;

        public static LocomotionProfile CreateComfort()
        {
            var p = CreateInstance<LocomotionProfile>();
            p.profileName = "Comfort";
            p.enableJoystickMove = false;
            p.enableTeleport = true;
            p.useSnapTurn = true;
            p.snapAngle = 45f;
            p.vignetteOnMove = true;
            return p;
        }

        public static LocomotionProfile CreateStandard()
        {
            var p = CreateInstance<LocomotionProfile>();
            p.profileName = "Standard";
            p.enableJoystickMove = true;
            p.enableTeleport = true;
            p.useSnapTurn = true;
            p.snapAngle = 30f;
            p.enableClimbing = true;
            p.vignetteOnMove = true;
            p.vignetteIntensity = 0.3f;
            return p;
        }

        public static LocomotionProfile CreateFull()
        {
            var p = CreateInstance<LocomotionProfile>();
            p.profileName = "Full";
            p.enableJoystickMove = true;
            p.enableGorillaMove = true;
            p.enableTeleport = true;
            p.enableClimbing = true;
            p.enablePushing = true;
            p.enableParkour = true;
            p.enableSwimming = true;
            p.useSnapTurn = false;
            p.smoothTurnSpeed = 120f;
            return p;
        }
    }

    public class LocomotionProfileManager : MonoBehaviour
    {
        [SerializeField] private LocomotionProfile activeProfile;
        [SerializeField] private LocomotionProfile[] availableProfiles;

        public LocomotionProfile ActiveProfile => activeProfile;
        public event Action<LocomotionProfile> OnProfileChanged;

        private JoystickMoveProvider _joystick;
        private GorillaMoveProvider _gorilla;
        private TeleportProvider _teleport;
        private ClimbProvider _climb;
        private PushProvider _push;
        private SnapTurnProvider _snapTurn;
        private SmoothTurnProvider _smoothTurn;

        private void Start()
        {
            CacheProviders();
            if (activeProfile != null) ApplyProfile(activeProfile);
        }

        public void SetProfile(LocomotionProfile profile)
        {
            if (profile == null) return;
            activeProfile = profile;
            ApplyProfile(profile);
            OnProfileChanged?.Invoke(profile);
        }

        public void SetProfile(int index)
        {
            if (availableProfiles == null || index < 0 || index >= availableProfiles.Length) return;
            SetProfile(availableProfiles[index]);
        }

        public void SetProfile(string profileName)
        {
            if (availableProfiles == null) return;
            for (int i = 0; i < availableProfiles.Length; i++)
            {
                if (availableProfiles[i].profileName == profileName)
                {
                    SetProfile(availableProfiles[i]);
                    return;
                }
            }
        }

        private void ApplyProfile(LocomotionProfile p)
        {
            if (_joystick != null)
            {
                _joystick.enabled = p.enableJoystickMove;
                if (p.enableJoystickMove)
                {
                    var field = typeof(JoystickMoveProvider).GetField("maxSpeed",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    field?.SetValue(_joystick, p.moveSpeed);
                }
            }

            if (_gorilla != null) _gorilla.enabled = p.enableGorillaMove;
            if (_teleport != null)
            {
                _teleport.enabled = p.enableTeleport;
                if (p.enableTeleport) _teleport.SetMaxDistance(p.teleportMaxDistance);
            }
            if (_climb != null) _climb.enabled = p.enableClimbing;
            if (_push != null) _push.enabled = p.enablePushing;

            if (_snapTurn != null && _smoothTurn != null)
            {
                _snapTurn.enabled = p.useSnapTurn;
                _smoothTurn.enabled = !p.useSnapTurn;
            }
        }

        private void CacheProviders()
        {
            _joystick = GetComponentInChildren<JoystickMoveProvider>(true);
            _gorilla = GetComponentInChildren<GorillaMoveProvider>(true);
            _teleport = GetComponentInChildren<TeleportProvider>(true);
            _climb = GetComponentInChildren<ClimbProvider>(true);
            _push = GetComponentInChildren<PushProvider>(true);
            _snapTurn = GetComponentInChildren<SnapTurnProvider>(true);
            _smoothTurn = GetComponentInChildren<SmoothTurnProvider>(true);
        }
    }
}
