using UnityEngine;
using System.Collections.Generic;

namespace CleanRender
{
    /// <summary>
    /// Attach to the player (or any character) to interact with grass.
    /// Self-contained: pushes _InteractorPositions / _InteractorCount
    /// directly to global shader properties every frame.
    /// 
    /// Features:
    ///   - Auto-register on enable, unregister on disable
    ///   - Configurable bend radius and strength
    ///   - Speed-based radius scaling (running = wider bend)
    ///   - Optional dust/particle effect on movement
    ///   - Works with ToonGrass shader's _InteractorPositions array
    /// 
    /// Usage:
    ///   1. Add this component to your player GameObject
    ///   2. That's it - grass will bend when you walk through it
    ///   
    /// For NPCs/animals: add this component to them too, each gets a slot
    /// (up to 8 interactors supported simultaneously by the grass shader)
    /// </summary>
    [AddComponentMenu("CleanRender/Player Foliage Interactor")]
    public class PlayerFoliageInteractor : MonoBehaviour
    {
        private const int MAX_INTERACTORS = 8;

        [Header("Interaction Settings")]
        [Tooltip("Base radius of grass bending around this character")]
        [Range(0.5f, 5f)]
        public float bendRadius = 1.5f;

        [Tooltip("How strongly grass bends away")]
        [Range(0.1f, 3f)]
        public float bendStrength = 1.0f;

        [Tooltip("Scale radius based on movement speed")]
        public bool speedScaling = true;

        [Tooltip("Radius multiplier at max speed")]
        [Range(1f, 3f)]
        public float maxSpeedRadiusMultiplier = 1.5f;

        [Tooltip("Speed considered 'max' for radius scaling")]
        public float maxSpeed = 10f;

        [Header("Ground Detection")]
        [Tooltip("Offset interaction point downward (for characters with pivot at center)")]
        public float groundOffset = 0f;

        [Tooltip("Use a specific child transform as the interaction point (e.g., feet)")]
        public Transform overridePoint;

        [Header("Effects")]
        [Tooltip("Optional particle system to play when moving through grass")]
        public ParticleSystem grassParticles;

        [Tooltip("Minimum speed to trigger particles")]
        public float particleSpeedThreshold = 1f;

        private Vector3 _lastPosition;
        private float _currentSpeed;
        private float _currentRadius;
        private float _particleCooldown;

        // Static interactor registry
        private static readonly List<PlayerFoliageInteractor> s_Interactors = new List<PlayerFoliageInteractor>();
        private static readonly Vector4[] s_ShaderData = new Vector4[MAX_INTERACTORS];

        private static readonly int ID_InteractorPositions = Shader.PropertyToID("_InteractorPositions");
        private static readonly int ID_InteractorCount = Shader.PropertyToID("_InteractorCount");

        private void OnEnable()
        {
            if (s_Interactors.Count < MAX_INTERACTORS)
                s_Interactors.Add(this);

            _lastPosition = GetInteractionPoint();
            _currentRadius = bendRadius;
        }

        private void OnDisable()
        {
            s_Interactors.Remove(this);
            PushToShader();

            if (grassParticles != null && grassParticles.isPlaying)
                grassParticles.Stop();
        }

        private void Update()
        {
            Vector3 currentPos = GetInteractionPoint();

            _currentSpeed = Vector3.Distance(currentPos, _lastPosition) / Time.deltaTime;
            _lastPosition = currentPos;

            if (speedScaling)
            {
                float speedFactor = Mathf.Clamp01(_currentSpeed / maxSpeed);
                float targetRadius = bendRadius * Mathf.Lerp(1f, maxSpeedRadiusMultiplier, speedFactor);
                _currentRadius = Mathf.Lerp(_currentRadius, targetRadius, Time.deltaTime * 8f);
            }
            else
            {
                _currentRadius = bendRadius;
            }

            UpdateParticles();
            PushToShader();
        }

        public Vector3 GetInteractionPoint()
        {
            if (overridePoint != null)
                return overridePoint.position;

            return transform.position + Vector3.down * groundOffset;
        }

        public float GetEffectiveRadius()
        {
            return _currentRadius;
        }

        public Vector4 GetShaderData()
        {
            Vector3 pos = GetInteractionPoint();
            return new Vector4(pos.x, pos.y, pos.z, _currentRadius);
        }

        private static void PushToShader()
        {
            int count = Mathf.Min(s_Interactors.Count, MAX_INTERACTORS);

            for (int i = 0; i < MAX_INTERACTORS; i++)
            {
                s_ShaderData[i] = i < count ? s_Interactors[i].GetShaderData() : Vector4.zero;
            }

            Shader.SetGlobalVectorArray(ID_InteractorPositions, s_ShaderData);
            Shader.SetGlobalInt(ID_InteractorCount, count);
        }

        private void UpdateParticles()
        {
            if (grassParticles == null) return;

            _particleCooldown -= Time.deltaTime;

            if (_currentSpeed > particleSpeedThreshold && _particleCooldown <= 0f)
            {
                if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 2f))
                {
                    grassParticles.transform.position = hit.point;

                    if (!grassParticles.isPlaying)
                        grassParticles.Play();
                }
            }
            else if (_currentSpeed < particleSpeedThreshold * 0.5f)
            {
                if (grassParticles.isPlaying)
                    grassParticles.Stop();
            }
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 pos = Application.isPlaying ? GetInteractionPoint() : transform.position - Vector3.up * groundOffset;
            float radius = Application.isPlaying ? _currentRadius : bendRadius;

            Gizmos.color = new Color(0.3f, 0.9f, 0.2f, 0.2f);
            Gizmos.DrawSphere(pos, radius);
            Gizmos.color = new Color(0.3f, 0.9f, 0.2f, 0.6f);
            Gizmos.DrawWireSphere(pos, radius);

            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(pos, 0.1f);
        }
    }
}
