using UnityEngine;
using BillInspector;

namespace RPGModular
{
    public class PlayerInputHandler : MonoBehaviour
    {
        [BillTitle("Player Input Handler")]
        [BillFoldoutGroup("Key Bindings")]
        [SerializeField] private KeyCode attackKey = KeyCode.Mouse0;
        [BillFoldoutGroup("Key Bindings")]
        [SerializeField] private KeyCode heavyAttackKey = KeyCode.Mouse1;
        [BillFoldoutGroup("Key Bindings")]
        [SerializeField] private KeyCode blockKey = KeyCode.Q;
        [BillFoldoutGroup("Key Bindings")]
        [SerializeField] private KeyCode lockOnKey = KeyCode.Tab;
        [BillFoldoutGroup("Key Bindings")]
        [SerializeField] private KeyCode switchTargetLeftKey = KeyCode.LeftBracket;
        [BillFoldoutGroup("Key Bindings")]
        [SerializeField] private KeyCode switchTargetRightKey = KeyCode.RightBracket;
        [BillFoldoutGroup("Key Bindings")]
        [SerializeField] private KeyCode sprintKey = KeyCode.LeftShift;
        [BillFoldoutGroup("Key Bindings")]
        [SerializeField] private KeyCode jumpKey = KeyCode.Space;
        [BillFoldoutGroup("Key Bindings")]
        [SerializeField] private KeyCode dashKey = KeyCode.LeftControl;
        [BillFoldoutGroup("Key Bindings")]
        [SerializeField] private KeyCode interactKey = KeyCode.F;

        [BillBoxGroup("Buffer")]
        [BillSlider(0.05f, 0.5f), BillSuffix("s")]
        [SerializeField] private float inputBufferTime = 0.15f;
        [BillBoxGroup("Buffer")]
        [BillSlider(0.1f, 0.5f), BillSuffix("s")]
        [SerializeField] private float doubleTapWindow = 0.25f;

        private float attackBufferTimer;
        private float heavyAttackBufferTimer;
        private float jumpBufferTimer;
        private float dashBufferTimer;

        private float lastDodgeTapTime;
        private Vector2 lastDodgeTapDirection;

        public Vector2 MoveInput { get; private set; }
        public Vector2 RawMoveInput { get; private set; }
        public bool AttackInput { get; private set; }
        public bool HeavyAttackInput { get; private set; }
        public bool BlockHeld { get; private set; }
        public bool LockOnToggle { get; private set; }
        public int SwitchTargetDirection { get; private set; }
        public bool SprintHeld { get; private set; }
        public bool JumpInput { get; private set; }
        public bool DashInput { get; private set; }
        public bool InteractInput { get; private set; }
        public bool DoubleTapDodge { get; private set; }
        public Vector2 DodgeDirection { get; private set; }
        public Vector2 MouseDelta { get; private set; }
        public bool MouseRightHeld { get; private set; }

        private void Update()
        {
            RawMoveInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            MoveInput = RawMoveInput.normalized;
            SprintHeld = Input.GetKey(sprintKey);
            BlockHeld = Input.GetKey(blockKey);
            MouseRightHeld = Input.GetMouseButton(1);
            MouseDelta = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
            InteractInput = Input.GetKeyDown(interactKey);

            if (Input.GetKeyDown(attackKey))
                attackBufferTimer = inputBufferTime;
            attackBufferTimer -= Time.deltaTime;
            AttackInput = attackBufferTimer > 0f;

            if (Input.GetKeyDown(heavyAttackKey))
                heavyAttackBufferTimer = inputBufferTime;
            heavyAttackBufferTimer -= Time.deltaTime;
            HeavyAttackInput = heavyAttackBufferTimer > 0f;

            if (Input.GetKeyDown(jumpKey))
                jumpBufferTimer = inputBufferTime;
            jumpBufferTimer -= Time.deltaTime;
            JumpInput = jumpBufferTimer > 0f;

            if (Input.GetKeyDown(dashKey))
                dashBufferTimer = inputBufferTime;
            dashBufferTimer -= Time.deltaTime;
            DashInput = dashBufferTimer > 0f;

            LockOnToggle = Input.GetKeyDown(lockOnKey);

            SwitchTargetDirection = 0;
            if (Input.GetKeyDown(switchTargetLeftKey)) SwitchTargetDirection = -1;
            if (Input.GetKeyDown(switchTargetRightKey)) SwitchTargetDirection = 1;

            DetectDoubleTapDodge();
        }

        private void DetectDoubleTapDodge()
        {
            DoubleTapDodge = false;
            DodgeDirection = Vector2.zero;

            Vector2 currentDir = Vector2.zero;
            if (Input.GetKeyDown(KeyCode.W)) currentDir = Vector2.up;
            else if (Input.GetKeyDown(KeyCode.S)) currentDir = Vector2.down;
            else if (Input.GetKeyDown(KeyCode.A)) currentDir = Vector2.left;
            else if (Input.GetKeyDown(KeyCode.D)) currentDir = Vector2.right;

            if (currentDir == Vector2.zero) return;

            if (Time.time - lastDodgeTapTime < doubleTapWindow && currentDir == lastDodgeTapDirection)
            {
                DoubleTapDodge = true;
                DodgeDirection = currentDir;
                lastDodgeTapTime = 0f;
                lastDodgeTapDirection = Vector2.zero;
            }
            else
            {
                lastDodgeTapTime = Time.time;
                lastDodgeTapDirection = currentDir;
            }
        }

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

        public void ConsumeJumpInput()
        {
            JumpInput = false;
            jumpBufferTimer = 0f;
        }

        public void ConsumeDashInput()
        {
            DashInput = false;
            dashBufferTimer = 0f;
        }

        public void ConsumeLockOnInput()
        {
            LockOnToggle = false;
        }

        public void ConsumeDoubleTapDodge()
        {
            DoubleTapDodge = false;
            DodgeDirection = Vector2.zero;
        }
    }
}
