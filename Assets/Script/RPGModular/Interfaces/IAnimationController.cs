// File: Interfaces/IAnimationController.cs
// Contract: Combat, Skill, Weapon đều gọi animation qua interface này
// Không ai trực tiếp gọi Animator.CrossFade() ngoài AnimationController
using System;

namespace RPGModular
{
    /// <summary>
    /// Mức ưu tiên animation - cao hơn sẽ override thấp hơn.
    /// Khi animation priority cao đang play, priority thấp hơn không thể interrupt.
    /// </summary>
    public enum AnimationPriority
    {
        Locomotion = 0,     // Walk, Run, Idle
        CombatIdle = 10,    // Thủ thế vũ khí
        NormalAttack = 20,  // Đánh thường
        Skill = 30,         // Skill tấn công/buff
        Block = 35,         // Đang block
        HitReaction = 40,   // Bị đánh trúng
        Knockback = 50,     // Bị đẩy lùi
        Stun = 60,          // Bị choáng
        Death = 100         // Chết - không gì override được
    }

    /// <summary>
    /// Phase của một animation action (cho combat timing).
    /// Startup → Active → Recovery → Done
    /// </summary>
    public enum AnimationPhase
    {
        Startup,    // Wind-up, có thể cancel bằng dodge
        Active,     // Hitbox active, đang gây damage
        Recovery,   // Hồi phục, có thể bị punish
        Done        // Animation xong
    }

    /// <summary>
    /// Interface chính điều khiển animation.
    /// Mọi module khác gọi qua đây, không trực tiếp access Animator.
    /// </summary>
    public interface IAnimationController
    {
        /// <summary>
        /// Play animation với priority. Chỉ play nếu priority >= current.
        /// </summary>
        /// <param name="stateName">Tên animation state trong Animator</param>
        /// <param name="priority">Mức ưu tiên</param>
        /// <param name="crossFadeDuration">Thời gian blend</param>
        /// <param name="layer">Animator layer (0 = full body, 1 = upper body...)</param>
        /// <returns>true nếu animation được chấp nhận play</returns>
        bool PlayAnimation(string stateName, AnimationPriority priority, 
                          float crossFadeDuration = 0.1f, int layer = 0);

        /// <summary>
        /// Play một AnimationAction (có startup/active/recovery phase).
        /// Dùng cho combat - cần biết khi nào hitbox active, khi nào recovery.
        /// </summary>
        bool PlayAction(AnimationActionData actionData, AnimationPriority priority,
                       Action<AnimationPhase> onPhaseChanged = null);

        /// <summary>
        /// Force play - bỏ qua priority check. Chỉ dùng cho Death, Cutscene.
        /// </summary>
        void ForcePlay(string stateName, float crossFadeDuration = 0.1f, int layer = 0);

        /// <summary>
        /// Set parameter float trên Animator (vd: MoveSpeed blend tree).
        /// </summary>
        void SetFloat(string paramName, float value, float dampTime = 0.1f);

        /// <summary>
        /// Set parameter bool trên Animator.
        /// </summary>
        void SetBool(string paramName, bool value);

        /// <summary>
        /// Kiểm tra animation hiện tại đã chạy đến % nào (0-1).
        /// </summary>
        float GetNormalizedTime(int layer = 0);

        /// <summary>
        /// Priority hiện tại đang active.
        /// </summary>
        AnimationPriority CurrentPriority { get; }

        /// <summary>
        /// Phase hiện tại nếu đang play AnimationAction.
        /// </summary>
        AnimationPhase CurrentPhase { get; }

        /// <summary>
        /// Có đang trong trạng thái có thể bị interrupt không.
        /// (Startup có thể cancel, Recovery có thể bị punish)
        /// </summary>
        bool CanBeInterrupted { get; }

        /// <summary>
        /// Khi animation action kết thúc hoàn toàn.
        /// AnimationController sẽ tự return về idle phù hợp.
        /// </summary>
        event Action OnActionComplete;

        /// <summary>
        /// Khi phase thay đổi (Startup → Active → Recovery → Done).
        /// </summary>
        event Action<AnimationPhase> OnPhaseChanged;
    }

    /// <summary>
    /// Data cho một animation action (skill, attack, block...).
    /// Tách data ra khỏi logic - dùng ScriptableObject hoặc serialize.
    /// </summary>
    [Serializable]
    public class AnimationActionData
    {
        public string AnimationStateName;       // Tên state trong Animator
        public float CrossFadeDuration = 0.1f;  // Blend time
        
        // Phase timing (normalized 0-1 trong animation clip)
        public float StartupEnd = 0.2f;         // Startup kết thúc tại 20%
        public float ActiveEnd = 0.6f;          // Active kết thúc tại 60%  
        // Recovery = từ ActiveEnd đến 1.0
        
        public bool CanCancelStartup = true;    // Có thể cancel trong startup (dodge)
        public bool CanCancelRecovery = false;   // Có thể cancel recovery (chain combo)
        
        public int AnimatorLayer = 0;           // Layer nào
    }
}
