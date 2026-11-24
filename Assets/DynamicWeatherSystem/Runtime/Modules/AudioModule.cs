using UnityEngine;

namespace DynamicWeatherSystem
{
    /// <summary>
    /// Module that manages ambient weather audio using a dual-AudioSource crossfade.
    ///
    /// Uses two internal AudioSources:
    ///   - Source A plays the outgoing clip (fade out)
    ///   - Source B plays the incoming clip (fade in)
    ///
    /// Setup:
    ///   1. Add this component as a child of the GameObject that holds WeatherManager.
    ///   2. Assign looping AudioClip assets in each WeatherStateData.
    ///   3. The crossfade syncs automatically with the transition duration.
    ///
    /// No additional configuration is required.
    /// </summary>
    [AddComponentMenu("Dynamic Weather System/Modules/Audio Module")]
    public class AudioModule : WeatherModule
    {
        [Header("Volume")]
        [Tooltip("Global volume multiplier for all audio in this module. " +
                 "Useful for balancing weather audio against other game sounds.")]
        [SerializeField, Range(0f, 1f)] private float masterVolume = 1f;

        // _sourceA: outgoing clip (fade out)
        // _sourceB: incoming clip (fade in)
        private AudioSource _sourceA;
        private AudioSource _sourceB;

        // Track the current (from, to) clip pair to detect new transitions
        private AudioClip _trackedFrom;
        private AudioClip _trackedTo;

        // --- Lifecycle ---

        private void Awake()
        {
            _sourceA = CreateAudioSource("AudioSource_A");
            _sourceB = CreateAudioSource("AudioSource_B");
        }

        // --- WeatherModule ---

        public override void Blend(WeatherStateData from, WeatherStateData to, float t)
        {
            bool newTransition = from.ambientClip != _trackedFrom
                              || to.ambientClip   != _trackedTo;

            if (newTransition)
            {
                InitializeCrossfade(from.ambientClip, from.ambientVolume, to.ambientClip);
                _trackedFrom = from.ambientClip;
                _trackedTo   = to.ambientClip;
            }

            if (from.ambientClip == to.ambientClip)
            {
                // Same clip: interpolate volume on sourceB only; sourceA is silenced
                _sourceA.volume = 0f;
                _sourceB.volume = Mathf.Lerp(from.ambientVolume, to.ambientVolume, t) * masterVolume;
            }
            else
            {
                // Different clips: full crossfade
                _sourceA.volume = Mathf.Lerp(from.ambientVolume, 0f, t) * masterVolume;
                _sourceB.volume = Mathf.Lerp(0f, to.ambientVolume, t)   * masterVolume;
            }
        }

        // --- Internal Logic ---

        /// <summary>
        /// Configures both AudioSources to begin a crossfade.
        /// Source A receives the outgoing clip at the starting volume.
        /// Source B receives the incoming clip, silent and ready to play.
        /// </summary>
        private void InitializeCrossfade(AudioClip fromClip, float fromVolume, AudioClip toClip)
        {
            // Source A: outgoing clip at starting volume
            SetupSource(_sourceA, fromClip, fromVolume * masterVolume);

            // Source B: incoming clip, silenced
            if (_sourceB.clip != toClip)
            {
                _sourceB.Stop();
                _sourceB.clip   = toClip;
                _sourceB.volume = 0f;

                if (toClip != null)
                    _sourceB.Play();
            }
            else if (toClip != null && !_sourceB.isPlaying)
            {
                _sourceB.volume = 0f;
                _sourceB.Play();
            }
        }

        /// <summary>
        /// Assigns a clip and volume to an AudioSource, restarting playback
        /// only if the clip changes. Avoids unnecessary loop restarts.
        /// </summary>
        private static void SetupSource(AudioSource source, AudioClip clip, float volume)
        {
            if (source.clip != clip)
            {
                source.Stop();
                source.clip = clip;

                if (clip != null)
                {
                    source.volume = volume;
                    source.Play();
                }
            }
            else if (clip != null)
            {
                source.volume = volume;

                if (!source.isPlaying)
                    source.Play();
            }
            else
            {
                // Clip is null — ensure the source is stopped
                source.Stop();
            }
        }

        private AudioSource CreateAudioSource(string sourceName)
        {
            var go = new GameObject(sourceName);
            go.transform.SetParent(transform);
            go.transform.localPosition = Vector3.zero;

            var src          = go.AddComponent<AudioSource>();
            src.loop         = true;
            src.playOnAwake  = false;
            src.spatialBlend = 0f;    // 2D audio — no spatial positioning
            src.priority     = 128;
            src.volume       = 0f;

            return src;
        }
    }
}
