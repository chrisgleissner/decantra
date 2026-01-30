using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Decantra.Domain.Generation;
using Decantra.Domain.Model;
using Decantra.Domain.Persistence;
using Decantra.Domain.Scoring;
using Decantra.Domain.Solver;
using Decantra.App.Services;
using Decantra.Presentation;
using Decantra.Presentation.View;
using UnityEngine;
using UnityEngine.UI;

namespace Decantra.Presentation.Controller
{
    public sealed class GameController : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private List<BottleView> bottleViews = new List<BottleView>();
        [SerializeField] private HudView hudView;
        [SerializeField] private LevelCompleteBanner levelBanner;
        [SerializeField] private IntroBanner introBanner;
        [SerializeField] private Image backgroundImage;

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
        private int _emptyTransitionScore;
        private int _lastStars;
        private LevelState _nextState;
        private int _nextLevel;
        private int _nextSeed;
        private CancellationTokenSource _precomputeCts;
        private Task<LevelState> _precomputeTask;

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

        private void OnDestroy()
        {
            CancelPrecompute();
        }

        public void LoadLevel(int levelIndex, int seed)
        {
            _currentLevel = Mathf.Max(1, levelIndex);
            _currentSeed = seed > 0 ? seed : NextSeed(_currentLevel, _currentSeed);
            _state = GenerateLevelWithRetry(_currentLevel, _currentSeed);
            _selectedIndex = -1;
            _isCompleting = false;
            _emptyTransitionScore = 0;
            _nextState = null;
            ApplyBackgroundVariation(_currentLevel, _currentSeed);
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
            if (TryApplyMoveAndScore(sourceIndex, targetIndex, out poured))
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
            TryApplyMoveAndScore(sourceIndex, targetIndex, out applied);
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
                int bonus;
                int score = ScoreCalculator.CalculateScore(baseScore, _emptyTransitionScore, _state.MovesUsed, _state.MovesAllowed, _state.OptimalMoves, out bonus);
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

            if (_precomputeTask == null)
            {
                StartPrecomputeNextLevel();
            }

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
                _lastStars = CalculateStars(_state.MovesUsed, _state.OptimalMoves);
                levelBanner.Show(_currentLevel, _lastStars, _sfxEnabled, () => finished = true);
                while (!finished)
                {
                    yield return null;
                }
            }
            else
            {
                yield return new WaitForSeconds(0.5f);
            }

            if (_precomputeTask != null && _precomputeTask.IsCompletedSuccessfully)
            {
                _nextState = _precomputeTask.Result;
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
            CancelPrecompute();
            _nextLevel = _currentLevel + 1;
            _nextSeed = NextSeed(_nextLevel, _currentSeed);

            var tokenSource = new CancellationTokenSource();
            _precomputeCts = tokenSource;
            int level = _nextLevel;
            int seed = _nextSeed;
            _precomputeTask = Task.Run(() => GenerateLevelWithRetryThreadSafe(level, seed, 6, tokenSource.Token), tokenSource.Token);
        }

        private LevelState GenerateLevelWithRetry(int level, int seed, int maxAttempts = 8)
        {
            return GenerateLevelWithRetryInternal(level, seed, maxAttempts, CancellationToken.None, useThreadSafeSeed: false);
        }

        private LevelState GenerateLevelWithRetryThreadSafe(int level, int seed, int maxAttempts, CancellationToken token)
        {
            return GenerateLevelWithRetryInternal(level, seed, maxAttempts, token, useThreadSafeSeed: true);
        }

        private LevelState GenerateLevelWithRetryInternal(int level, int seed, int maxAttempts, CancellationToken token, bool useThreadSafeSeed)
        {
            int attempt = 0;
            int currentSeed = seed;
            int scaledReverse = ComputeReverseMoves(level);
            int scaledPadding = ComputeMovesPadding(level);

            while (attempt < maxAttempts)
            {
                if (token.IsCancellationRequested) return null;
                try
                {
                    return _generator.Generate(currentSeed, level, scaledReverse, scaledPadding);
                }
                catch
                {
                    attempt++;
                    currentSeed = useThreadSafeSeed
                        ? NextSeedThreadSafe(level, currentSeed + 31)
                        : NextSeed(level, currentSeed + 31);
                }
            }

            if (token.IsCancellationRequested) return null;
            int fallbackReverse = useThreadSafeSeed ? System.Math.Max(6, scaledReverse - 6) : Mathf.Max(6, scaledReverse - 6);
            return _generator.Generate(seed, level, fallbackReverse, scaledPadding + 2);
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

        private int CalculateStars(int movesUsed, int optimalMoves)
        {
            if (optimalMoves <= 0) return 1;

            float ratio = movesUsed / (float)optimalMoves;
            if (ratio <= 1.0f) return 5;
            if (ratio <= 1.2f) return 4;
            if (ratio <= 1.4f) return 3;
            if (ratio <= 1.7f) return 2;
            return 1;
        }

        private bool TryApplyMoveAndScore(int sourceIndex, int targetIndex, out int poured)
        {
            poured = 0;
            if (_state == null) return false;
            if (sourceIndex < 0 || sourceIndex >= _state.Bottles.Count) return false;
            if (targetIndex < 0 || targetIndex >= _state.Bottles.Count) return false;

            int sourceCountBefore = _state.Bottles[sourceIndex].Count;
            bool applied = _state.TryApplyMove(sourceIndex, targetIndex, out poured);
            if (!applied) return false;

            if (sourceCountBefore > 0 && _state.Bottles[sourceIndex].IsEmpty)
            {
                _emptyTransitionScore += ScoreCalculator.CalculateEmptyTransitionIncrement(_state.LevelIndex, sourceCountBefore);
            }

            return true;
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

        private void ApplyBackgroundVariation(int levelIndex, int seed)
        {
            if (backgroundImage == null) return;

            float h, s, v;
            Color.RGBToHSV(backgroundImage.color, out h, out s, out v);

            int mix = Mathf.Abs(seed + levelIndex * 9973);
            float t = (mix % 1000) / 1000f;
            float hueOffset = Mathf.Lerp(-0.08f, 0.08f, t);
            float sat = Mathf.Clamp01(s + 0.12f * (0.5f - t));
            float val = Mathf.Clamp01(v + -0.04f * (t - 0.5f));

            Color tint = Color.HSVToRGB(Mathf.Repeat(h + hueOffset, 1f), sat, val);
            tint.a = backgroundImage.color.a;
            backgroundImage.color = tint;
        }

        private static int NextSeedThreadSafe(int level, int previous)
        {
            unchecked
            {
                int baseSeed = previous != 0 ? previous : 12345;
                int mix = baseSeed * 1103515245 + 12345 + level * 97;
                return System.Math.Abs(mix == 0 ? level * 7919 : mix);
            }
        }

        private void CancelPrecompute()
        {
            if (_precomputeCts != null)
            {
                _precomputeCts.Cancel();
                _precomputeCts.Dispose();
                _precomputeCts = null;
            }
            _precomputeTask = null;
            _nextState = null;
        }
    }
}
