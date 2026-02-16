/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using UnityEngine;

namespace Decantra.Presentation
{
    public sealed class AudioManager : MonoBehaviour
    {
        private AudioSource _audioSource;
        private AudioClip _pourClip;
        private AudioClip _levelCompleteClip;
        private AudioClip _buttonClickClip;
        private bool _isEnabled = true;
        private float _volume01 = 1f;

        public bool IsEnabled => _isEnabled;
        public float Volume01 => _volume01;

        private void Awake()
        {
            _audioSource = gameObject.GetComponent<AudioSource>();
            if (_audioSource == null)
            {
                try
                {
                    _audioSource = gameObject.AddComponent<AudioSource>();
                }
                catch
                {
                    _audioSource = null;
                }
            }

            if (_audioSource == null)
            {
                _isEnabled = false;
                return;
            }

            _audioSource.playOnAwake = false;
            _audioSource.loop = false;
            _audioSource.spatialBlend = 0f;
            _audioSource.priority = 64;

            _pourClip = CreatePourClip();
            _levelCompleteClip = CreateLevelCompleteClip();
            _buttonClickClip = CreateButtonClickClip();

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
            if (!_isEnabled || _audioSource == null || _pourClip == null) return;
            _audioSource.pitch = Mathf.Lerp(0.8f, 1.2f, Mathf.Clamp01(fillRatio));
            _audioSource.PlayOneShot(_pourClip, Mathf.Lerp(0.6f, 1f, 1f - Mathf.Clamp01(fillRatio)));
        }

        public void PlayLevelComplete()
        {
            if (!_isEnabled || _audioSource == null || _levelCompleteClip == null) return;
            _audioSource.pitch = 1f;
            _audioSource.PlayOneShot(_levelCompleteClip, 1f);
        }

        public void PlayButtonClick()
        {
            if (!_isEnabled || _audioSource == null || _buttonClickClip == null) return;
            _audioSource.pitch = 0.96f;
            _audioSource.PlayOneShot(_buttonClickClip, 0.62f);
        }

        private void ApplyAudioState()
        {
            if (_audioSource == null) return;
            _audioSource.mute = !_isEnabled || _volume01 <= 0.0001f;
            _audioSource.volume = _volume01;
        }

        private static AudioClip CreatePourClip()
        {
            const int sampleRate = 44100;
            const float duration = 0.25f;
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
            const float freq = 220f;
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)sampleRate;
                float noise = Mathf.PerlinNoise(t * 18f, 0.1f) - 0.5f;
                float sine = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.15f;
                float env = Mathf.Exp(-t * 8f);
                data[i] = (noise * 0.3f + sine) * env;
            }

            if (!clip.SetData(data, 0))
            {
                return null;
            }
            return clip;
        }

        private static AudioClip CreateLevelCompleteClip()
        {
            const int sampleRate = 44100;
            const float duration = 0.42f;
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
            const float baseFreq = 523.25f;
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)sampleRate;
                float env = Mathf.Exp(-t * 5.5f);
                float toneA = Mathf.Sin(2f * Mathf.PI * baseFreq * t);
                float toneB = Mathf.Sin(2f * Mathf.PI * baseFreq * 1.5f * t) * 0.65f;
                float toneC = Mathf.Sin(2f * Mathf.PI * baseFreq * 2f * t) * 0.35f;
                data[i] = (toneA + toneB + toneC) * 0.15f * env;
            }

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
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)sampleRate;
                float fastAttack = 1f - Mathf.Exp(-t * 180f);
                float decay = Mathf.Exp(-t * 22f);
                float env = fastAttack * decay;

                float thump = Mathf.Sin(2f * Mathf.PI * baseFreq * t);
                float body = Mathf.Sin(2f * Mathf.PI * bodyFreq * t) * 0.45f;
                float texture = (Mathf.PerlinNoise(t * 42f, 0.37f) - 0.5f) * 0.08f;

                data[i] = (thump * 0.72f + body * 0.28f + texture) * 0.2f * env;
            }

            if (!clip.SetData(data, 0))
            {
                return null;
            }
            return clip;
        }
    }
}
