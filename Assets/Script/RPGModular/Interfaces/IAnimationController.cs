using System;

namespace RPGModular
{

    public enum AnimationPriority
    {
        Locomotion = 0,
        CombatIdle = 10,
        NormalAttack = 20,
        Skill = 30,
        Block = 35,
        HitReaction = 40,
        Knockback = 50,
        Stun = 60,
        Death = 100
    }

    public enum AnimationPhase
    {
        Startup,
        Active,
        Recovery,
        Done
    }

    public interface IAnimationController
    {

        bool PlayAnimation(string stateName, AnimationPriority priority,
                          float crossFadeDuration = 0.1f, int layer = 0);

        bool PlayAction(AnimationActionData actionData, AnimationPriority priority,
                       Action<AnimationPhase> onPhaseChanged = null);

        void ForcePlay(string stateName, float crossFadeDuration = 0.1f, int layer = 0);

        void SetFloat(string paramName, float value, float dampTime = 0.1f);

        void SetBool(string paramName, bool value);

        float GetNormalizedTime(int layer = 0);

        AnimationPriority CurrentPriority { get; }

        AnimationPhase CurrentPhase { get; }

        bool CanBeInterrupted { get; }

        event Action OnActionComplete;

        event Action<AnimationPhase> OnPhaseChanged;
    }

    [Serializable]
    public class AnimationActionData
    {
        public string AnimationStateName;
        public float CrossFadeDuration = 0.1f;

        public float StartupEnd = 0.2f;
        public float ActiveEnd = 0.6f;

        public bool CanCancelStartup = true;
        public bool CanCancelRecovery = false;

        public int AnimatorLayer = 0;
    }
}
