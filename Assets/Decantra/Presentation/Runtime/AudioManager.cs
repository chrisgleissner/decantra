/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System.Collections.Generic;
using UnityEngine;

namespace Decantra.Presentation
{
    public sealed class AudioManager : MonoBehaviour
    {
        private const int SourcePoolSize = 8;
        private const int PourClipSteps = 12;

        private readonly Dictionary<int, AudioClip> _safeClipCache = new Dictionary<int, AudioClip>();
        private readonly HashSet<int> _internallySafeClipIds = new HashSet<int>();
        private AudioSource[] _sources;
        private AudioClip[] _pourClips;
        private AudioClip _levelCompleteClip;
        private AudioClip _buttonClickClip;
        private bool _isEnabled = true;
        private float _volume01 = 1f;
        private int _nextSourceIndex;
        private uint _variationState = 0x9E3779B9u;

        public bool IsEnabled => _isEnabled;
        public float Volume01 => _volume01;

        private void Awake()
        {
            _sources = new AudioSource[SourcePoolSize];
            for (int i = 0; i < _sources.Length; i++)
            {
                try
                {
                    var source = gameObject.AddComponent<AudioSource>();
                    source.playOnAwake = false;
                    source.loop = false;
                    source.spatialBlend = 0f;
                    source.priority = 64;
                    source.volume = 0f;
                    _sources[i] = source;
                }
                catch
                {
                    _sources[i] = null;
                }
            }

            if (!HasAnySource())
            {
                _isEnabled = false;
                return;
            }

            _pourClips = CreatePourClips();
            _levelCompleteClip = CreateLevelCompleteClip();
            _buttonClickClip = CreateButtonClickClip();
            RegisterInternallySafeClip(_levelCompleteClip);
            RegisterInternallySafeClip(_buttonClickClip);
            if (_pourClips != null)
            {
                for (int i = 0; i < _pourClips.Length; i++)
                {
                    RegisterInternallySafeClip(_pourClips[i]);
                }
            }

            ApplyAudioState();
        }

        public void SetEnabled(bool enabled)
        {
            _isEnabled = enabled;
            ApplyAudioState();
        }

        public void SetVolume01(float volume01)
        {
            _volume01 = Mathf.Clamp01(volume01);
            ApplyAudioState();
        }

        public void PlayPour(float fillRatio)
        {
            if (!_isEnabled || _pourClips == null || _pourClips.Length == 0) return;
            float clampedFill = Mathf.Clamp01(fillRatio);
            int index = Mathf.Clamp(Mathf.RoundToInt(clampedFill * (_pourClips.Length - 1)), 0, _pourClips.Length - 1);
            float gain = 0.55f + (1f - clampedFill) * 0.25f;
            PlayTransient(_pourClips[index], gain, 1f + NextSignedJitter(0.02f));
        }

        public void PlayLevelComplete()
        {
            if (!_isEnabled || _levelCompleteClip == null) return;
            PlayTransient(_levelCompleteClip, 0.52f, 1f + NextSignedJitter(0.015f));
        }

        public void PlayButtonClick()
        {
            if (!_isEnabled || _buttonClickClip == null) return;
            PlayTransient(_buttonClickClip, 0.56f, 0.96f + NextSignedJitter(0.01f));
        }

        public void PlayTransient(AudioClip clip, float gain, float pitch = 1f)
        {
            if (!_isEnabled || clip == null || !HasAnySource()) return;

            var source = GetNextSource();
            if (source == null) return;

            var safeClip = EnsureSafeClip(clip);
            if (safeClip == null) return;

            source.Stop();
            source.clip = safeClip;
            source.pitch = pitch;
            source.volume = Mathf.Clamp01(gain) * _volume01;
            source.mute = !_isEnabled || _volume01 <= 0.0001f;
            source.Play();
        }

        private void ApplyAudioState()
        {
            if (_sources == null) return;
            bool mute = !_isEnabled || _volume01 <= 0.0001f;
            for (int i = 0; i < _sources.Length; i++)
            {
                var source = _sources[i];
                if (source == null) continue;
                source.mute = mute;
            }
        }

        private AudioClip EnsureSafeClip(AudioClip clip)
        {
            if (clip == null) return null;
            int id = clip.GetInstanceID();
            if (_internallySafeClipIds.Contains(id))
            {
                return clip;
            }
            if (_safeClipCache.TryGetValue(id, out var cached) && cached != null)
            {
                return cached;
            }

            int sampleCount = clip.samples * clip.channels;
            if (sampleCount <= 0)
            {
                return clip;
            }

            var data = new float[sampleCount];
            if (!clip.GetData(data, 0))
            {
                return clip;
            }

            HardenSampleData(data, clip.frequency, clip.channels, 0.006f, 0.012f);
            var safe = AudioClip.Create($"{clip.name}_Safe", clip.samples, clip.channels, clip.frequency, false);
            if (safe == null || !safe.SetData(data, 0))
            {
                return clip;
            }

            _safeClipCache[id] = safe;
            return safe;
        }

        private void RegisterInternallySafeClip(AudioClip clip)
        {
            if (clip == null) return;
            // Generated clips are hardened at creation and should skip EnsureSafeClip reprocessing.
            _internallySafeClipIds.Add(clip.GetInstanceID());
        }

        private bool HasAnySource()
        {
            if (_sources == null) return false;
            for (int i = 0; i < _sources.Length; i++)
            {
                if (_sources[i] != null) return true;
            }
            return false;
        }

        private AudioSource GetNextSource()
        {
            if (_sources == null || _sources.Length == 0)
            {
                return null;
            }

            for (int i = 0; i < _sources.Length; i++)
            {
                int index = (_nextSourceIndex + i) % _sources.Length;
                if (_sources[index] == null) continue;
                _nextSourceIndex = (index + 1) % _sources.Length;
                return _sources[index];
            }

            return null;
        }

        private AudioClip[] CreatePourClips()
        {
            var clips = new AudioClip[PourClipSteps];
            for (int i = 0; i < clips.Length; i++)
            {
                float fillRatio = i / (float)(clips.Length - 1);
                clips[i] = CreatePourClip(fillRatio, i);
            }
            return clips;
        }

        private static float NextNoiseSample(ref uint state)
        {
            state = state * 1664525u + 1013904223u;
            return ((state >> 8) * (1f / 8388608f)) * 2f - 1f;
        }

        private static float OnePoleCoefficient(float cutoffHz, int sampleRate)
        {
            float clampedCutoff = Mathf.Clamp(cutoffHz, 10f, sampleRate * 0.45f);
            return 1f - Mathf.Exp(-2f * Mathf.PI * clampedCutoff / sampleRate);
        }

        private static void GetBandPassCoefficients(float centerFrequency, float q, int sampleRate, out float b0, out float b1, out float b2, out float a1, out float a2)
        {
            float clampedFrequency = Mathf.Clamp(centerFrequency, 30f, sampleRate * 0.45f);
            float clampedQ = Mathf.Clamp(q, 1f, 8f);
            float omega = 2f * Mathf.PI * clampedFrequency / sampleRate;
            float sin = Mathf.Sin(omega);
            float cos = Mathf.Cos(omega);
            float alpha = sin / (2f * clampedQ);

            float a0 = 1f + alpha;
            b0 = alpha / a0;
            b1 = 0f;
            b2 = -alpha / a0;
            a1 = -2f * cos / a0;
            a2 = (1f - alpha) / a0;
        }

        private AudioClip CreatePourClip(float fillRatio, int variant)
        {
            const int sampleRate = 44100;
            const float duration = 0.24f;
            int samples = Mathf.CeilToInt(sampleRate * duration);
            if (samples <= 0)
            {
                return null;
            }

            var clip = AudioClip.Create("Pour", samples, 1, sampleRate, false);
            if (clip == null)
            {
                return null;
            }

            var data = new float[samples];
            float clampedFill = Mathf.Clamp01(fillRatio);
            float airRatio = 1f - clampedFill;
            // Hollow cavity resonance rises as the target bottle fills up.
            float resonanceFrequency = Mathf.Lerp(240f, 1050f, clampedFill);
            float noiseGain = 0.8f - 0.3f * clampedFill;
            float resonanceGain = airRatio * 0.6f;

            float lpFast = 0f;
            float lpSlow = 0f;
            float fastCoeff = OnePoleCoefficient(2500f, sampleRate);
            float slowCoeff = OnePoleCoefficient(300f, sampleRate);

            float b0 = 0f;
            float b1 = 0f;
            float b2 = 0f;
            float a1 = 0f;
            float a2 = 0f;
            float x1 = 0f;
            float x2 = 0f;
            float y1 = 0f;
            float y2 = 0f;

            uint noiseState = 0xA511E9B3u + (uint)variant * 1103515245u;
            float phaseA = 0.47f + variant * 0.31f;
            float phaseB = 1.29f + variant * 0.23f;

            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)sampleRate;
                if ((i & 127) == 0)
                {
                    // Deterministic ±2% center-frequency modulation to avoid tonal fatigue.
                    float jitter = 1f + 0.02f * Mathf.Sin(2f * Mathf.PI * 2.1f * t + phaseA);
                    GetBandPassCoefficients(resonanceFrequency * jitter, 3f, sampleRate, out b0, out b1, out b2, out a1, out a2);
                }

                float white = NextNoiseSample(ref noiseState);
                lpFast += fastCoeff * (white - lpFast);
                lpSlow += slowCoeff * (white - lpSlow);
                float turbulence = lpFast - lpSlow;

                float y = b0 * white + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
                x2 = x1;
                x1 = white;
                y2 = y1;
                y1 = y;

                float ampJitter = 1f + 0.05f * Mathf.Sin(2f * Mathf.PI * 3.1f * t + phaseB);
                data[i] = (turbulence * noiseGain + y * resonanceGain) * 0.62f * ampJitter;
            }

            HardenSampleData(data, sampleRate, 1, 0.007f, 0.015f);

            if (!clip.SetData(data, 0))
            {
                return null;
            }
            return clip;
        }

        private static AudioClip CreateLevelCompleteClip()
        {
            const int sampleRate = 44100;
            const float duration = 0.18f;
            int samples = Mathf.CeilToInt(sampleRate * duration);
            if (samples <= 0)
            {
                return null;
            }

            var clip = AudioClip.Create("LevelComplete", samples, 1, sampleRate, false);
            if (clip == null)
            {
                return null;
            }

            var data = new float[samples];
            uint noiseState = 0xC0FFEE12u;
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)sampleRate;
                float attack = 1f - Mathf.Exp(-t * 120f);
                float decay = Mathf.Exp(-t * 22f);
                float env = attack * decay;
                float noise = NextNoiseSample(ref noiseState);
                float shimmerA = Mathf.Sin(2f * Mathf.PI * 760f * t) * 0.12f;
                float shimmerB = Mathf.Sin(2f * Mathf.PI * 1140f * t) * 0.08f;
                data[i] = (noise * 0.2f + shimmerA + shimmerB) * 0.35f * env;
            }

            HardenSampleData(data, sampleRate, 1, 0.006f, 0.012f);

            if (!clip.SetData(data, 0))
            {
                return null;
            }
            return clip;
        }

        private static AudioClip CreateButtonClickClip()
        {
            const int sampleRate = 44100;
            const float duration = 0.11f;
            int samples = Mathf.CeilToInt(sampleRate * duration);
            if (samples <= 0)
            {
                return null;
            }

            var clip = AudioClip.Create("ButtonClick", samples, 1, sampleRate, false);
            if (clip == null)
            {
                return null;
            }

            var data = new float[samples];
            const float baseFreq = 210f;
            const float bodyFreq = 290f;
            uint noiseState = 0x1BADB002u;
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)sampleRate;
                float fastAttack = 1f - Mathf.Exp(-t * 180f);
                float decay = Mathf.Exp(-t * 22f);
                float env = fastAttack * decay;

                float thump = Mathf.Sin(2f * Mathf.PI * baseFreq * t);
                float body = Mathf.Sin(2f * Mathf.PI * bodyFreq * t) * 0.45f;
                float texture = NextNoiseSample(ref noiseState) * 0.05f;

                data[i] = (thump * 0.72f + body * 0.28f + texture) * 0.2f * env;
            }

            HardenSampleData(data, sampleRate, 1, 0.005f, 0.01f);

            if (!clip.SetData(data, 0))
            {
                return null;
            }
            return clip;
        }

        private float NextSignedJitter(float amount)
        {
            _variationState = _variationState * 1664525u + 1013904223u;
            float normalized = ((_variationState >> 8) * (1f / 8388608f)) * 2f - 1f;
            return normalized * amount;
        }

        public static void HardenSampleData(float[] data, int sampleRate, int channels, float attackDuration, float releaseDuration)
        {
            if (data == null || data.Length == 0 || sampleRate <= 0 || channels <= 0)
            {
                return;
            }

            int frameCount = data.Length / channels;
            if (frameCount <= 0)
            {
                return;
            }

            int attackSamples = Mathf.Max(1, Mathf.RoundToInt(sampleRate * Mathf.Clamp(attackDuration, 0.005f, 0.01f)));
            int releaseSamples = Mathf.Max(1, Mathf.RoundToInt(sampleRate * Mathf.Clamp(releaseDuration, 0.005f, 0.02f)));

            // Attack/release envelope suppresses start/end discontinuities that cause clicks.
            for (int frame = 0; frame < frameCount; frame++)
            {
                float attack = Mathf.Clamp01(frame / (float)attackSamples);
                float release = Mathf.Clamp01((frameCount - 1 - frame) / (float)releaseSamples);
                float env = attack * release;
                int offset = frame * channels;
                for (int channel = 0; channel < channels; channel++)
                {
                    data[offset + channel] *= env;
                }
            }

            for (int channel = 0; channel < channels; channel++)
            {
                // Hard-zero boundaries guarantee click-free clip edges.
                int firstIndex = channel;
                int lastIndex = (frameCount - 1) * channels + channel;
                data[firstIndex] = 0f;
                data[lastIndex] = 0f;

                // Mean subtraction removes DC bias and lowers low-frequency thumps.
                // Compute and apply DC offset only over interior samples so that
                // the zeroed boundaries remain unchanged.
                if (frameCount > 2)
                {
                    float sum = 0f;
                    for (int frame = 1; frame < frameCount - 1; frame++)
                    {
                        sum += data[frame * channels + channel];
                    }

                    float mean = sum / (frameCount - 2);
                    for (int frame = 1; frame < frameCount - 1; frame++)
                    {
                        int index = frame * channels + channel;
                        data[index] = Mathf.Clamp(data[index] - mean, -1f, 1f);
                    }
                }
            }
        }
    }
}
