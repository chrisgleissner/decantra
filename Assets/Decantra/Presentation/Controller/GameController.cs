using System.Collections;
using System.Collections.Generic;
using Decantra.Domain.Generation;
using Decantra.Domain.Model;
using Decantra.Domain.Persistence;
using Decantra.Domain.Scoring;
using Decantra.Domain.Solver;
using Decantra.App.Services;
using Decantra.Presentation.View;
using UnityEngine;

namespace Decantra.Presentation.Controller
{
    public sealed class GameController : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private List<BottleView> bottleViews = new List<BottleView>();
        [SerializeField] private HudView hudView;
        [SerializeField] private LevelCompleteBanner levelBanner;
        [SerializeField] private IntroBanner introBanner;

        [Header("Config")]
        [SerializeField] private int reverseMoves = 18;
        [SerializeField] private int movesAllowedPadding = 6;
        [SerializeField] private int baseScore = 100;

        private LevelState _state;
        private bool _inputLocked;
        private int _selectedIndex = -1;
        private bool _isCompleting;
        private int _currentLevel = 1;
        private int _currentSeed;
        private int _currentScore;
        private int _lastBonus;
        private LevelState _nextState;
        private int _nextLevel;
        private int _nextSeed;
        private Coroutine _precomputeRoutine;

        private BfsSolver _solver;
        private LevelGenerator _generator;
        private ProgressStore _progressStore;
        private ProgressData _progress;
        private SettingsStore _settingsStore;
        private bool _sfxEnabled = true;
        private AudioSource _audioSource;
        private AudioClip _pourClip;

        public bool IsInputLocked => _inputLocked;
        public bool IsSfxEnabled => _sfxEnabled;

        private void Awake()
        {
            _solver = new BfsSolver();
            _generator = new LevelGenerator(_solver);
            _progressStore = new ProgressStore();
            _settingsStore = new SettingsStore();
        }

        private void Start()
        {
            for (int i = 0; i < bottleViews.Count; i++)
            {
                bottleViews[i].Initialize(i);
            }

            Canvas.ForceUpdateCanvases();
            _progress = _progressStore.Load();
            _sfxEnabled = _settingsStore.LoadSfxEnabled();
            SetupAudio();

            _currentLevel = _progress.CurrentLevel > 0 ? _progress.CurrentLevel : 1;
            _currentSeed = _progress.CurrentSeed > 0 ? _progress.CurrentSeed : NextSeed(_currentLevel, 0);
            StartCoroutine(BeginSession());
        }

        public void LoadLevel(int levelIndex, int seed)
        {
            _currentLevel = Mathf.Max(1, levelIndex);
            _currentSeed = seed > 0 ? seed : NextSeed(_currentLevel, _currentSeed);
            _state = GenerateLevelWithRetry(_currentLevel, _currentSeed);
            _selectedIndex = -1;
            _isCompleting = false;
            StartPrecomputeNextLevel();
            Render();
        }

        public void OnBottleTapped(int index)
        {
            if (_inputLocked || _state == null) return;

            if (_selectedIndex < 0)
            {
                _selectedIndex = index;
                return;
            }

            if (_selectedIndex == index)
            {
                _selectedIndex = -1;
                return;
            }

            StartCoroutine(ApplyMoveWithAnimation(_selectedIndex, index));
            _selectedIndex = -1;
        }

        public int GetPourAmount(int sourceIndex, int targetIndex)
        {
            if (_state == null) return 0;
            if (sourceIndex < 0 || sourceIndex >= _state.Bottles.Count) return 0;
            if (targetIndex < 0 || targetIndex >= _state.Bottles.Count) return 0;
            return _state.Bottles[sourceIndex].MaxPourAmountInto(_state.Bottles[targetIndex]);
        }

        public bool TryStartMove(int sourceIndex, int targetIndex, out float duration)
        {
            duration = 0f;
            if (_inputLocked || _state == null) return false;
            if (sourceIndex == targetIndex) return false;

            int poured = GetPourAmount(sourceIndex, targetIndex);
            if (poured <= 0) return false;

            duration = Mathf.Max(0.2f, 0.12f * poured);
            _inputLocked = true;

            var sourceView = GetBottleView(sourceIndex);
            var targetView = GetBottleView(targetIndex);
            var color = _state.Bottles[sourceIndex].TopColor;

            PlayPourSfx(targetIndex, poured);

            StartCoroutine(AnimateMove(sourceIndex, targetIndex, poured, color, duration, sourceView, targetView));
            return true;
        }

        private IEnumerator ApplyMoveWithAnimation(int sourceIndex, int targetIndex)
        {
            _inputLocked = true;
            int poured;
            if (_state.TryApplyMove(sourceIndex, targetIndex, out poured))
            {
                float duration = Mathf.Max(0.2f, 0.12f * poured);
                yield return new WaitForSeconds(duration);
                Render();
            }
            _inputLocked = false;
        }

        private IEnumerator AnimateMove(int sourceIndex, int targetIndex, int poured, ColorId? color, float duration, BottleView sourceView, BottleView targetView)
        {
            float time = 0f;
            while (time < duration)
            {
                time += Time.deltaTime;
                float t = Mathf.Clamp01(time / duration);
                sourceView?.AnimateOutgoing(poured, t);
                if (color.HasValue)
                {
                    targetView?.AnimateIncoming(color.Value, poured, t);
                }
                yield return null;
            }

            int applied;
            _state.TryApplyMove(sourceIndex, targetIndex, out applied);
            Render();
            sourceView?.ClearOutgoing();
            targetView?.ClearIncoming();
            _inputLocked = false;
        }

        private BottleView GetBottleView(int index)
        {
            if (bottleViews == null) return null;
            if (index < 0 || index >= bottleViews.Count) return null;
            return bottleViews[index];
        }

        private void Render()
        {
            if (_state == null) return;

            if (bottleViews != null)
            {
                for (int i = 0; i < bottleViews.Count && i < _state.Bottles.Count; i++)
                {
                    var view = bottleViews[i];
                    if (view == null) continue;
                    view.Render(_state.Bottles[i]);
                }
            }

            if (hudView != null)
            {
                int filledUnits = 0;
                for (int i = 0; i < _state.Bottles.Count; i++)
                {
                    filledUnits += _state.Bottles[i].Count;
                }

                int bonus;
                int score = ScoreCalculator.CalculateScore(baseScore, filledUnits, _state.MovesUsed, _state.MovesAllowed, _state.OptimalMoves, out bonus);
                _lastBonus = bonus;
                _currentScore = score;
                if (_progress != null && score > _progress.HighScore)
                {
                    _progress.HighScore = score;
                    _progressStore.Save(_progress);
                }
                int highScore = _progress?.HighScore ?? 0;
                int maxLevel = _progress?.HighestUnlockedLevel ?? _currentLevel;
                hudView.Render(_state.LevelIndex, _state.MovesUsed, _state.MovesAllowed, _state.OptimalMoves, score, highScore, maxLevel);
            }

            if (_state.IsWin() && !_isCompleting)
            {
                StartCoroutine(HandleLevelComplete());
            }
        }

        private IEnumerator HandleLevelComplete()
        {
            _isCompleting = true;
            _inputLocked = true;

            int nextLevel = _currentLevel + 1;
            if (_progress != null)
            {
                _progress.HighestUnlockedLevel = Mathf.Max(_progress.HighestUnlockedLevel, nextLevel);
                _progress.CurrentLevel = nextLevel;
                _progress.CurrentSeed = NextSeed(nextLevel, _currentSeed);
                _progress.CurrentScore = _currentScore;
                _progressStore.Save(_progress);
            }

            bool finished = false;
            if (levelBanner != null)
            {
                levelBanner.Show(_currentLevel, _lastBonus, () => finished = true);
                while (!finished)
                {
                    yield return null;
                }
            }
            else
            {
                yield return new WaitForSeconds(0.5f);
            }

            if (_nextState != null && _nextLevel == nextLevel)
            {
                _state = _nextState;
                _currentLevel = _nextLevel;
                _currentSeed = _nextSeed;
                _nextState = null;
                _selectedIndex = -1;
                _isCompleting = false;
                StartPrecomputeNextLevel();
                Render();
            }
            else
            {
                LoadLevel(nextLevel, _progress?.CurrentSeed ?? NextSeed(nextLevel, _currentSeed));
            }
            _inputLocked = false;
        }

        private void StartPrecomputeNextLevel()
        {
            if (_precomputeRoutine != null)
            {
                StopCoroutine(_precomputeRoutine);
            }

            _nextLevel = _currentLevel + 1;
            _nextSeed = NextSeed(_nextLevel, _currentSeed);
            _nextState = GenerateLevelWithRetry(_nextLevel, _nextSeed, 6);
        }

        private LevelState GenerateLevelWithRetry(int level, int seed, int maxAttempts = 8)
        {
            int attempt = 0;
            int currentSeed = seed;
            int scaledReverse = ComputeReverseMoves(level);
            int scaledPadding = ComputeMovesPadding(level);

            while (attempt < maxAttempts)
            {
                try
                {
                    _currentSeed = currentSeed;
                    return _generator.Generate(currentSeed, level, scaledReverse, scaledPadding);
                }
                catch
                {
                    attempt++;
                    currentSeed = NextSeed(level, currentSeed + 31);
                }
            }

            return _generator.Generate(seed, level, Mathf.Max(6, scaledReverse - 6), scaledPadding + 2);
        }

        public void SetSfxEnabled(bool enabled)
        {
            _sfxEnabled = enabled;
            _settingsStore.SaveSfxEnabled(enabled);
        }

        private IEnumerator BeginSession()
        {
            _inputLocked = true;
            if (introBanner != null)
            {
                yield return introBanner.Play();
            }
            LoadLevel(_currentLevel, _currentSeed);
            _inputLocked = false;
        }

        private int ComputeReverseMoves(int level)
        {
            if (level <= 1) return 6;
            if (level <= 3) return 8 + level;
            if (level <= 8) return 10 + level * 2;
            return 20 + level * 2;
        }

        private int ComputeMovesPadding(int level)
        {
            if (level <= 2) return 6;
            if (level <= 6) return 5;
            return 4;
        }

        private void SetupAudio()
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.loop = false;
            _pourClip = CreatePourClip();
        }

        private void PlayPourSfx(int targetIndex, int amount)
        {
            if (!_sfxEnabled || _audioSource == null || _pourClip == null || _state == null) return;

            int targetFill = _state.Bottles[targetIndex].Count + amount;
            float ratio = Mathf.Clamp01(targetFill / (float)_state.Bottles[targetIndex].Capacity);
            _audioSource.pitch = Mathf.Lerp(0.8f, 1.2f, ratio);
            _audioSource.volume = Mathf.Lerp(0.35f, 0.7f, 1f - ratio);
            _audioSource.PlayOneShot(_pourClip);
        }

        private AudioClip CreatePourClip()
        {
            int sampleRate = 44100;
            float duration = 0.25f;
            int samples = Mathf.CeilToInt(sampleRate * duration);
            var clip = AudioClip.Create("Pour", samples, 1, sampleRate, false);

            float[] data = new float[samples];
            float freq = 220f;
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)sampleRate;
                float noise = Mathf.PerlinNoise(t * 18f, 0.1f) - 0.5f;
                float sine = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.15f;
                float env = Mathf.Exp(-t * 8f);
                data[i] = (noise * 0.3f + sine) * env;
            }
            clip.SetData(data, 0);
            return clip;
        }

        private int NextSeed(int level, int previous)
        {
            unchecked
            {
                int baseSeed = previous != 0 ? previous : 12345;
                int mix = baseSeed * 1103515245 + 12345 + level * 97;
                return Mathf.Abs(mix == 0 ? level * 7919 : mix);
            }
        }
    }
}
