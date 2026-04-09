using UnityEngine;

namespace VRCore.Feedback
{
    [RequireComponent(typeof(AudioSource))]
    public class CollisionSound : MonoBehaviour
    {
        [Header("Clips")]
        [SerializeField] private AudioClip[] impactClips;
        [SerializeField] private AudioClip[] slideClips;

        [Header("Velocity")]
        [SerializeField] private float minVelocity = 0.3f;
        [SerializeField] private float maxVelocity = 5f;
        [SerializeField] private float minVolume = 0.05f;
        [SerializeField] private float maxVolume = 0.8f;

        [Header("Variation")]
        [SerializeField] private float pitchMin = 0.9f;
        [SerializeField] private float pitchMax = 1.1f;

        [Header("Cooldown")]
        [SerializeField] private float cooldown = 0.08f;

        [Header("Filter")]
        [SerializeField] private LayerMask collisionLayers = ~0;

        private AudioSource _source;
        private float _lastPlayTime;

        private void Awake()
        {
            _source = GetComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 1f;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (Time.time - _lastPlayTime < cooldown) return;
            if ((collisionLayers & (1 << collision.gameObject.layer)) == 0) return;

            float speed = collision.relativeVelocity.magnitude;
            if (speed < minVelocity) return;

            PlayImpact(speed);
        }

        public void PlayImpact(float velocity)
        {
            if (impactClips == null || impactClips.Length == 0) return;

            float normalizedVel = Mathf.InverseLerp(minVelocity, maxVelocity, velocity);
            float volume = Mathf.Lerp(minVolume, maxVolume, normalizedVel);
            float pitch = Random.Range(pitchMin, pitchMax);

            AudioClip clip = impactClips[Random.Range(0, impactClips.Length)];
            _source.pitch = pitch;
            _source.PlayOneShot(clip, volume);
            _lastPlayTime = Time.time;
        }

        public void PlayImpact() => PlayImpact(maxVelocity * 0.5f);

        public void PlaySlide(float velocity)
        {
            if (slideClips == null || slideClips.Length == 0) return;
            if (_source.isPlaying) return;

            float normalizedVel = Mathf.InverseLerp(minVelocity, maxVelocity, velocity);
            _source.clip = slideClips[Random.Range(0, slideClips.Length)];
            _source.volume = Mathf.Lerp(minVolume, maxVolume * 0.5f, normalizedVel);
            _source.loop = true;
            _source.Play();
        }

        public void StopSlide()
        {
            if (_source.loop)
            {
                _source.loop = false;
                _source.Stop();
            }
        }

        public void SetCooldown(float cd) => cooldown = cd;
        public void SetVolumeRange(float min, float max) { minVolume = min; maxVolume = max; }
    }
}
