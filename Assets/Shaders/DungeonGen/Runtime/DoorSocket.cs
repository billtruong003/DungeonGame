using UnityEngine;
using DungeonSystem.Core;

namespace DungeonSystem.Runtime
{
    public class DoorSocket : MonoBehaviour
    {
        [Header("Socket Identity")]
        public Direction socketDirection;
        public Vector2Int cellOffset;

        [Header("Visual States (assign in prefab)")]
        public GameObject openState;
        public GameObject wallState;
        public GameObject lockedState;
        public GameObject hiddenState;

        [Header("Runtime")]
        [SerializeField] private DoorState _currentState = DoorState.Walled;
        public DoorState CurrentState => _currentState;

        public RoomInstance ConnectedRoom { get; set; }

        public void SetState(DoorState state)
        {
            _currentState = state;
            if (openState != null)   openState.SetActive(state == DoorState.Open);
            if (wallState != null)   wallState.SetActive(state == DoorState.Walled);
            if (lockedState != null) lockedState.SetActive(state == DoorState.Locked);
            if (hiddenState != null) hiddenState.SetActive(state == DoorState.Hidden);
        }

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
