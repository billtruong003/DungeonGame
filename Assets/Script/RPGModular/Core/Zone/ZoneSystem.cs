using System;
using System.Collections.Generic;
using UnityEngine;
using BillGameCore;

namespace RPGModular
{
    public class ZoneSystem : MonoBehaviour
    {
        public static ZoneSystem Instance { get; private set; }

        public ZoneData CurrentZone { get; private set; }
        private HashSet<string> discoveredZones = new HashSet<string>();
        private ZoneData _pendingZone;
        private string _pendingSpawnID;

        public event Action<ZoneData> OnZoneEnter;
        public event Action<ZoneData> OnZoneExit;
        public event Action<ZoneData> OnZoneDiscovered;

        private void Awake() => Instance = this;
        private void OnDestroy() { if (Instance == this) Instance = null; }

        public void TravelTo(ZoneData zone, string spawnPointID = null)
        {
            if (zone == null) return;

            var oldZone = CurrentZone;
            if (oldZone != null) OnZoneExit?.Invoke(oldZone);

            CurrentZone = zone;

            // Load scene via Bill.Scene
            if (Bill.Scene != null && !string.IsNullOrEmpty(zone.sceneName))
            {
                _pendingZone = zone;
                _pendingSpawnID = spawnPointID;
                UnityEngine.SceneManagement.SceneManager.sceneLoaded += HandleSceneLoaded;
                Bill.Scene.Load(zone.sceneName);
            }
        }

        private void HandleSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= HandleSceneLoaded;
            if (_pendingZone != null)
                OnSceneLoaded(_pendingZone, _pendingSpawnID);
            _pendingZone = null;
            _pendingSpawnID = null;
        }

        private void OnSceneLoaded(ZoneData zone, string spawnPointID)
        {
            // Position player at spawn point
            if (zone.spawnPoints != null && Game.Player != null)
            {
                SpawnPoint sp = null;
                if (!string.IsNullOrEmpty(spawnPointID))
                {
                    foreach (var s in zone.spawnPoints)
                        if (s.spawnID == spawnPointID) { sp = s; break; }
                }
                sp ??= zone.spawnPoints.Length > 0 ? zone.spawnPoints[0] : null;

                if (sp != null)
                {
                    Game.Player.transform.position = sp.position;
                    Game.Player.transform.rotation = Quaternion.Euler(0, sp.yRotation, 0);
                }
            }

            // BGM
            if (Bill.Audio != null && !string.IsNullOrEmpty(zone.bgmKey))
                Bill.Audio.PlayMusic(zone.bgmKey, 1f);

            // Discover
            if (!discoveredZones.Contains(zone.zoneID))
            {
                discoveredZones.Add(zone.zoneID);
                OnZoneDiscovered?.Invoke(zone);
            }

            OnZoneEnter?.Invoke(zone);
        }

        public bool IsZoneDiscovered(ZoneData zone) =>
            zone != null && discoveredZones.Contains(zone.zoneID);

        public List<string> GetDiscoveredZoneIDs() => new List<string>(discoveredZones);
    }
}
