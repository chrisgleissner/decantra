/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Decantra.Presentation
{
    public sealed class AudioManager : MonoBehaviour
    {
        private const int SourcePoolSize = 8;
        private const float SegmentFadeSeconds = 0.01f;
        private const float SegmentFadeMinSeconds = 0.005f;
        private const float SegmentFadeMaxSeconds = 0.015f;

        private readonly Dictionary<int, AudioClip> _safeClipCache = new Dictionary<int, AudioClip>();
        private readonly Dictionary<int, ClipSampleData> _clipSampleCache = new Dictionary<int, ClipSampleData>();
        private readonly Dictionary<AudioSource, Coroutine> _segmentRoutines = new Dictionary<AudioSource, Coroutine>();

        private AudioSource[] _sources;
        private AudioClip[] _pourClips;
        private AudioClip _selectedPourClip;
        private int _selectedPourClipIndex = -1;
        private AudioClip _levelCompleteClip;
        private AudioClip _buttonClickClip;
        private AudioClip _bottleFullClip;
        private AudioClip _stageUnlockedClip;

        private bool _isEnabled = true;
        private float _volume01 = 1f;
        private int _nextSourceIndex;

        private struct ClipSampleData
        {
            public float[] Samples;
            public int Channels;
            public int Frequency;
        }

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

            _buttonClickClip = LoadClip("Sound/button-click");
            _levelCompleteClip = LoadClip("Sound/level-complete");
            _bottleFullClip = LoadClip("Sound/bottle-full");
            _stageUnlockedClip = LoadClip("Sound/stage-unlocked");

            var pours = new List<AudioClip>(2);
            var pourA = LoadClip("Sound/liquid-pour");
            var pourB = LoadClip("Sound/liquid-pour2");
            if (pourA != null) pours.Add(pourA);
            if (pourB != null) pours.Add(pourB);
            _pourClips = pours.ToArray();
            _selectedPourClip = _pourClips.Length > 0 ? _pourClips[0] : null;
            _selectedPourClipIndex = _selectedPourClip != null ? 0 : -1;

            ApplyAudioState();
        }

        private void OnDestroy()
        {
            foreach (var pair in _segmentRoutines)
            {
                if (pair.Value != null)
                {
                    StopCoroutine(pair.Value);
                }
            }
            _segmentRoutines.Clear();

            foreach (var pair in _safeClipCache)
            {
                if (pair.Value != null)
                {
                    Destroy(pair.Value);
                }
            }
            _safeClipCache.Clear();
            _clipSampleCache.Clear();
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

        public void SelectPourClipForLevel(int levelIndex, int seed)
        {
            if (_pourClips == null || _pourClips.Length == 0)
            {
                _selectedPourClip = null;
                _selectedPourClipIndex = -1;
                return;
            }

            var previousState = Random.state;
            Random.InitState(HashLevelSeed(levelIndex, seed));
            int selected = Random.Range(0, _pourClips.Length);
            Random.state = previousState;

            _selectedPourClipIndex = selected;
            _selectedPourClip = _pourClips[selected];
        }

        public void PlayPour(float fillRatio)
        {
            PlayPourSegment(0f, Mathf.Clamp01(fillRatio));
        }

        public void PlayPourSegment(float previousFillRatio, float newFillRatio)
        {
            if (!_isEnabled) return;

            var clip = _selectedPourClip != null ? _selectedPourClip : (_pourClips != null && _pourClips.Length > 0 ? _pourClips[0] : null);
            if (clip == null) return;

            float start = Mathf.Clamp01(previousFillRatio);
            float end = Mathf.Clamp01(newFillRatio);
            if (end <= start) return;

            PlayTransientSegment(clip, start, end, 0.55f);
        }

        public void PlayLevelComplete()
        {
            if (!_isEnabled || _levelCompleteClip == null) return;
            PlayTransient(_levelCompleteClip, 0.52f, 1f);
        }

        public void PlayButtonClick()
        {
            if (!_isEnabled || _buttonClickClip == null) return;
            PlayTransient(_buttonClickClip, 0.56f, 1f);
        }

        public void PlayBottleFull()
        {
            if (!_isEnabled || _bottleFullClip == null) return;
            PlayTransient(_bottleFullClip, 0.56f, 1f);
        }

        public void PlayStageUnlocked()
        {
            if (!_isEnabled || _stageUnlockedClip == null) return;
            PlayTransient(_stageUnlockedClip, 0.56f, 1f);
        }

        public void PlayTransient(AudioClip clip, float gain, float pitch = 1f)
        {
            if (!_isEnabled || clip == null || !HasAnySource()) return;

            var source = GetNextSource();
            if (source == null) return;

            var safeClip = EnsureSafeClip(clip);
            if (safeClip == null) return;

            if (_segmentRoutines.TryGetValue(source, out var existingRoutine) && existingRoutine != null)
            {
                StopCoroutine(existingRoutine);
                _segmentRoutines.Remove(source);
            }

            source.Stop();
            source.clip = safeClip;
            source.pitch = Mathf.Max(0.01f, pitch);
            source.volume = Mathf.Clamp01(gain) * _volume01;
            source.mute = !_isEnabled || _volume01 <= 0.0001f;
            source.time = 0f;
            source.Play();
        }

        private void PlayTransientSegment(AudioClip clip, float startRatio, float endRatio, float gain)
        {
            if (!_isEnabled || clip == null || !HasAnySource()) return;

            var source = GetNextSource();
            if (source == null) return;

            var safeClip = EnsureSafeClip(clip);
            if (safeClip == null || safeClip.length <= 0f) return;

            float clampedStartRatio = Mathf.Clamp01(startRatio);
            float clampedEndRatio = Mathf.Clamp01(endRatio);
            if (clampedEndRatio <= clampedStartRatio) return;

            float startTime = clampedStartRatio * safeClip.length;
            float endTime = clampedEndRatio * safeClip.length;
            if (endTime <= startTime) return;

            startTime = FindNearZeroStartTime(safeClip, startTime);
            float duration = Mathf.Max(0.001f, endTime - startTime);

            if (_segmentRoutines.TryGetValue(source, out var existingRoutine) && existingRoutine != null)
            {
                StopCoroutine(existingRoutine);
                _segmentRoutines.Remove(source);
            }

            source.Stop();
            source.clip = safeClip;
            source.pitch = 1f;
            source.volume = 0f;
            source.mute = !_isEnabled || _volume01 <= 0.0001f;
            source.time = Mathf.Clamp(startTime, 0f, Mathf.Max(0f, safeClip.length - 0.0001f));
            source.Play();

            var routine = StartCoroutine(PlaySegmentRoutine(source, Mathf.Clamp01(gain) * _volume01, duration));
            _segmentRoutines[source] = routine;
        }

        private IEnumerator PlaySegmentRoutine(AudioSource source, float targetVolume, float duration)
        {
            float clampedDuration = Mathf.Max(0.001f, duration);
            float fade = Mathf.Clamp(SegmentFadeSeconds, SegmentFadeMinSeconds, SegmentFadeMaxSeconds);
            fade = Mathf.Min(fade, clampedDuration * 0.5f);

            float elapsed = 0f;
            while (elapsed < clampedDuration)
            {
                if (source == null || !source.isPlaying)
                {
                    break;
                }

                float remaining = clampedDuration - elapsed;
                float inGain = fade > 0f ? Mathf.Clamp01(elapsed / fade) : 1f;
                float outGain = fade > 0f ? Mathf.Clamp01(remaining / fade) : 1f;
                source.volume = targetVolume * Mathf.Min(inGain, outGain);

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (source != null)
            {
                source.volume = 0f;
                source.Stop();
            }

            if (source != null)
            {
                _segmentRoutines.Remove(source);
            }
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

        private float FindNearZeroStartTime(AudioClip clip, float startTime)
        {
            if (clip == null || clip.frequency <= 0 || clip.samples <= 0)
            {
                return Mathf.Max(0f, startTime);
            }

            var data = GetClipSampleData(clip);
            if (data.Samples == null || data.Samples.Length == 0 || data.Channels <= 0)
            {
                return Mathf.Max(0f, startTime);
            }

            int startFrame = Mathf.Clamp(Mathf.RoundToInt(startTime * data.Frequency), 0, clip.samples - 1);
            int maxForwardFrames = Mathf.Max(1, Mathf.RoundToInt(data.Frequency * SegmentFadeMaxSeconds));
            int endFrame = Mathf.Min(clip.samples - 1, startFrame + maxForwardFrames);

            int bestFrame = startFrame;
            float bestAmplitude = float.MaxValue;
            for (int frame = startFrame; frame <= endFrame; frame++)
            {
                float amplitude = AverageAbsAmplitudeAtFrame(data.Samples, frame, data.Channels);
                if (amplitude < bestAmplitude)
                {
                    bestAmplitude = amplitude;
                    bestFrame = frame;
                }

                if (amplitude <= 0.0015f)
                {
                    return frame / (float)data.Frequency;
                }
            }

            return bestFrame / (float)data.Frequency;
        }

        private ClipSampleData GetClipSampleData(AudioClip clip)
        {
            int id = clip.GetInstanceID();
            if (_clipSampleCache.TryGetValue(id, out var cached) && cached.Samples != null)
            {
                return cached;
            }

            int sampleCount = clip.samples * clip.channels;
            if (sampleCount <= 0)
            {
                return default;
            }

            var samples = new float[sampleCount];
            if (!clip.GetData(samples, 0))
            {
                return default;
            }

            var data = new ClipSampleData
            {
                Samples = samples,
                Channels = clip.channels,
                Frequency = clip.frequency
            };

            _clipSampleCache[id] = data;
            return data;
        }

        private static float AverageAbsAmplitudeAtFrame(float[] samples, int frame, int channels)
        {
            int offset = frame * channels;
            if (offset < 0 || offset >= samples.Length) return 0f;

            float sum = 0f;
            int count = 0;
            for (int i = 0; i < channels; i++)
            {
                int index = offset + i;
                if (index >= samples.Length) break;
                sum += Mathf.Abs(samples[index]);
                count++;
            }

            if (count <= 0) return 0f;
            return sum / count;
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

        private static AudioClip LoadClip(string resourcePath)
        {
            if (string.IsNullOrWhiteSpace(resourcePath)) return null;
            return Resources.Load<AudioClip>(resourcePath);
        }

        private static int HashLevelSeed(int levelIndex, int seed)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + Mathf.Max(1, levelIndex);
                hash = hash * 31 + seed;
                return hash;
            }
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
                int firstIndex = channel;
                int lastIndex = (frameCount - 1) * channels + channel;
                data[firstIndex] = 0f;
                data[lastIndex] = 0f;

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
