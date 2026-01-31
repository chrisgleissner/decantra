using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Decantra.Domain.Generation;
using Decantra.Domain.Model;
using Decantra.Domain.Persistence;
using Decantra.Domain.Rules;
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
        [SerializeField] private OutOfMovesBanner outOfMovesBanner;
        [SerializeField] private RestartGameDialog restartDialog;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image backgroundDetail;
        [SerializeField] private Image backgroundFlow;
        [SerializeField] private Image backgroundShapes;
        [SerializeField] private Image backgroundVignette;

        private LevelState _state;
        private LevelState _initialState;
        private bool _inputLocked;
        private int _selectedIndex = -1;
        private bool _isCompleting;
        private bool _isFailing;
        private bool _isResetting;
        private int _levelSessionId;
        private int _currentLevel = 1;
        private int _currentSeed;
        private int _lastStars;
        private PerformanceGrade _lastGrade;
        private LevelState _nextState;
        private int _nextLevel;
        private int _nextSeed;
        private CancellationTokenSource _precomputeCts;
        private Task<LevelState> _precomputeTask;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
        private string _debugLogPath;
#endif

        private BfsSolver _solver;
        private LevelGenerator _generator;
        private ProgressStore _progressStore;
        private ProgressData _progress;
        private ScoreSession _scoreSession;
        private int _completionStreak;
        private bool _usedUndo;
        private bool _usedHints;
        private SettingsStore _settingsStore;
        private bool _sfxEnabled = true;
        private AudioSource _audioSource;
        private AudioClip _pourClip;

        private const float TransitionTimeoutSeconds = 2.5f;
        private const float BannerTimeoutSeconds = 5.5f;

        private struct BackgroundPalette
        {
            public float Hue;
            public float Saturation;
            public float Value;
            public float DetailSaturation;
            public float DetailValue;
            public float FlowSaturation;
            public float FlowValue;
            public float FlowAlpha;
            public float ShapeSaturation;
            public float ShapeValue;
            public float ShapeAlpha;
            public float VignetteAlpha;
        }

        private struct BackgroundThemeStyle
        {
            public float HueShift;
            public float HueRange;
            public float SaturationBoost;
            public float ValueBoost;
            public float DetailAlphaScale;
            public float FlowAlphaScale;
            public float ShapeAlphaScale;
            public float VignetteAlphaScale;
        }

        private static readonly BackgroundPalette[] BackgroundPalettes =
        {
            new BackgroundPalette { Hue = 0.56f, Saturation = 0.28f, Value = 0.55f, DetailSaturation = 0.2f, DetailValue = 0.6f, FlowSaturation = 0.22f, FlowValue = 0.66f, FlowAlpha = 0.1f, ShapeSaturation = 0.18f, ShapeValue = 0.62f, ShapeAlpha = 0.08f, VignetteAlpha = 0.22f },
            new BackgroundPalette { Hue = 0.33f, Saturation = 0.26f, Value = 0.52f, DetailSaturation = 0.18f, DetailValue = 0.58f, FlowSaturation = 0.2f, FlowValue = 0.64f, FlowAlpha = 0.11f, ShapeSaturation = 0.16f, ShapeValue = 0.6f, ShapeAlpha = 0.09f, VignetteAlpha = 0.24f },
            new BackgroundPalette { Hue = 0.08f, Saturation = 0.24f, Value = 0.54f, DetailSaturation = 0.16f, DetailValue = 0.6f, FlowSaturation = 0.18f, FlowValue = 0.66f, FlowAlpha = 0.09f, ShapeSaturation = 0.14f, ShapeValue = 0.62f, ShapeAlpha = 0.08f, VignetteAlpha = 0.2f },
            new BackgroundPalette { Hue = 0.72f, Saturation = 0.22f, Value = 0.56f, DetailSaturation = 0.16f, DetailValue = 0.62f, FlowSaturation = 0.18f, FlowValue = 0.68f, FlowAlpha = 0.1f, ShapeSaturation = 0.14f, ShapeValue = 0.64f, ShapeAlpha = 0.09f, VignetteAlpha = 0.23f },
            new BackgroundPalette { Hue = 0.46f, Saturation = 0.26f, Value = 0.5f, DetailSaturation = 0.18f, DetailValue = 0.56f, FlowSaturation = 0.2f, FlowValue = 0.62f, FlowAlpha = 0.11f, ShapeSaturation = 0.16f, ShapeValue = 0.58f, ShapeAlpha = 0.1f, VignetteAlpha = 0.25f },
            new BackgroundPalette { Hue = 0.62f, Saturation = 0.2f, Value = 0.58f, DetailSaturation = 0.15f, DetailValue = 0.64f, FlowSaturation = 0.18f, FlowValue = 0.7f, FlowAlpha = 0.1f, ShapeSaturation = 0.14f, ShapeValue = 0.66f, ShapeAlpha = 0.08f, VignetteAlpha = 0.21f }
        };

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

            _scoreSession = new ScoreSession(_progress?.CurrentScore ?? 0);
            _scoreSession.BeginAttempt(_progress?.CurrentScore ?? 0);

            _currentLevel = ProgressionResumePolicy.ResolveResumeLevel(_progress);
            _currentSeed = _progress.CurrentSeed > 0 ? _progress.CurrentSeed : NextSeed(_currentLevel, 0);
            PersistCurrentProgress(_currentLevel, _currentSeed);
            StartCoroutine(BeginSession());
        }

        private void OnDestroy()
        {
            CancelPrecompute();
        }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        private void Update()
        {
            if (_state == null) return;
            if (Input.GetKeyDown(KeyCode.Menu))
            {
                int nextLevel = Mathf.Max(1, _currentLevel + 1);
                int nextSeed = NextSeed(nextLevel, _currentSeed);
                Debug.Log($"Decantra DevAdvance level={nextLevel} seed={nextSeed}");
                AppendDebugLog($"DevAdvance level={nextLevel} seed={nextSeed}");
                LoadLevel(nextLevel, nextSeed);
            }
        }
#endif

        public void LoadLevel(int levelIndex, int seed)
        {
            _levelSessionId++;
            _currentLevel = Mathf.Max(1, levelIndex);
            _currentSeed = seed > 0 ? seed : NextSeed(_currentLevel, _currentSeed);
            _state = EnsureBackground(GenerateLevelWithRetry(_currentLevel, _currentSeed), _currentLevel, _currentSeed);
            CaptureInitialState(_state);
            _selectedIndex = -1;
            _isCompleting = false;
            _isFailing = false;
            _usedUndo = false;
            _usedHints = false;
            int attemptTotal = _progress?.CurrentScore ?? _scoreSession?.TotalScore ?? 0;
            _scoreSession?.BeginAttempt(attemptTotal);
            _nextState = null;
            ApplyBackgroundVariation(_currentLevel, _currentSeed, _state?.BackgroundPaletteIndex ?? -1);
            StartPrecomputeNextLevel();
            PersistCurrentProgress(_currentLevel, _currentSeed);
            Render();
            _inputLocked = false;
        }

        public void ResetCurrentLevel()
        {
            if (_isResetting || _state == null) return;
            if (_inputLocked) return;
            if (_isCompleting) return;

            _isResetting = true;
            _inputLocked = true;

            _scoreSession?.ResetAttempt();
            _usedUndo = false;
            _usedHints = false;
            _selectedIndex = -1;
            _isFailing = false;
            _isCompleting = false;

            RestartCurrentLevel();
            _inputLocked = false;
            _isResetting = false;
        }

        private void RestartCurrentLevel(LevelState restartState = null)
        {
            var stateToUse = restartState;
            if (stateToUse == null && _initialState != null)
            {
                stateToUse = new LevelState(_initialState.Bottles, 0, _initialState.MovesAllowed, _initialState.OptimalMoves, _initialState.LevelIndex, _initialState.Seed, _initialState.ScrambleMoves, _initialState.BackgroundPaletteIndex);
            }

            if (stateToUse == null && _state != null)
            {
                stateToUse = new LevelState(_state.Bottles, 0, _state.MovesAllowed, _state.OptimalMoves, _state.LevelIndex, _state.Seed, _state.ScrambleMoves, _state.BackgroundPaletteIndex);
            }

            if (stateToUse == null)
            {
                LoadLevel(_currentLevel, _currentSeed);
                return;
            }

            ApplyLoadedState(stateToUse, _currentLevel, stateToUse.Seed);
        }

        private void CaptureInitialState(LevelState state)
        {
            if (state == null) return;
            _initialState = new LevelState(state.Bottles, 0, state.MovesAllowed, state.OptimalMoves, state.LevelIndex, state.Seed, state.ScrambleMoves, state.BackgroundPaletteIndex);
        }

        public void OnBottleTapped(int index)
        {
            if (_inputLocked || _state == null) return;
            if (index < 0 || index >= _state.Bottles.Count) return;

            if (_selectedIndex < 0 && !InteractionRules.CanUseAsSource(_state.Bottles[index]))
            {
                return;
            }

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
            return MoveRules.GetPourAmount(_state, sourceIndex, targetIndex);
        }

        public bool CanDragBottle(int index)
        {
            if (_inputLocked || _state == null) return false;
            if (index < 0 || index >= _state.Bottles.Count) return false;
            return InteractionRules.CanDrag(_state.Bottles[index]);
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

#if DEVELOPMENT_BUILD || UNITY_EDITOR
            Debug.Log($"Decantra PourStart level={_state.LevelIndex} source={sourceIndex} target={targetIndex} poured={poured} color={(color.HasValue ? color.Value.ToString() : "none")}");
#endif

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
            _inputLocked = _state != null && (_state.IsWin() || _state.IsFail());
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
            _inputLocked = _state != null && (_state.IsWin() || _state.IsFail());
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
                for (int i = 0; i < bottleViews.Count; i++)
                {
                    var view = bottleViews[i];
                    if (view == null) continue;
                    bool active = i < _state.Bottles.Count;
                    if (view.gameObject.activeSelf != active)
                    {
                        view.gameObject.SetActive(active);
                    }
                    if (active)
                    {
                        view.Render(_state.Bottles[i]);
                    }
                }
            }

            if (hudView != null)
            {
                int total = _scoreSession?.TotalScore ?? 0;
                int provisional = _scoreSession?.ProvisionalScore ?? 0;
                int displayScore = total + provisional;
                int highScore = _progress?.HighScore ?? total;
                int maxLevel = _progress?.HighestUnlockedLevel ?? _currentLevel;
                hudView.Render(_state.LevelIndex, _state.MovesUsed, _state.MovesAllowed, _state.OptimalMoves, displayScore, highScore, maxLevel);
            }

            if (_state.IsWin() && !_isCompleting)
            {
                StartCoroutine(HandleLevelComplete());
            }

            if (_state.IsFail() && !_isFailing && !_isCompleting)
            {
                StartCoroutine(HandleOutOfMoves());
            }
        }

        private IEnumerator HandleLevelComplete()
        {
            int sessionId = _levelSessionId;
            _isCompleting = true;
            _inputLocked = true;

            if (_precomputeTask == null)
            {
                StartPrecomputeNextLevel();
            }

            int nextLevel = _currentLevel + 1;
            float efficiency = ScoreCalculator.CalculateEfficiency(_state.OptimalMoves, _state.MovesUsed);
            _lastGrade = ScoreCalculator.CalculateGrade(_state.OptimalMoves, _state.MovesUsed);
            _lastStars = CalculateStars(_lastGrade);

            _scoreSession?.UpdateProvisional(_state.LevelIndex, _state.OptimalMoves, _state.MovesUsed, _usedUndo, _usedHints, _completionStreak);
            _scoreSession?.CommitLevel();
            _completionStreak++;

            bool finished = false;
            if (_progress != null)
            {
                _progress.HighestUnlockedLevel = Mathf.Max(_progress.HighestUnlockedLevel, nextLevel);
                _progress.CurrentLevel = nextLevel;
                _progress.CurrentSeed = NextSeed(nextLevel, _currentSeed);
                _progress.CurrentScore = _scoreSession?.TotalScore ?? _progress.CurrentScore;
                if (_progress.CurrentScore > _progress.HighScore)
                {
                    _progress.HighScore = _progress.CurrentScore;
                }

                PerformanceTracker.UpdateBest(_progress, new Decantra.Domain.Persistence.LevelPerformanceRecord
                {
                    LevelIndex = _state.LevelIndex,
                    BestMoves = _state.MovesUsed,
                    BestEfficiency = efficiency,
                    BestGrade = _lastGrade
                });

                _progressStore.Save(_progress);
            }
            if (levelBanner != null)
            {
                levelBanner.Show(_currentLevel, _lastStars, _lastGrade, _sfxEnabled, () => finished = true);
                float bannerWait = 0f;
                while (!finished && bannerWait < BannerTimeoutSeconds)
                {
                    bannerWait += Time.unscaledDeltaTime;
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

            int targetSeed = _progress?.CurrentSeed ?? NextSeed(nextLevel, _currentSeed);
            if (sessionId != _levelSessionId) yield break;
            yield return TransitionToLevel(nextLevel, targetSeed);
        }

        private IEnumerator HandleOutOfMoves()
        {
            int sessionId = _levelSessionId;
            _isFailing = true;
            _inputLocked = true;
            _completionStreak = 0;
            var restartSnapshot = CreateRestartSnapshot();

            bool finished = false;
            if (outOfMovesBanner != null)
            {
                outOfMovesBanner.Show("Out of moves. Try again.", _sfxEnabled, () => finished = true);
                if (Application.isBatchMode)
                {
                    yield return new WaitForSeconds(0.6f);
                }
                else
                {
                    float wait = 0f;
                    while (!finished && wait < BannerTimeoutSeconds)
                    {
                        wait += Time.unscaledDeltaTime;
                        yield return null;
                    }
                }
            }
            else
            {
                yield return new WaitForSeconds(0.5f);
            }

            if (sessionId != _levelSessionId) yield break;
            _scoreSession?.FailLevel();
            RestartCurrentLevel(restartSnapshot);
            _inputLocked = false;
            _isFailing = false;
        }

        private LevelState CreateRestartSnapshot()
        {
            if (_initialState != null)
            {
                return new LevelState(_initialState.Bottles, 0, _initialState.MovesAllowed, _initialState.OptimalMoves, _initialState.LevelIndex, _initialState.Seed, _initialState.ScrambleMoves, _initialState.BackgroundPaletteIndex);
            }

            if (_state != null)
            {
                return new LevelState(_state.Bottles, 0, _state.MovesAllowed, _state.OptimalMoves, _state.LevelIndex, _state.Seed, _state.ScrambleMoves, _state.BackgroundPaletteIndex);
            }

            return null;
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
            var profile = LevelDifficultyEngine.GetProfile(level);

            while (attempt < maxAttempts)
            {
                if (token.IsCancellationRequested) return null;
                try
                {
                    return _generator.Generate(currentSeed, profile);
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
            int fallbackReverse = useThreadSafeSeed ? System.Math.Max(6, profile.ReverseMoves - 6) : Mathf.Max(6, profile.ReverseMoves - 6);
            int fallbackAttempts = 0;
            int fallbackSeed = seed;
            while (fallbackAttempts < 3)
            {
                if (token.IsCancellationRequested) return null;
                try
                {
                    var fallbackProfile = LevelDifficultyEngine.GetProfile(level);
                    var adjustedProfile = new DifficultyProfile(level,
                        fallbackProfile.Band,
                        fallbackProfile.BottleCount,
                        fallbackProfile.ColorCount,
                        fallbackProfile.EmptyBottleCount,
                        fallbackReverse,
                        fallbackProfile.ThemeId,
                        fallbackProfile.DifficultyRating);
                    return _generator.Generate(fallbackSeed, adjustedProfile);
                }
                catch
                {
                    fallbackAttempts++;
                    fallbackSeed = useThreadSafeSeed
                        ? NextSeedThreadSafe(level, fallbackSeed + 31)
                        : NextSeed(level, fallbackSeed + 31);
                    fallbackReverse = useThreadSafeSeed
                        ? System.Math.Max(4, fallbackReverse - 1)
                        : Mathf.Max(4, fallbackReverse - 1);
                }
            }
            return null;
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
            if (_state == null)
            {
                LoadLevel(_currentLevel, _currentSeed);
            }
            _inputLocked = false;
        }

        private void PersistCurrentProgress(int levelIndex, int seed)
        {
            if (_progress == null) return;
            _progress.CurrentLevel = Mathf.Max(1, levelIndex);
            _progress.CurrentSeed = seed > 0 ? seed : _progress.CurrentSeed;
            _progressStore.Save(_progress);
        }

        private int CalculateStars(PerformanceGrade grade)
        {
            switch (grade)
            {
                case PerformanceGrade.S:
                    return 5;
                case PerformanceGrade.A:
                    return 4;
                case PerformanceGrade.B:
                    return 3;
                case PerformanceGrade.C:
                    return 2;
                default:
                    return 1;
            }
        }

        private bool TryApplyMoveAndScore(int sourceIndex, int targetIndex, out int poured)
        {
            poured = 0;
            if (_state == null) return false;
            if (sourceIndex < 0 || sourceIndex >= _state.Bottles.Count) return false;
            if (targetIndex < 0 || targetIndex >= _state.Bottles.Count) return false;
            bool applied = _state.TryApplyMove(sourceIndex, targetIndex, out poured);
            if (!applied) return false;

            _scoreSession?.UpdateProvisional(_state.LevelIndex, _state.OptimalMoves, _state.MovesUsed, _usedUndo, _usedHints, _completionStreak);

#if DEVELOPMENT_BUILD || UNITY_EDITOR
            Debug.Log($"Decantra ScoreUpdate level={_state.LevelIndex} movesUsed={_state.MovesUsed} optimal={_state.OptimalMoves} provisional={_scoreSession?.ProvisionalScore ?? 0}");
            AppendDebugLog($"ScoreUpdate level={_state.LevelIndex} movesUsed={_state.MovesUsed} optimal={_state.OptimalMoves} provisional={_scoreSession?.ProvisionalScore ?? 0}");
#endif

            return true;
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

        private void SetupAudio()
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.loop = false;
            _pourClip = CreatePourClip();
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

        private IEnumerator TransitionToLevel(int nextLevel, int seed)
        {
#if UNITY_EDITOR
            Debug.Log($"TransitionToLevel start level={nextLevel} seed={seed} precomputeReady={_precomputeTask != null && _precomputeTask.IsCompletedSuccessfully}");
#endif
            ApplyBackgroundVariation(nextLevel, seed, ResolveBackgroundPaletteIndex(nextLevel, seed));

            if (_precomputeTask != null && _precomputeTask.IsCompletedSuccessfully)
            {
                _nextState = _precomputeTask.Result;
            }

            if (_nextState != null && _nextLevel == nextLevel)
            {
                ApplyLoadedState(_nextState, _nextLevel, _nextSeed);
                _inputLocked = false;
                yield break;
            }

            CancelPrecompute();

            LevelState loaded = null;
            int attempt = 0;
            int currentSeed = seed;
            while (loaded == null && attempt < 2)
            {
                int attemptSeed = currentSeed;
                using (var tokenSource = new CancellationTokenSource())
                {
                    var task = Task.Run(() => GenerateLevelWithRetryThreadSafe(nextLevel, attemptSeed, 8, tokenSource.Token), tokenSource.Token);
                    float elapsed = 0f;
                    while (!task.IsCompleted && elapsed < TransitionTimeoutSeconds)
                    {
                        elapsed += Time.unscaledDeltaTime;
                        yield return null;
                    }

                    if (!task.IsCompleted)
                    {
#if UNITY_EDITOR
                        Debug.LogWarning($"TransitionToLevel timeout level={nextLevel} seed={attemptSeed}");
#endif
                        tokenSource.Cancel();
                        attempt++;
                        currentSeed = NextSeed(nextLevel, currentSeed + 97);
                        continue;
                    }

                    if (task.IsCompletedSuccessfully && task.Result != null)
                    {
                        loaded = task.Result;
                    }
                    else
                    {
                        attempt++;
                        currentSeed = NextSeed(nextLevel, currentSeed + 97);
                    }
                }
            }

            if (loaded == null)
            {
                using (var tokenSource = new CancellationTokenSource())
                {
                    int fallbackSeed = currentSeed;
                    var task = Task.Run(() => GenerateLevelWithRetryThreadSafe(nextLevel, fallbackSeed, 4, tokenSource.Token), tokenSource.Token);
                    float elapsed = 0f;
                    while (!task.IsCompleted && elapsed < TransitionTimeoutSeconds)
                    {
                        elapsed += Time.unscaledDeltaTime;
                        yield return null;
                    }

                    if (task.IsCompletedSuccessfully && task.Result != null)
                    {
                        loaded = task.Result;
                    }
                    else
                    {
#if UNITY_EDITOR
                        Debug.LogWarning($"TransitionToLevel fallback timeout level={nextLevel} seed={fallbackSeed}");
#endif
                        tokenSource.Cancel();
                    }
                }
            }

            if (loaded == null)
            {
                loaded = GenerateLevelWithRetry(nextLevel, currentSeed, 2);
            }

            if (loaded == null)
            {
                int emergencyAttempts = 0;
                int emergencySeed = NextSeed(nextLevel, currentSeed + 97);
                while (loaded == null && emergencyAttempts < 2)
                {
                    loaded = GenerateLevelWithRetry(nextLevel, emergencySeed, 2);
                    emergencyAttempts++;
                    emergencySeed = NextSeed(nextLevel, emergencySeed + 97);
                }
            }

            if (loaded == null)
            {
#if UNITY_EDITOR
                Debug.LogWarning($"TransitionToLevel failed to generate level={nextLevel}");
#endif
                _inputLocked = false;
                yield break;
            }

            ApplyLoadedState(loaded, nextLevel, currentSeed);
            _inputLocked = false;
        }

        public void RequestRestartGame()
        {
            if (_inputLocked) return;
            if (restartDialog == null)
            {
                RestartGameConfirmed();
                return;
            }

            _inputLocked = true;
            restartDialog.Show(RestartGameConfirmed, () => { _inputLocked = false; });
        }

        private void RestartGameConfirmed()
        {
            CancelPrecompute();
            _completionStreak = 0;
            _usedUndo = false;
            _usedHints = false;
            _selectedIndex = -1;
            _isCompleting = false;
            _isFailing = false;

            _progress = new ProgressData
            {
                HighestUnlockedLevel = 1,
                CurrentLevel = 1,
                CurrentSeed = 0,
                CurrentScore = 0,
                HighScore = 0,
                BestPerformances = new List<LevelPerformanceRecord>()
            };

            _progressStore?.Save(_progress);

            _scoreSession = new ScoreSession(0);
            _scoreSession.BeginAttempt(0);

            int restartSeed = NextSeed(1, 0);
            LoadLevel(1, restartSeed);
        }
        private void ApplyLoadedState(LevelState state, int levelIndex, int seed)
        {
            _levelSessionId++;
            _currentLevel = Mathf.Max(1, levelIndex);
            _currentSeed = seed > 0 ? seed : NextSeed(_currentLevel, _currentSeed);
            _state = EnsureBackground(state, _currentLevel, _currentSeed);
            CaptureInitialState(_state);
            _selectedIndex = -1;
            _isCompleting = false;
            _isFailing = false;
            _usedUndo = false;
            _usedHints = false;
            int attemptTotal = _progress?.CurrentScore ?? _scoreSession?.TotalScore ?? 0;
            _scoreSession?.BeginAttempt(attemptTotal);
            _nextState = null;
            ApplyBackgroundVariation(_currentLevel, _currentSeed, _state?.BackgroundPaletteIndex ?? -1);
            StartPrecomputeNextLevel();
            PersistCurrentProgress(_currentLevel, _currentSeed);
            Render();

#if DEVELOPMENT_BUILD || UNITY_EDITOR
            Debug.Log($"Decantra LevelLoaded level={_currentLevel} seed={_currentSeed}");
#endif
        }

        private void ApplyBackgroundVariation(int levelIndex, int seed, int backgroundPaletteIndex)
        {
            if (backgroundImage == null) return;

            var profile = LevelDifficultyEngine.GetProfile(levelIndex);
            var themeStyle = GetThemeStyle(profile.ThemeId);

            int paletteIndex = backgroundPaletteIndex >= 0
                ? backgroundPaletteIndex
                : BackgroundRules.ComputePaletteIndex(levelIndex, seed, profile.ThemeId, BackgroundPalettes.Length);
            var palette = BackgroundPalettes[paletteIndex];

            float jitter = Hash01(seed, levelIndex);
            float jitter2 = Hash01(levelIndex * 31, seed ^ 0x4f1d);
            float hueOffset = Mathf.Lerp(-themeStyle.HueRange, themeStyle.HueRange, jitter);
            float hue = Mathf.Repeat(palette.Hue + hueOffset + themeStyle.HueShift, 1f);
            float sat = Mathf.Clamp01(palette.Saturation + themeStyle.SaturationBoost + Mathf.Lerp(-0.04f, 0.04f, jitter2));
            float val = Mathf.Clamp01(palette.Value + themeStyle.ValueBoost + Mathf.Lerp(-0.05f, 0.05f, 1f - jitter));

            Color baseTint = Color.HSVToRGB(hue, sat, val);
            baseTint.a = 1f;
            backgroundImage.color = baseTint;

            if (backgroundDetail != null)
            {
                Color detailTint = Color.HSVToRGB(Mathf.Repeat(hue + 0.02f, 1f), palette.DetailSaturation, Mathf.Clamp01(palette.DetailValue + 0.03f * (jitter - 0.5f)));
                detailTint.a = Mathf.Lerp(0.12f, 0.22f, jitter2) * themeStyle.DetailAlphaScale;
                backgroundDetail.color = detailTint;
            }

            if (backgroundFlow != null)
            {
                Color flowTint = Color.HSVToRGB(Mathf.Repeat(hue + 0.08f, 1f), palette.FlowSaturation, palette.FlowValue);
                flowTint.a = Mathf.Lerp(palette.FlowAlpha * 0.8f, palette.FlowAlpha * 1.25f, jitter) * themeStyle.FlowAlphaScale;
                backgroundFlow.color = flowTint;
                backgroundFlow.rectTransform.localEulerAngles = new Vector3(0f, 0f, Mathf.Lerp(-6f, 6f, jitter2));
            }

            if (backgroundShapes != null)
            {
                Color shapeTint = Color.HSVToRGB(Mathf.Repeat(hue - 0.04f, 1f), palette.ShapeSaturation, palette.ShapeValue);
                shapeTint.a = Mathf.Lerp(palette.ShapeAlpha * 0.7f, palette.ShapeAlpha * 1.2f, jitter2) * themeStyle.ShapeAlphaScale;
                backgroundShapes.color = shapeTint;
                backgroundShapes.rectTransform.localEulerAngles = new Vector3(0f, 0f, Mathf.Lerp(4f, -4f, jitter));
            }

            if (backgroundVignette != null)
            {
                var vignetteColor = backgroundVignette.color;
                vignetteColor.a = palette.VignetteAlpha * themeStyle.VignetteAlphaScale;
                backgroundVignette.color = vignetteColor;
            }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
            Debug.Log($"Decantra Background level={levelIndex} seed={seed} palette={paletteIndex} theme={profile.ThemeId} base={backgroundImage.color}");
            AppendDebugLog($"Background level={levelIndex} seed={seed} palette={paletteIndex} theme={profile.ThemeId} base={backgroundImage.color}");
#endif
        }

        private int ResolveBackgroundPaletteIndex(int levelIndex, int seed)
        {
            var profile = LevelDifficultyEngine.GetProfile(levelIndex);
            return BackgroundRules.ComputePaletteIndex(levelIndex, seed, profile.ThemeId, BackgroundPalettes.Length);
        }

        private LevelState EnsureBackground(LevelState state, int levelIndex, int seed)
        {
            if (state == null) return null;
            if (state.BackgroundPaletteIndex >= 0) return state;
            int paletteIndex = ResolveBackgroundPaletteIndex(levelIndex, seed);
            return new LevelState(state.Bottles, state.MovesUsed, state.MovesAllowed, state.OptimalMoves, state.LevelIndex, state.Seed, state.ScrambleMoves, paletteIndex);
        }
        private static BackgroundThemeStyle GetThemeStyle(BackgroundThemeId themeId)
        {
            switch (themeId)
            {
                case BackgroundThemeId.SoftGradient:
                    return new BackgroundThemeStyle { HueShift = 0.0f, HueRange = 0.04f, SaturationBoost = -0.06f, ValueBoost = 0.02f, DetailAlphaScale = 0.9f, FlowAlphaScale = 0.85f, ShapeAlphaScale = 0.85f, VignetteAlphaScale = 1.0f };
                case BackgroundThemeId.PastelRainbow:
                    return new BackgroundThemeStyle { HueShift = 0.02f, HueRange = 0.09f, SaturationBoost = -0.02f, ValueBoost = 0.04f, DetailAlphaScale = 1.05f, FlowAlphaScale = 1.0f, ShapeAlphaScale = 0.95f, VignetteAlphaScale = 0.95f };
                case BackgroundThemeId.Balloons:
                    return new BackgroundThemeStyle { HueShift = 0.03f, HueRange = 0.07f, SaturationBoost = 0.02f, ValueBoost = 0.03f, DetailAlphaScale = 1.15f, FlowAlphaScale = 1.1f, ShapeAlphaScale = 1.1f, VignetteAlphaScale = 0.95f };
                case BackgroundThemeId.LightCarnival:
                    return new BackgroundThemeStyle { HueShift = 0.01f, HueRange = 0.06f, SaturationBoost = 0.01f, ValueBoost = 0.02f, DetailAlphaScale = 1.05f, FlowAlphaScale = 1.0f, ShapeAlphaScale = 1.0f, VignetteAlphaScale = 0.95f };
                case BackgroundThemeId.CarnivalPattern:
                    return new BackgroundThemeStyle { HueShift = 0.0f, HueRange = 0.05f, SaturationBoost = 0.03f, ValueBoost = 0.01f, DetailAlphaScale = 1.1f, FlowAlphaScale = 1.05f, ShapeAlphaScale = 1.0f, VignetteAlphaScale = 0.95f };
                case BackgroundThemeId.CarnivalContrast:
                    return new BackgroundThemeStyle { HueShift = 0.04f, HueRange = 0.05f, SaturationBoost = 0.05f, ValueBoost = 0.0f, DetailAlphaScale = 1.15f, FlowAlphaScale = 1.1f, ShapeAlphaScale = 1.05f, VignetteAlphaScale = 0.9f };
                case BackgroundThemeId.RainbowArcs:
                    return new BackgroundThemeStyle { HueShift = 0.05f, HueRange = 0.1f, SaturationBoost = 0.02f, ValueBoost = 0.03f, DetailAlphaScale = 1.1f, FlowAlphaScale = 1.05f, ShapeAlphaScale = 1.05f, VignetteAlphaScale = 0.9f };
                case BackgroundThemeId.PlayfulMotifs:
                    return new BackgroundThemeStyle { HueShift = 0.02f, HueRange = 0.08f, SaturationBoost = 0.04f, ValueBoost = 0.02f, DetailAlphaScale = 1.15f, FlowAlphaScale = 1.1f, ShapeAlphaScale = 1.1f, VignetteAlphaScale = 0.9f };
                case BackgroundThemeId.RefinedCarnival:
                    return new BackgroundThemeStyle { HueShift = 0.0f, HueRange = 0.05f, SaturationBoost = 0.0f, ValueBoost = 0.02f, DetailAlphaScale = 0.95f, FlowAlphaScale = 0.9f, ShapeAlphaScale = 0.85f, VignetteAlphaScale = 1.05f };
                case BackgroundThemeId.RefinedRainbow:
                    return new BackgroundThemeStyle { HueShift = 0.02f, HueRange = 0.07f, SaturationBoost = 0.01f, ValueBoost = 0.03f, DetailAlphaScale = 1.0f, FlowAlphaScale = 0.95f, ShapeAlphaScale = 0.9f, VignetteAlphaScale = 1.0f };
                default:
                    return new BackgroundThemeStyle { HueShift = 0.0f, HueRange = 0.05f, SaturationBoost = 0.0f, ValueBoost = 0.0f, DetailAlphaScale = 1.0f, FlowAlphaScale = 1.0f, ShapeAlphaScale = 1.0f, VignetteAlphaScale = 1.0f };
            }
        }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        private void AppendDebugLog(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            if (string.IsNullOrWhiteSpace(_debugLogPath))
            {
                _debugLogPath = Path.Combine(Application.persistentDataPath, "debug-verification.log");
            }

            try
            {
                File.AppendAllText(_debugLogPath, $"{System.DateTime.UtcNow:O} {message}\n");
            }
            catch
            {
                // Ignore logging failures in debug builds.
            }
        }
#endif

        private static float Hash01(int a, int b)
        {
            unchecked
            {
                int h = a * 73856093 ^ b * 19349663;
                h = (h ^ (h >> 13)) * 1274126177;
                return Mathf.Abs(h % 1000) / 1000f;
            }
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
