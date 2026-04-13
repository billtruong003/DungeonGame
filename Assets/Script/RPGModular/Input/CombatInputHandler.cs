// File: Input/CombatInputHandler.cs
// OBSOLETE — Use PlayerInputHandler instead.
// PlayerInputHandler is a superset that includes all combat input
// plus sprint, jump, dash, double-tap dodge, interact, and mouse delta.
using System;
using UnityEngine;

namespace RPGModular
{
    [Obsolete("Use PlayerInputHandler instead. It is a superset with all combat + locomotion input.")]
    public class CombatInputHandler : MonoBehaviour
    {
        [Header("Key Bindings")]
        [SerializeField] private KeyCode attackKey = KeyCode.Mouse0;
        [SerializeField] private KeyCode heavyAttackKey = KeyCode.Mouse1;
        [SerializeField] private KeyCode blockKey = KeyCode.Q;
        [SerializeField] private KeyCode lockOnKey = KeyCode.Tab;
        [SerializeField] private KeyCode switchTargetLeftKey = KeyCode.LeftBracket;
        [SerializeField] private KeyCode switchTargetRightKey = KeyCode.RightBracket;

        // Buffered input - giữ input 1 vài frame để combo mượt hơn
        [Header("Input Buffer")]
        [SerializeField] private float inputBufferTime = 0.15f;
        
        private float attackBufferTimer;
        private float heavyAttackBufferTimer;
        
        // Properties - các module khác đọc
        public bool AttackInput { get; private set; }
        public bool HeavyAttackInput { get; private set; }
        public bool BlockHeld { get; private set; }
        public bool LockOnToggle { get; private set; }
        public int SwitchTargetDirection { get; private set; } // -1, 0, 1

        // Movement input (forward from locomotion or new)
        public Vector2 MoveInput { get; private set; }
        public bool IsRunning { get; private set; }

        private void Update()
        {
            // Movement
            MoveInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            IsRunning = Input.GetKey(KeyCode.LeftShift);

            // Attack (with buffer)
            if (Input.GetKeyDown(attackKey))
            {
                attackBufferTimer = inputBufferTime;
            }
            attackBufferTimer -= Time.deltaTime;
            AttackInput = attackBufferTimer > 0f;

            // Heavy attack (with buffer)
            if (Input.GetKeyDown(heavyAttackKey))
            {
                heavyAttackBufferTimer = inputBufferTime;
            }
            heavyAttackBufferTimer -= Time.deltaTime;
            HeavyAttackInput = heavyAttackBufferTimer > 0f;

            // Block (hold)
            BlockHeld = Input.GetKey(blockKey);

            // Lock-on toggle
            LockOnToggle = Input.GetKeyDown(lockOnKey);

            // Switch target
            SwitchTargetDirection = 0;
            if (Input.GetKeyDown(switchTargetLeftKey)) SwitchTargetDirection = -1;
            if (Input.GetKeyDown(switchTargetRightKey)) SwitchTargetDirection = 1;
        }

        /// <summary>
        /// Consume attack input (sau khi đã xử lý)
        /// </summary>
        public void ConsumeAttackInput()
        {
            AttackInput = false;
            attackBufferTimer = 0f;
        }

        public void ConsumeHeavyAttackInput()
        {
            HeavyAttackInput = false;
            heavyAttackBufferTimer = 0f;
        }

        public void ConsumeLockOnInput()
        {
            LockOnToggle = false;
        }
    }
}
