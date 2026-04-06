using UnityEngine;
using DungeonSystem.Core;

namespace DungeonSystem.Runtime
{
    /// <summary>
    /// A connection point on a room's edge. Can be a door (open), wall, locked, or hidden.
    /// Attach to a child GameObject positioned at the room's edge.
    /// </summary>
    public class DoorSocket : MonoBehaviour
    {
        [Header("Socket Identity")]
        public Direction socketDirection;
        public Vector2Int cellOffset;           // Which cell of a multi-cell room this socket belongs to

        [Header("Visual States (assign in prefab)")]
        public GameObject openState;            // Doorway / archway
        public GameObject wallState;            // Solid wall segment
        public GameObject lockedState;          // Locked door visual
        public GameObject hiddenState;          // Secret door (looks like wall, but interactable)

        [Header("Runtime")]
        [SerializeField] private DoorState _currentState = DoorState.Walled;
        public DoorState CurrentState => _currentState;

        /// <summary>
        /// Connected room on the other side (null if walled).
        /// </summary>
        public RoomInstance ConnectedRoom { get; set; }

        public void SetState(DoorState state)
        {
            _currentState = state;

            if (openState != null)   openState.SetActive(state == DoorState.Open);
            if (wallState != null)   wallState.SetActive(state == DoorState.Walled);
            if (lockedState != null) lockedState.SetActive(state == DoorState.Locked);
            if (hiddenState != null) hiddenState.SetActive(state == DoorState.Hidden);
        }

        /// <summary>
        /// Quick helper: connected and not locked = open.
        /// </summary>
        public void SetConnected(bool isConnected, bool isLocked = false, bool isSecret = false)
        {
            if (!isConnected)
                SetState(DoorState.Walled);
            else if (isSecret)
                SetState(DoorState.Hidden);
            else if (isLocked)
                SetState(DoorState.Locked);
            else
                SetState(DoorState.Open);
        }
    }
}
