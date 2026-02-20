/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Decantra.Domain.Background;
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
        [SerializeField] private ResetLevelDialog resetLevelDialog;
        [SerializeField] private StarTradeInDialog starTradeInDialog;
        [SerializeField] private TutorialManager tutorialManager;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image backgroundDetail;
        [SerializeField] private Image backgroundFlow;
        [SerializeField] private Image backgroundShapes;
        [SerializeField] private Image backgroundVignette;
        [SerializeField] private Image backgroundBubbles;
        [SerializeField] private Image backgroundLargeStructure;
        [SerializeField] private GameObject backgroundStars;
        [SerializeField] private Button levelPanelButton;
        [SerializeField] private GameObject levelJumpOverlay;
        [SerializeField] private InputField levelJumpInput;
        [SerializeField] private Button levelJumpGoButton;
        [SerializeField] private Button levelJumpDismissButton;

        private LevelState _state;
        private LevelState _initialState;
        private bool _inputLocked;
        private int _selectedIndex = -1;
        private bool _isCompleting;
        private bool _isFailing;
        private bool _isResetting;
        private bool _isAutoSolving;
        private bool _autoSolvePlaybackActive;
        private float _autoSolveMoveDuration = 1f;
        private Coroutine _autoSolveReturnRoutine;
        private RectTransform _autoSolveActiveRect;
        private Vector2 _autoSolveStartAnchoredPosition;
        private Quaternion _autoSolveStartLocalRotation;
        private bool _autoSolveVisualActive;
        private int _nextMoveId = 1;
        private int _levelSessionId;
        private int _currentLevel = 1;
        private int _currentSeed;
        private int _lastStars;
        private PerformanceGrade _lastGrade;
        private LevelState _nextState;
        private int _nextLevel;
        private int _nextSeed;
        private int _currentDifficulty100;
        private int _nextDifficulty100;
        private bool _introDismissed;
        private int _currentBackgroundFamily = -1;
        private Coroutine _backgroundTransition;
        private CancellationTokenSource _precomputeCts;
        private Task<GeneratedLevel> _precomputeTask;
        private Coroutine _webGlPrecomputeRoutine;
        private bool _wasInputLockedBeforeOverlay;
        private bool _wasInputLockedBeforeStarTradeIn;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
        private string _debugLogPath;
        private int _precomputeStartFrame = -1;
        private float _levelLoadRealtime = -1f;
        private float _levelCompleteRealtime = -1f;
#endif

        private BfsSolver _solver;
        private LevelGenerator _generator;
        private ProgressStore _progressStore;
        private ProgressData _progress;
        private ScoreSession _scoreSession;
        private int _completionStreak;
        private bool _usedUndo;
        private bool _usedHints;
        private bool _usedRestart;
        private bool _isCurrentLevelAssisted;
        private int _levelResetCount;
        private SettingsStore _settingsStore;
        private bool _sfxEnabled = true;
        private float _sfxVolume01 = 1f;
        private bool _highContrastEnabled;
        private bool _colorBlindAssistEnabled;
        private StarfieldConfig _starfieldConfig;
        private Material _starfieldMaterial;
        private GameObject _optionsOverlay;
        private GameObject _howToPlayOverlay;
        private GameObject _privacyPolicyOverlay;
        private GameObject _termsOverlay;
        private GameObject _highContrastOverlay;
        private Toggle _accessibleColorsToggle;
        private AudioManager _audioManager;
        private int _lastStageUnlockSfxLevel = int.MinValue;

        private const float TransitionTimeoutSeconds = 2.5f;
        private const float BannerTimeoutSeconds = 5.5f;
        private const float StartupFadeDurationSeconds = 0.55f;
        private const float StartupVisualSettleSeconds = 0.9f;
        private const float AutoSolveMinDragSeconds = 0.35f;
        private const float AutoSolveMaxDragSeconds = 1.0f;
        private const float AutoSolveDragSlowdownMultiplier = 1.5f;
        private const float AutoSolveDragTiltDegrees = 30f;
        private const float AutoSolveTiltStartNormalized = 0.62f;
        private const float AutoSolveReturnMinSeconds = 0.2f;
        private const string QuietAutomationFlag = "decantra_quiet";
        private const string CiProbeFlag = "decantra_ci_probe";
        private static readonly bool EnablePourDiagnostics = false;
        private static readonly bool EnableAutoSolveDiagnostics = false;
        private static readonly bool EnablePrecomputeDiagnostics = Application.isEditor || HasLaunchFlag(CiProbeFlag);
        private const int PrecomputeStartFrameBudget = 2;
        private const float PrecomputeReadyExpectedSeconds = 2f;

        public readonly struct PourLifecycleEvent
        {
            public PourLifecycleEvent(int moveId, int sourceIndex, int targetIndex, int amount)
            {
                MoveId = moveId;
                SourceIndex = sourceIndex;
                TargetIndex = targetIndex;
                Amount = amount;
            }

            public int MoveId { get; }
            public int SourceIndex { get; }
            public int TargetIndex { get; }
            public int Amount { get; }
        }

        public readonly struct MoveExecutionResult
        {
            public MoveExecutionResult(bool success, int poured, string rejectionReason)
            {
                Success = success;
                Poured = poured;
                RejectionReason = rejectionReason;
            }

            public bool Success { get; }
            public int Poured { get; }
            public string RejectionReason { get; }
        }

        public event Action<PourLifecycleEvent> PourStarted;
        public event Action<PourLifecycleEvent> PourCompleted;
        public event Action<int> AutoSolveTargetActivated;
        public event Action<int> AutoSolveReleaseInvoked;

        private readonly struct GeneratedLevel
        {
            public GeneratedLevel(LevelState state, int difficulty100)
            {
                State = state;
                Difficulty100 = difficulty100;
            }

            public LevelState State { get; }
            public int Difficulty100 { get; }
        }

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
            // VignetteAlpha removed - vignette effect completely disabled
        }

        private struct BackgroundFamilyProfile
        {
            public float Hue;
            public float Saturation;
            public float Value;
            public float DetailAlphaScale;
            public float FlowAlphaScale;
            public float ShapeAlphaScale;
            public float MacroAlphaScale;
            public float BubbleAlphaScale;
            // VignetteAlphaScale removed - vignette effect completely disabled
            public float DetailScale;
            public float FlowScale;
            public float ShapeScale;
            public float MacroScale;
            public float BubbleScale;
            public Color GradientTop;
            public Color GradientBottom;
            public float GradientDirection;
        }

        private readonly struct ZoneLayoutKey
        {
            private readonly int _globalSeed;
            private readonly int _zoneIndex;

            public ZoneLayoutKey(int globalSeed, int zoneIndex)
            {
                _globalSeed = globalSeed;
                _zoneIndex = zoneIndex;
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (_globalSeed * 397) ^ _zoneIndex;
                }
            }

            public override bool Equals(object obj)
            {
                return obj is ZoneLayoutKey other && other._globalSeed == _globalSeed && other._zoneIndex == _zoneIndex;
            }
        }

        private struct ZoneLayout
        {
            public float DetailScale;
            public Vector2 DetailOffset;
            public float FlowScale;
            public Vector2 FlowOffset;
            public float FlowRotation;
            public float ShapeScale;
            public Vector2 ShapeOffset;
            public float ShapeRotation;
            public float MacroScale;
            public Vector2 MacroOffset;
            public float MacroRotation;
            public float BubbleScale;
            public Vector2 BubbleOffset;
            public float BubbleRotation;
            public float GradientDirection;
            public float GradientIntensity;
        }

        private static readonly Dictionary<ZoneLayoutKey, ZoneLayout> ZoneLayouts = new Dictionary<ZoneLayoutKey, ZoneLayout>();

        private static bool _introShown;

        private static readonly BackgroundPalette[] BackgroundPalettes =
        {
            // Dark blue palettes for cloud overlays (avoid bottle liquid colors)
            // Palette 0: Deep navy
            new BackgroundPalette { Hue = 0.62f, Saturation = 0.32f, Value = 0.32f, DetailSaturation = 0.28f, DetailValue = 0.38f, FlowSaturation = 0.3f, FlowValue = 0.36f, FlowAlpha = 0.32f, ShapeSaturation = 0.28f, ShapeValue = 0.34f, ShapeAlpha = 0.26f },
            // Palette 1: Midnight blue
            new BackgroundPalette { Hue = 0.58f, Saturation = 0.28f, Value = 0.3f, DetailSaturation = 0.26f, DetailValue = 0.36f, FlowSaturation = 0.28f, FlowValue = 0.35f, FlowAlpha = 0.3f, ShapeSaturation = 0.26f, ShapeValue = 0.33f, ShapeAlpha = 0.25f },
            // Palette 2: Indigo
            new BackgroundPalette { Hue = 0.66f, Saturation = 0.34f, Value = 0.34f, DetailSaturation = 0.3f, DetailValue = 0.4f, FlowSaturation = 0.32f, FlowValue = 0.38f, FlowAlpha = 0.32f, ShapeSaturation = 0.3f, ShapeValue = 0.36f, ShapeAlpha = 0.26f },
            // Palette 3: Deep teal-blue
            new BackgroundPalette { Hue = 0.54f, Saturation = 0.3f, Value = 0.31f, DetailSaturation = 0.26f, DetailValue = 0.37f, FlowSaturation = 0.28f, FlowValue = 0.35f, FlowAlpha = 0.3f, ShapeSaturation = 0.26f, ShapeValue = 0.33f, ShapeAlpha = 0.25f },
            // Palette 4: Slate blue
            new BackgroundPalette { Hue = 0.7f, Saturation = 0.3f, Value = 0.33f, DetailSaturation = 0.28f, DetailValue = 0.39f, FlowSaturation = 0.3f, FlowValue = 0.37f, FlowAlpha = 0.32f, ShapeSaturation = 0.28f, ShapeValue = 0.35f, ShapeAlpha = 0.26f },
            // Palette 5: Nightfall blue
            new BackgroundPalette { Hue = 0.6f, Saturation = 0.26f, Value = 0.29f, DetailSaturation = 0.24f, DetailValue = 0.35f, FlowSaturation = 0.26f, FlowValue = 0.34f, FlowAlpha = 0.3f, ShapeSaturation = 0.24f, ShapeValue = 0.32f, ShapeAlpha = 0.24f }
        };

        private const float BackgroundFamilyTransitionSeconds = 0.75f;

        private readonly struct GradientCacheKey
        {
            private readonly int _familyIndex;
            private readonly int _hash;

            public GradientCacheKey(int familyIndex, Color top, Color bottom)
            {
                _familyIndex = familyIndex;
                _hash = ColorHash(top) ^ (ColorHash(bottom) * 397);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (_familyIndex * 397) ^ _hash;
                }
            }

            public override bool Equals(object obj)
            {
                return obj is GradientCacheKey other && other._familyIndex == _familyIndex && other._hash == _hash;
            }

            private static int ColorHash(Color color)
            {
                int r = Mathf.RoundToInt(color.r * 255f);
                int g = Mathf.RoundToInt(color.g * 255f);
                int b = Mathf.RoundToInt(color.b * 255f);
                int a = Mathf.RoundToInt(color.a * 255f);
                unchecked
                {
                    int hash = r;
                    hash = (hash * 397) ^ g;
                    hash = (hash * 397) ^ b;
                    hash = (hash * 397) ^ a;
                    return hash;
                }
            }
        }

        private static readonly Dictionary<GradientCacheKey, Sprite> BackgroundFamilyGradients = new Dictionary<GradientCacheKey, Sprite>();

        public bool IsInputLocked => _inputLocked;
        public bool IsSfxEnabled => _sfxEnabled;
        public float SfxVolume01 => _sfxVolume01;
        public bool HighContrastEnabled => _highContrastEnabled;
        public bool AccessibleColorsEnabled => _colorBlindAssistEnabled;
        public bool ColorBlindAssistEnabled => _colorBlindAssistEnabled;
        public bool HasActiveLevel => _state != null;

        private void Awake()
        {
            _solver = new BfsSolver();
            _generator = new LevelGenerator(_solver);
            _progressStore = new ProgressStore();
            _settingsStore = new SettingsStore();
        }

        private void Start()
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            CiProbeClearIfEnabled();
            CiProbeAppendIfEnabled("SESSION_STARTED");
#endif

            for (int i = 0; i < bottleViews.Count; i++)
            {
                bottleViews[i].Initialize(i);
            }

            Canvas.ForceUpdateCanvases();
            _progress = _progressStore.Load();
            _sfxEnabled = _settingsStore.LoadSfxEnabled();
            _sfxVolume01 = _settingsStore.LoadSfxVolume01();
            _highContrastEnabled = _settingsStore.LoadHighContrastEnabled();
            _colorBlindAssistEnabled = _settingsStore.LoadAccessibleColorsEnabled();
            _starfieldConfig = _settingsStore.LoadStarfieldConfig();
            ApplyQuietAudioOverridesForAutomation();
            ApplyStarfieldConfig();
            SetupAudio();
            ApplyAccessibilitySettings();

            if (tutorialManager != null)
            {
                tutorialManager.Initialize(this, _settingsStore);
            }

            ResolveOverlayReferencesIfMissing();
            ApplyHowToPlayOverlayTypography();
            HideModal(_optionsOverlay);
            HideModal(_howToPlayOverlay);
            HideModal(_privacyPolicyOverlay);
            HideModal(_termsOverlay);

            if (starTradeInDialog != null)
            {
                starTradeInDialog.Initialize();
                starTradeInDialog.Hide();
            }

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
            var generated = GenerateLevelWithRetry(_currentLevel, _currentSeed);
            _currentDifficulty100 = generated.Difficulty100;
            _state = EnsureBackground(generated.State, _currentLevel, _currentSeed);
            CaptureInitialState(_state);
            _selectedIndex = -1;
            _isCompleting = false;
            _isFailing = false;
            _usedUndo = false;
            _usedHints = false;
            _usedRestart = false;
            _isCurrentLevelAssisted = false;
            _levelResetCount = 0;
            _isAutoSolving = false;
            _autoSolvePlaybackActive = false;
            int attemptTotal = _progress?.CurrentScore ?? _scoreSession?.TotalScore ?? 0;
            _scoreSession?.BeginAttempt(attemptTotal);
            _nextState = null;
            ApplyBackgroundVariation(_currentLevel, _currentSeed, _state?.BackgroundPaletteIndex ?? -1);
            StartPrecomputeNextLevel();
            PersistCurrentProgress(_currentLevel, _currentSeed);
            InitializeLevelAudio(_currentLevel, _currentSeed);
            Render();
            _inputLocked = false;
            TrackLevelLoadPrecomputeExpectations(_currentLevel);
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (EnablePrecomputeDiagnostics)
            {
                StartCoroutine(EmitNextLevelFirstFrame(_levelSessionId, _currentLevel));
            }
#endif
        }

        public void ShowLevelJumpOverlay()
        {
            if (levelJumpOverlay == null) return;
            if (levelJumpOverlay.activeSelf) return;

            _wasInputLockedBeforeOverlay = _inputLocked;
            _inputLocked = true;
            levelJumpOverlay.SetActive(true);

            if (levelPanelButton != null)
            {
                levelPanelButton.interactable = false;
            }

            if (levelJumpInput != null)
            {
                levelJumpInput.text = _currentLevel.ToString();
                levelJumpInput.caretPosition = levelJumpInput.text.Length;
                levelJumpInput.ActivateInputField();
            }
        }

        public void HideLevelJumpOverlay()
        {
            if (levelJumpOverlay == null) return;
            levelJumpOverlay.SetActive(false);
            if (levelPanelButton != null)
            {
                levelPanelButton.interactable = true;
            }
            _inputLocked = _wasInputLockedBeforeOverlay;
        }

        public void JumpToLevelFromOverlay()
        {
            if (levelJumpInput == null)
            {
                HideLevelJumpOverlay();
                return;
            }

            if (!int.TryParse(levelJumpInput.text, out int targetLevel))
            {
                return;
            }

            targetLevel = Mathf.Max(1, targetLevel);
            int seed = CalculateSeedForLevel(targetLevel);
            HideLevelJumpOverlay();
            LoadLevel(targetLevel, seed);
        }

        public void ResetCurrentLevel()
        {
            if (_isResetting || _state == null) return;
            if (_inputLocked) return;
            if (_isCompleting) return;

            if (resetLevelDialog != null)
            {
                _inputLocked = true;
                PlayButtonSfx();
                resetLevelDialog.Show(ConfirmResetCurrentLevel, CancelResetCurrentLevel);
                return;
            }

            ConfirmResetCurrentLevel();
        }

        private void ConfirmResetCurrentLevel()
        {
            if (_isResetting || _state == null) return;

            _isResetting = true;
            _inputLocked = true;

            _scoreSession?.ResetAttempt();
            _usedUndo = false;
            _usedHints = false;
            _selectedIndex = -1;
            _isFailing = false;
            _isCompleting = false;
            _levelResetCount++;

            RestartCurrentLevel();
            _usedRestart = true;
            _inputLocked = false;
            _isResetting = false;
            PlayButtonSfx();
        }

        private void CancelResetCurrentLevel()
        {
            _inputLocked = false;
            PlayButtonSfx();
        }

        private void RestartCurrentLevel(LevelState restartState = null)
        {
            int preservedResetCount = _levelResetCount;
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
            _levelResetCount = preservedResetCount;
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
                GetBottleView(index)?.PlayResistanceFeedback();
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

        public void NotifyFirstInteraction()
        {
            if (_introDismissed) return;
            if (introBanner != null && introBanner.IsPlaying)
            {
                return;
            }
            _introDismissed = true;
        }

        public bool TryStartMove(int sourceIndex, int targetIndex, out float duration)
        {
            return TryStartMoveInternal(sourceIndex, targetIndex, out duration, out _);
        }

        private bool TryStartMoveInternal(int sourceIndex, int targetIndex, out float duration, out int moveId)
        {
            duration = 0f;
            moveId = -1;
            if ((_inputLocked && !_isAutoSolving) || _state == null) return false;
            if (sourceIndex == targetIndex) return false;

            int poured = GetPourAmount(sourceIndex, targetIndex);
            if (poured <= 0) return false;

            var targetBottle = _state.Bottles[targetIndex];
            float previousFillRatio = Mathf.Clamp01(targetBottle.Count / (float)targetBottle.Capacity);
            float newFillRatio = Mathf.Clamp01((targetBottle.Count + poured) / (float)targetBottle.Capacity);

            duration = ResolvePourWindowDuration(previousFillRatio, newFillRatio, poured);
            _inputLocked = true;

            var sourceView = GetBottleView(sourceIndex);
            var targetView = GetBottleView(targetIndex);
            var color = _state.Bottles[sourceIndex].TopColor;

            PlayPourSfx(previousFillRatio, newFillRatio);

            if (EnablePourDiagnostics)
            {
                string mode = _autoSolvePlaybackActive ? "auto-solve" : "manual";
                Debug.Log($"Decantra PourWindow mode={mode} start={Time.realtimeSinceStartup:0.000}s end~={Time.realtimeSinceStartup + duration:0.000}s startFill={previousFillRatio:0.###} endFill={newFillRatio:0.###} delta={(newFillRatio - previousFillRatio):0.###} duration={duration:0.###}");
            }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
            Debug.Log($"Decantra PourStart level={_state.LevelIndex} source={sourceIndex} target={targetIndex} poured={poured} color={(color.HasValue ? color.Value.ToString() : "none")}");
#endif

            moveId = _nextMoveId++;
            var startedEvent = new PourLifecycleEvent(moveId, sourceIndex, targetIndex, poured);
            PourStarted?.Invoke(startedEvent);
            EmitAutoSolveDiagnostic($"PourStarted(amount={poured})", $"moveId={moveId} source={sourceIndex} target={targetIndex}");
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            CiProbeAppendIfEnabled($"POUR_STARTED moveId={moveId} source={sourceIndex} target={targetIndex} amount={poured}");
#endif

            StartCoroutine(AnimateMove(moveId, sourceIndex, targetIndex, poured, color, duration, sourceView, targetView));
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

        private IEnumerator AnimateMove(int moveId, int sourceIndex, int targetIndex, int poured, ColorId? color, float duration, BottleView sourceView, BottleView targetView)
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
            if (_state != null && targetIndex >= 0 && targetIndex < _state.Bottles.Count && _state.Bottles[targetIndex].IsSolvedBottle())
            {
                PlayBottleFullSfx();
            }
            Render();
            sourceView?.ClearOutgoing();
            targetView?.ClearIncoming();
            var completedEvent = new PourLifecycleEvent(moveId, sourceIndex, targetIndex, applied);
            PourCompleted?.Invoke(completedEvent);
            EmitAutoSolveDiagnostic("PourCompleted()", $"moveId={moveId} source={sourceIndex} target={targetIndex} amount={applied}");
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (_state != null)
            {
                CiProbeAppendIfEnabled($"POUR_COMPLETED moveId={moveId} source={sourceIndex} target={targetIndex} amount={applied} movesUsed={_state.MovesUsed}");
            }
#endif
            _inputLocked = _state != null && (_state.IsWin() || _state.IsFail());
        }

        public IEnumerator PerformAutoSolveMove(int sourceIndex, int targetIndex, int stepId, Action<MoveExecutionResult> onCompleted = null)
        {
            yield return PerformMove(sourceIndex, targetIndex, stepId, animateDrag: true, onCompleted: onCompleted);
        }

        private IEnumerator PerformMove(int sourceIndex, int targetIndex, int stepId, bool animateDrag, Action<MoveExecutionResult> onCompleted)
        {
            string orchestrationState = "Idle";
            MoveExecutionResult result;

            void TransitionTo(string newState)
            {
                EmitAutoSolveDiagnostic($"StateTransition({orchestrationState},{newState})", $"stepId={stepId}");
                orchestrationState = newState;
            }

            MoveExecutionResult Reject(string reason)
            {
                EmitAutoSolveDiagnostic($"MoveRejected(reason={reason})", $"stepId={stepId} source={sourceIndex} target={targetIndex}");
                return new MoveExecutionResult(success: false, poured: 0, rejectionReason: reason);
            }

            EmitAutoSolveDiagnostic($"AutosolveStepStarted(stepId={stepId})", $"source={sourceIndex} target={targetIndex}");
            if (_state == null)
            {
                result = Reject("state-null");
                onCompleted?.Invoke(result);
                yield break;
            }

            if (sourceIndex < 0 || sourceIndex >= _state.Bottles.Count)
            {
                result = Reject("source-index-out-of-range");
                onCompleted?.Invoke(result);
                yield break;
            }

            if (targetIndex < 0 || targetIndex >= _state.Bottles.Count)
            {
                result = Reject("target-index-out-of-range");
                onCompleted?.Invoke(result);
                yield break;
            }

            int pourAmount = GetPourAmount(sourceIndex, targetIndex);
            if (pourAmount <= 0)
            {
                result = Reject("illegal-move");
                onCompleted?.Invoke(result);
                yield break;
            }

            TransitionTo("SourceSelected");
            EmitAutoSolveDiagnostic($"SourceSelected(bottleId={sourceIndex})", $"stepId={stepId}");
            EmitAutoSolveDiagnostic($"TargetChosen(bottleId={targetIndex})", $"stepId={stepId}");

            var targetView = GetBottleView(targetIndex);
            if (animateDrag)
            {
                TransitionTo("DragAnimating");
                yield return AnimateAutoSolveDrag(sourceIndex, targetIndex);
            }

            TransitionTo("TargetActivation");
            if (targetView != null)
            {
                targetView.SetHighlight(true);
                AutoSolveTargetActivated?.Invoke(targetIndex);
                EmitAutoSolveDiagnostic($"TargetActivated(bottleId={targetIndex})", $"stepId={stepId}");
            }

            yield return null;

            TransitionTo("Release");
            AutoSolveReleaseInvoked?.Invoke(targetIndex);
            EmitAutoSolveDiagnostic("ReleaseInvoked()", $"stepId={stepId}");

            bool started = TryStartMoveInternal(sourceIndex, targetIndex, out float moveDuration, out int moveId);
            if (!started)
            {
                if (_autoSolveVisualActive)
                {
                    StartAutoSolveReturn(AutoSolveReturnMinSeconds);
                }
                if (targetView != null)
                {
                    targetView.SetHighlight(false);
                }

                result = Reject("release-rejected");
                onCompleted?.Invoke(result);
                yield break;
            }

            StartAutoSolveReturn(moveDuration);
            TransitionTo("WaitingForPourCompletion");

            bool pourCompleted = false;
            void HandlePourCompleted(PourLifecycleEvent evt)
            {
                if (evt.MoveId == moveId)
                {
                    pourCompleted = true;
                }
            }

            PourCompleted += HandlePourCompleted;
            while (!pourCompleted)
            {
                if (_state == null)
                {
                    break;
                }

                yield return null;
            }
            PourCompleted -= HandlePourCompleted;

            if (targetView != null)
            {
                targetView.SetHighlight(false);
            }

            TransitionTo("Completed");
            result = new MoveExecutionResult(success: pourCompleted, poured: pourAmount, rejectionReason: pourCompleted ? string.Empty : "pour-not-completed");
            onCompleted?.Invoke(result);
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
                // Compute max capacity for canonical liquid-height mapping
                int maxCap = 1;
                for (int i = 0; i < _state.Bottles.Count; i++)
                {
                    if (_state.Bottles[i].Capacity > maxCap)
                        maxCap = _state.Bottles[i].Capacity;
                }

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
                        view.SetLevelMaxCapacity(maxCap);
                        view.Render(_state.Bottles[i]);
                    }
                }
            }

            if (hudView != null)
            {
                int total = _scoreSession?.TotalScore ?? 0;
                // Requirement #5: Score applies at specific moment in interstitial.
                // We show only TotalScore during gameplay (Provisional hidden).
                int displayScore = total;
                int starBalance = _progress?.StarBalance ?? 0;
                int highScore = _progress?.HighScore ?? total;
                int maxLevel = _progress?.HighestUnlockedLevel ?? _currentLevel;
                bool levelCompleted = IsCurrentLevelCompleted(_state.LevelIndex);
                hudView.Render(_state.LevelIndex, _state.MovesUsed, _state.MovesAllowed, _state.OptimalMoves, displayScore, starBalance, highScore, maxLevel, _currentDifficulty100, levelCompleted);
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
            int baseStars = CalculateStars(_state.OptimalMoves, _state.MovesUsed, _state.MovesAllowed);
            _lastStars = ResolveAwardedStars(baseStars);

            if (_isCurrentLevelAssisted)
            {
                _scoreSession?.ResetAttempt();
            }
            else
            {
                _scoreSession?.UpdateProvisional(_state.OptimalMoves, _state.MovesUsed, _state.MovesAllowed, _currentDifficulty100, IsCleanSolve);
            }

            int baseAwardedScore = _isCurrentLevelAssisted ? 0 : (_scoreSession?.ProvisionalScore ?? 0);
            int awardedScore = ResolveAwardedScore(baseAwardedScore);
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            Debug.Log($"Decantra AwardDegradation level={_state.LevelIndex} resetCount={_levelResetCount} baseStars={baseStars} awardedStars={_lastStars} baseScore={baseAwardedScore} awardedScore={awardedScore}");
#endif
            // CommitLevel delayed to onScoreApply
            _completionStreak++;

            bool finished = false;

            Action onScoreApply = () =>
            {
                if (!_isCurrentLevelAssisted)
                {
                    _scoreSession?.CommitLevel(awardedScore);
                }
                else
                {
                    _scoreSession?.ResetAttempt();
                }

                if (_progress != null)
                {
                    _progress.StarBalance = Math.Max(0, _progress.StarBalance + _lastStars);
                    _progress.HighestUnlockedLevel = Mathf.Max(_progress.HighestUnlockedLevel, nextLevel);
                    _progress.CurrentLevel = nextLevel;
                    _progress.CurrentSeed = NextSeed(nextLevel, _currentSeed);
                    if (_progress.CompletedLevels == null)
                    {
                        _progress.CompletedLevels = new List<int>();
                    }
                    if (!_progress.CompletedLevels.Contains(_currentLevel))
                    {
                        _progress.CompletedLevels.Add(_currentLevel);
                    }
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
                Render();
                if (hudView != null) hudView.AnimateScoreUpdate();
            };

            if (levelBanner != null)
            {
                PlayLevelCompleteSfx();
                levelBanner.Show(_currentLevel, _lastStars, awardedScore, false, onScoreApply, () => finished = true);
                float bannerWait = 0f;
                while (!finished && bannerWait < BannerTimeoutSeconds)
                {
                    bannerWait += Time.unscaledDeltaTime;
                    yield return null;
                }
            }
            else
            {
                onScoreApply();
                yield return new WaitForSeconds(0.5f);
            }

            TryApplyCompletedPrecompute("level-complete");

#if DEVELOPMENT_BUILD || UNITY_EDITOR
            _levelCompleteRealtime = Time.realtimeSinceStartup;
            bool precomputeReadyAtCompletion = _nextState != null || _webGlPrecomputeRoutine != null;
            EmitPrecomputeDiagnostic("LevelComplete",
                $"level={_currentLevel} nextLevel={nextLevel} readyAtCompletion={precomputeReadyAtCompletion}");
            if (!precomputeReadyAtCompletion && _levelLoadRealtime >= 0f && (_levelCompleteRealtime - _levelLoadRealtime) >= PrecomputeReadyExpectedSeconds)
            {
                Debug.LogWarning($"Decantra Precompute assert failed: next level not ready by completion level={_currentLevel} elapsed={(_levelCompleteRealtime - _levelLoadRealtime):0.000}s");
            }
#endif

            int targetSeed = _progress?.CurrentSeed ?? NextSeed(nextLevel, _currentSeed);
            if (sessionId != _levelSessionId) yield break;
            TryPlayStageUnlockedTransitionSfx(nextLevel);
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
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            _precomputeStartFrame = Time.frameCount;
#endif
            EmitPrecomputeDiagnostic("Start", $"level={_currentLevel} nextLevel={level} seed={seed}");

            if (UseWebGlMainThreadPrecompute())
            {
                _webGlPrecomputeRoutine = StartCoroutine(PrecomputeNextLevelOnMainThread(level, seed));
                return;
            }

            _precomputeTask = Task.Run(() => GenerateLevelWithRetryThreadSafe(level, seed, 6, tokenSource.Token), tokenSource.Token);
        }

        private GeneratedLevel GenerateLevelWithRetry(int level, int seed, int maxAttempts = 8)
        {
            var state = GenerateLevelWithRetryInternal(level, seed, maxAttempts, CancellationToken.None, useThreadSafeSeed: false);
            int difficulty = ResolveLastDifficulty100();
            return new GeneratedLevel(state, difficulty);
        }

        private GeneratedLevel GenerateLevelWithRetryThreadSafe(int level, int seed, int maxAttempts, CancellationToken token)
        {
            var state = GenerateLevelWithRetryInternal(level, seed, maxAttempts, token, useThreadSafeSeed: true);
            int difficulty = ResolveLastDifficulty100();
            return new GeneratedLevel(state, difficulty);
        }

        private int ResolveLastDifficulty100()
        {
            int difficulty = _generator?.LastReport?.Difficulty100 ?? 1;
            return Mathf.Clamp(difficulty, 0, 100);
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
                    var generated = _generator.Generate(currentSeed, profile);
                    return EnsureBackground(generated, level, currentSeed);
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
            if (_audioManager != null)
            {
                _audioManager.SetEnabled(enabled);
            }
            PlayButtonSfx();
        }

        public void SetSfxVolume01(float volume01)
        {
            _sfxVolume01 = Mathf.Clamp01(volume01);
            _settingsStore.SaveSfxVolume01(_sfxVolume01);
            if (_audioManager != null)
            {
                _audioManager.SetVolume01(_sfxVolume01);
            }
        }

        public void SetHighContrastEnabled(bool enabled)
        {
            _highContrastEnabled = enabled;
            _settingsStore.SaveHighContrastEnabled(enabled);
            ApplyAccessibilitySettings();
            PlayButtonSfx();
        }

        public void SetColorBlindAssistEnabled(bool enabled)
        {
            _colorBlindAssistEnabled = enabled;
            _settingsStore.SaveAccessibleColorsEnabled(enabled);
            ApplyAccessibilitySettings();
            Render();
            PlayButtonSfx();
        }

        public void SetAccessibleColorsEnabled(bool enabled)
        {
            SetColorBlindAssistEnabled(enabled);
        }

        public void ReplayTutorial()
        {
            HideOptionsOverlay();
            ResolveTutorialManagerIfMissing();
            if (tutorialManager != null)
            {
                tutorialManager.BeginReplay();
            }
            PlayButtonSfx();
        }

        public void SuppressTutorialOverlayForAutomation()
        {
            ResolveTutorialManagerIfMissing();
            if (tutorialManager != null)
            {
                tutorialManager.SuppressForAutomation();
            }

            var tutorialOverlay = GameObject.Find("TutorialOverlay");
            if (tutorialOverlay != null)
            {
                HideModal(tutorialOverlay);
            }
        }

        public void ShowPrivacyPolicyOverlay()
        {
            ResolveOverlayReferencesIfMissing();
            ShowModal(_privacyPolicyOverlay);
            PlayButtonSfx();
        }

        public void HidePrivacyPolicyOverlay()
        {
            ResolveOverlayReferencesIfMissing();
            HideModal(_privacyPolicyOverlay);
            PlayButtonSfx();
        }

        public void ShowTermsOverlay()
        {
            ResolveOverlayReferencesIfMissing();
            ShowModal(_termsOverlay);
            PlayButtonSfx();
        }

        public void HideTermsOverlay()
        {
            ResolveOverlayReferencesIfMissing();
            HideModal(_termsOverlay);
            PlayButtonSfx();
        }

        public StarfieldConfig StarfieldConfiguration => _starfieldConfig;

        public void SetStarfieldConfig(StarfieldConfig config)
        {
            if (config == null) return;
            _starfieldConfig = config;
            _settingsStore.SaveStarfieldConfig(config);
            ApplyStarfieldConfig();
        }

        public void SetStarfieldEnabled(bool enabled)
        {
            SetStarfieldConfig(_starfieldConfig.WithEnabled(enabled));
        }

        public void SetStarfieldDensity(float density)
        {
            SetStarfieldConfig(_starfieldConfig.WithDensity(density));
        }

        public void SetStarfieldSpeed(float speed)
        {
            SetStarfieldConfig(_starfieldConfig.WithSpeed(speed));
        }

        public void SetStarfieldBrightness(float brightness)
        {
            SetStarfieldConfig(_starfieldConfig.WithBrightness(brightness));
        }

        public void ShowOptionsOverlay()
        {
            ResolveOverlayReferencesIfMissing();
            if (_optionsOverlay != null)
            {
                if (_accessibleColorsToggle != null)
                {
                    _accessibleColorsToggle.SetIsOnWithoutNotify(_colorBlindAssistEnabled);
                }

                ShowModal(_optionsOverlay);
            }
            PlayButtonSfx();
        }

        public void HideOptionsOverlay()
        {
            ResolveOverlayReferencesIfMissing();
            HideModal(_optionsOverlay);

            HideHowToPlayOverlay();
            HideModal(_privacyPolicyOverlay);
            HideModal(_termsOverlay);
        }

        public bool IsOptionsOverlayVisible => IsModalVisible(_optionsOverlay);

        public void ShowStarTradeInDialog()
        {
            if (_state == null || starTradeInDialog == null || _isAutoSolving) return;
            if (tutorialManager != null && tutorialManager.IsRunning) return;
            if (starTradeInDialog.IsVisible) return;

            int starBalance = Math.Max(0, _progress?.StarBalance ?? 0);
            int convertCost = StarEconomy.ConvertSinksCost;
            int autoSolveCost = ResolveAutoSolveCost();
            bool hasSink = HasAnySinkBottle();
            bool canConvert = hasSink && starBalance >= convertCost;
            bool canAutoSolve = starBalance >= autoSolveCost;

            _wasInputLockedBeforeStarTradeIn = _inputLocked;
            _inputLocked = true;

            starTradeInDialog.Show(
                starBalance,
                convertCost,
                hasSink,
                canConvert,
                autoSolveCost,
                canAutoSolve,
                ExecuteConvertSinksTradeIn,
                ExecuteAutoSolveTradeIn,
                HideStarTradeInDialog);
            PlayButtonSfx();
        }

        public void HideStarTradeInDialog()
        {
            if (starTradeInDialog != null)
            {
                starTradeInDialog.Hide();
            }
            _inputLocked = _wasInputLockedBeforeStarTradeIn;
            PlayButtonSfx();
        }

        public void ShowHowToPlayOverlay()
        {
            ResolveOverlayReferencesIfMissing();
            ApplyHowToPlayOverlayTypography();
            ShowModal(_howToPlayOverlay);
            PlayButtonSfx();
        }

        public void HideHowToPlayOverlay()
        {
            ResolveOverlayReferencesIfMissing();
            HideModal(_howToPlayOverlay);
            PlayButtonSfx();
        }

        public bool IsHowToPlayOverlayVisible => IsModalVisible(_howToPlayOverlay);

        private void ApplyHowToPlayOverlayTypography()
        {
            if (_howToPlayOverlay == null)
            {
                return;
            }

            var texts = _howToPlayOverlay.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                var text = texts[i];
                if (text == null)
                {
                    continue;
                }

                text.resizeTextForBestFit = false;
                text.horizontalOverflow = HorizontalWrapMode.Wrap;
                text.verticalOverflow = VerticalWrapMode.Overflow;

                if (string.Equals(text.name, "Title", StringComparison.OrdinalIgnoreCase))
                {
                    text.fontSize = Mathf.Max(text.fontSize, ModalDesignTokens.Typography.ModalHeader + 4);
                    continue;
                }

                if (string.Equals(text.name, "Label", StringComparison.OrdinalIgnoreCase)
                    && text.transform.parent != null
                    && string.Equals(text.transform.parent.name, "BackRow", StringComparison.OrdinalIgnoreCase))
                {
                    text.fontSize = Mathf.Max(text.fontSize, ModalDesignTokens.Typography.ButtonText);
                    continue;
                }

                if (text.transform.parent != null
                    && string.Equals(text.transform.parent.name, "Content", StringComparison.OrdinalIgnoreCase))
                {
                    text.fontSize = Mathf.Max(text.fontSize, ModalDesignTokens.Typography.BodyText + 8);
                    text.lineSpacing = Mathf.Max(text.lineSpacing, 1.22f);
                }
            }
        }

        private static void ShowModal(GameObject modalRoot)
        {
            if (modalRoot == null)
            {
                return;
            }

            var baseModal = modalRoot.GetComponent<BaseModal>();
            if (baseModal != null)
            {
                baseModal.Show();
                return;
            }

            modalRoot.SetActive(true);
        }

        private static void HideModal(GameObject modalRoot)
        {
            if (modalRoot == null)
            {
                return;
            }

            var baseModal = modalRoot.GetComponent<BaseModal>();
            if (baseModal != null)
            {
                baseModal.Hide();
                return;
            }

            modalRoot.SetActive(false);
        }

        private static bool IsModalVisible(GameObject modalRoot)
        {
            if (modalRoot == null)
            {
                return false;
            }

            var baseModal = modalRoot.GetComponent<BaseModal>();
            if (baseModal != null)
            {
                return baseModal.IsVisible;
            }

            return modalRoot.activeSelf;
        }

        private static bool ShouldSuppressAutoTutorialForAutomation()
        {
            if (Application.isBatchMode)
            {
                return true;
            }

            if (UnityEngine.Object.FindFirstObjectByType<RuntimeScreenshot>() != null)
            {
                return true;
            }

            return HasLaunchFlag("-runTests")
                || HasLaunchFlag("-testPlatform")
                || HasLaunchFlag("-testResults")
                || HasLaunchFlag("decantra_screenshots")
                || HasLaunchFlag("decantra_screenshots_only")
                || HasLaunchFlag("decantra_motion_capture")
                || HasLaunchFlag("--screenshots")
                || HasLaunchFlag("--screenshots-only")
                || HasLaunchFlag("--motion-capture")
                || HasLaunchFlag(QuietAutomationFlag);
        }

        private void ApplyQuietAudioOverridesForAutomation()
        {
            if (!ShouldForceQuietAudioForAutomation())
            {
                return;
            }

            _sfxEnabled = false;
            _sfxVolume01 = 0f;
            AudioListener.pause = true;
            AudioListener.volume = 0f;
        }

        private static bool ShouldForceQuietAudioForAutomation()
        {
            if (Application.isBatchMode)
            {
                return true;
            }

            if (IsTruthyEnvironmentFlag(Environment.GetEnvironmentVariable("UNITY_DISABLE_AUDIO")))
            {
                return true;
            }

            return HasLaunchFlag("-noaudio")
                || HasLaunchFlag("-disable-audio")
                || HasLaunchFlag("-runTests")
                || HasLaunchFlag("-testPlatform")
                || HasLaunchFlag("-testResults")
                || HasLaunchFlag("decantra_screenshots")
                || HasLaunchFlag("decantra_screenshots_only")
                || HasLaunchFlag("decantra_motion_capture")
                || HasLaunchFlag("--screenshots")
                || HasLaunchFlag("--screenshots-only")
                || HasLaunchFlag("--motion-capture")
                || HasLaunchFlag(QuietAutomationFlag);
        }

        private static bool IsTruthyEnvironmentFlag(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "on", StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasLaunchFlag(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var intent = activity.Call<AndroidJavaObject>("getIntent"))
                {
                    if (intent == null || !intent.Call<bool>("hasExtra", key))
                    {
                        return false;
                    }

                    return intent.Call<bool>("getBooleanExtra", key, false)
                           || string.Equals(intent.Call<string>("getStringExtra", key), "true", StringComparison.OrdinalIgnoreCase)
                           || string.Equals(intent.Call<string>("getStringExtra", key), "1", StringComparison.OrdinalIgnoreCase);
                }
            }
            catch
            {
                return false;
            }
#endif

            return false;
        }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        private static void CiProbeClearIfEnabled()
        {
            if (!HasLaunchFlag(CiProbeFlag))
            {
                return;
            }

            try
            {
                string path = Path.Combine(Application.persistentDataPath, "ci_probe.log");
                File.WriteAllText(path, string.Empty);
                Debug.Log($"Decantra CI Probe reset: {path}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Decantra CI Probe reset failed: {ex.Message}");
            }
        }

        private static void CiProbeAppendIfEnabled(string line)
        {
            if (!HasLaunchFlag(CiProbeFlag))
            {
                return;
            }

            try
            {
                string path = Path.Combine(Application.persistentDataPath, "ci_probe.log");
                string timestamp = DateTime.UtcNow.ToString("O");
                File.AppendAllText(path, $"{timestamp} {line}{Environment.NewLine}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Decantra CI Probe append failed: {ex.Message}");
            }
        }
#endif

        private void ResolveOverlayReferencesIfMissing()
        {
            _optionsOverlay = ResolveOverlayReference(_optionsOverlay, "OptionsOverlay", null);

            Transform optionsParent = _optionsOverlay != null ? _optionsOverlay.transform : null;
            _howToPlayOverlay = ResolveOverlayReference(_howToPlayOverlay, "HowToPlayOverlay", optionsParent);
            _privacyPolicyOverlay = ResolveOverlayReference(_privacyPolicyOverlay, "PrivacyPolicyOverlay", optionsParent);
            _termsOverlay = ResolveOverlayReference(_termsOverlay, "TermsOverlay", optionsParent);
        }

        private void ResolveTutorialManagerIfMissing()
        {
            if (tutorialManager == null || tutorialManager.gameObject == null || tutorialManager.gameObject.scene != gameObject.scene)
            {
                tutorialManager = FindTutorialManagerInCurrentScene();
            }

            if (tutorialManager != null && _settingsStore != null)
            {
                tutorialManager.Initialize(this, _settingsStore);
            }
        }

        private TutorialManager FindTutorialManagerInCurrentScene()
        {
            TutorialManager bestMatch = null;
            int bestScore = int.MinValue;
            var allManagers = Resources.FindObjectsOfTypeAll<TutorialManager>();
            for (int i = 0; i < allManagers.Length; i++)
            {
                var candidate = allManagers[i];
                if (candidate == null || candidate.hideFlags != HideFlags.None)
                {
                    continue;
                }

                var candidateObject = candidate.gameObject;
                if (candidateObject == null || !candidateObject.scene.IsValid() || !candidateObject.scene.isLoaded)
                {
                    continue;
                }

                int score = 0;
                if (candidateObject.scene == gameObject.scene)
                {
                    score += 1000;
                }

                if (candidateObject.activeInHierarchy)
                {
                    score += 100;
                }
                else if (candidateObject.activeSelf)
                {
                    score += 50;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestMatch = candidate;
                }
            }

            return bestMatch;
        }

        private GameObject ResolveOverlayReference(GameObject current, string expectedName, Transform preferredParent)
        {
            if (IsOverlayReferenceValid(current, expectedName, preferredParent))
            {
                return current;
            }

            return FindOverlayByName(expectedName, preferredParent);
        }

        private bool IsOverlayReferenceValid(GameObject candidate, string expectedName, Transform preferredParent)
        {
            if (candidate == null || candidate.name != expectedName)
            {
                return false;
            }

            if (!candidate.scene.IsValid() || !candidate.scene.isLoaded)
            {
                return false;
            }

            if (candidate.scene != gameObject.scene)
            {
                return false;
            }

            if (preferredParent != null && !candidate.transform.IsChildOf(preferredParent))
            {
                return false;
            }

            return true;
        }

        private GameObject FindOverlayByName(string name, Transform preferredParent)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            GameObject bestMatch = null;
            int bestScore = int.MinValue;
            var allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
            for (int i = 0; i < allObjects.Length; i++)
            {
                var candidate = allObjects[i];
                if (candidate == null || candidate.hideFlags != HideFlags.None)
                {
                    continue;
                }

                if (!candidate.scene.IsValid() || !candidate.scene.isLoaded)
                {
                    continue;
                }

                if (candidate.name != name)
                {
                    continue;
                }

                int score = 0;
                if (preferredParent != null && candidate.transform.IsChildOf(preferredParent))
                {
                    score += 1000;
                }

                if (candidate.scene == gameObject.scene)
                {
                    score += 200;
                }

                if (candidate.activeInHierarchy)
                {
                    score += 100;
                }
                else if (candidate.activeSelf)
                {
                    score += 50;
                }

                if (candidate.transform.parent != null)
                {
                    score += 10;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestMatch = candidate;
                }
            }

            return bestMatch;
        }

        private void ApplyStarfieldConfig()
        {
            if (_starfieldConfig == null) return;

            if (backgroundStars != null)
            {
                backgroundStars.SetActive(_starfieldConfig.Enabled);
            }

            if (_starfieldMaterial != null)
            {
                _starfieldMaterial.SetFloat("_StarDensity", _starfieldConfig.Density);
                _starfieldMaterial.SetFloat("_StarSpeed", _starfieldConfig.Speed);
                _starfieldMaterial.SetFloat("_StarBrightness", _starfieldConfig.Brightness);
            }
        }

        private IEnumerator BeginSession()
        {
            _inputLocked = true;

            if (introBanner != null)
            {
                introBanner.ShowBlackOverlayImmediate();
                yield return null;
            }

            if (_state == null)
            {
                LoadLevel(_currentLevel, _currentSeed);
            }

            // Keep gameplay fully hidden while background transitions and first-frame
            // post-load visual adjustments settle, then fade in uniformly from black.
            yield return new WaitForSecondsRealtime(StartupVisualSettleSeconds);

            if (introBanner != null)
            {
                yield return introBanner.FadeToClear(StartupFadeDurationSeconds);
            }

            _inputLocked = false;

            if (tutorialManager != null && !ShouldSuppressAutoTutorialForAutomation())
            {
                tutorialManager.BeginIfFirstLaunch();
            }
        }

        private void PersistCurrentProgress(int levelIndex, int seed)
        {
            if (_progress == null) return;
            _progress.CurrentLevel = Mathf.Max(1, levelIndex);
            _progress.CurrentSeed = seed > 0 ? seed : _progress.CurrentSeed;
            _progressStore.Save(_progress);
        }

        private int ResolveAwardedStars(int baseStars)
        {
            return StarEconomy.ResolveAwardedStars(baseStars, _levelResetCount, _isCurrentLevelAssisted);
        }

        private int ResolveAwardedScore(int baseScore)
        {
            return StarEconomy.ResolveAwardedScore(baseScore, _levelResetCount, _isCurrentLevelAssisted);
        }

        private static float ResolveResetMultiplier(int resetCount)
        {
            return StarEconomy.ResolveResetMultiplier(resetCount);
        }

        private bool HasAnySinkBottle()
        {
            if (_state == null) return false;
            for (int i = 0; i < _state.Bottles.Count; i++)
            {
                if (_state.Bottles[i].IsSink) return true;
            }

            return false;
        }

        private int ResolveAutoSolveCost()
        {
            return StarEconomy.ResolveAutoSolveCost(_currentDifficulty100);
        }

        private int ResolveDifficultyTier()
        {
            return StarEconomy.ResolveDifficultyTier(_currentDifficulty100);
        }

        private bool TrySpendStars(int cost)
        {
            if (_progress == null || cost <= 0) return false;
            if (!StarEconomy.TrySpend(_progress.StarBalance, cost, out int newBalance))
                return false;

            _progress.StarBalance = newBalance;
            _progressStore.Save(_progress);
            Render();
            return true;
        }

        private void RefundStars(int stars)
        {
            if (_progress == null || stars <= 0) return;
            _progress.StarBalance = StarEconomy.Refund(_progress.StarBalance, stars);
            _progressStore.Save(_progress);
            Render();
        }

        private void ExecuteConvertSinksTradeIn()
        {
            int convertCost = StarEconomy.ConvertSinksCost;
            if (_state == null) return;
            if (!HasAnySinkBottle()) return;
            if (!TrySpendStars(convertCost)) return;

            _isCurrentLevelAssisted = true;
            ConvertAllSinksToNormalBottles();
        }

        private void ConvertAllSinksToNormalBottles()
        {
            if (_state == null) return;

            var converted = new List<Bottle>(_state.Bottles.Count);
            for (int i = 0; i < _state.Bottles.Count; i++)
            {
                var bottle = _state.Bottles[i];
                var slots = new ColorId?[bottle.Slots.Count];
                for (int s = 0; s < bottle.Slots.Count; s++)
                {
                    slots[s] = bottle.Slots[s];
                }

                converted.Add(new Bottle(slots, isSink: false));
            }

            _state = new LevelState(
                converted,
                _state.MovesUsed,
                _state.MovesAllowed,
                _state.OptimalMoves,
                _state.LevelIndex,
                _state.Seed,
                _state.ScrambleMoves,
                _state.BackgroundPaletteIndex);

            if (_initialState != null)
            {
                var initialConverted = new List<Bottle>(_initialState.Bottles.Count);
                for (int i = 0; i < _initialState.Bottles.Count; i++)
                {
                    var bottle = _initialState.Bottles[i];
                    var slots = new ColorId?[bottle.Slots.Count];
                    for (int s = 0; s < bottle.Slots.Count; s++)
                    {
                        slots[s] = bottle.Slots[s];
                    }

                    initialConverted.Add(new Bottle(slots, isSink: false));
                }

                _initialState = new LevelState(
                    initialConverted,
                    0,
                    _initialState.MovesAllowed,
                    _initialState.OptimalMoves,
                    _initialState.LevelIndex,
                    _initialState.Seed,
                    _initialState.ScrambleMoves,
                    _initialState.BackgroundPaletteIndex);
            }

            Render();
        }

        private void ExecuteAutoSolveTradeIn()
        {
            if (_state == null || _isAutoSolving) return;
            StartCoroutine(RunAutoSolveTradeIn());
        }

        private IEnumerator RunAutoSolveTradeIn()
        {
            if (_state == null || _isAutoSolving) yield break;

            int cost = ResolveAutoSolveCost();
            if (!TrySpendStars(cost))
            {
                Debug.LogWarning($"Decantra AutoSolveStartRejected reason=insufficient-stars-or-progress-null cost={cost} balance={_progress?.StarBalance ?? -1} level={_currentLevel}");
                yield break;
            }

            _isCurrentLevelAssisted = true;
            _isAutoSolving = true;
            _inputLocked = true;
            _levelResetCount++;
            RestartCurrentLevel();

            // RestartCurrentLevel -> ApplyLoadedState resets runtime flags for normal gameplay.
            // Re-assert auto-solve execution flags so the first orchestrated move is allowed.
            _isCurrentLevelAssisted = true;
            _isAutoSolving = true;
            _inputLocked = true;

            var solveResult = _solver.SolveWithPath(_state, 8_000_000, 8_000, allowSinkMoves: true);
            if (solveResult == null || solveResult.OptimalMoves < 0 || solveResult.Path == null || solveResult.Path.Count == 0)
            {
                Debug.LogWarning($"Decantra AutoSolveStartRejected reason=no-solver-path level={_state.LevelIndex} seed={_state.Seed} optimal={(solveResult != null ? solveResult.OptimalMoves : -999)} pathCount={(solveResult?.Path != null ? solveResult.Path.Count : -1)}");
                RefundStars(cost);
                _isAutoSolving = false;
                _inputLocked = false;
                yield break;
            }

            Debug.Log($"Decantra AutoSolveStarted level={_state.LevelIndex} seed={_state.Seed} moves={solveResult.Path.Count} cost={cost}");

            for (int i = 0; i < solveResult.Path.Count; i++)
            {
                if (_state == null)
                {
                    RefundStars(cost);
                    ResetAutoSolveVisualStateImmediate();
                    _isAutoSolving = false;
                    _autoSolvePlaybackActive = false;
                    yield break;
                }

                var move = solveResult.Path[i];
                _autoSolveMoveDuration = ResolveAutoSolveMoveDuration(_state.LevelIndex, i);
                _autoSolvePlaybackActive = true;
                MoveExecutionResult stepResult = new MoveExecutionResult(success: false, poured: 0, rejectionReason: "not-started");
                bool stepCompleted = false;
                yield return PerformMove(
                    move.Source,
                    move.Target,
                    i,
                    animateDrag: true,
                    onCompleted: result =>
                    {
                        stepResult = result;
                        stepCompleted = true;
                    });

                if (!stepCompleted || !stepResult.Success)
                {
                    Debug.LogWarning($"Decantra AutoSolveStepRejected step={i} reason={stepResult.RejectionReason} source={move.Source} target={move.Target} level={_state?.LevelIndex ?? -1}");
                    EmitAutoSolveDiagnostic(
                        $"MoveRejected(reason={stepResult.RejectionReason})",
                        $"stepId={i} source={move.Source} target={move.Target}");
                    RefundStars(cost);
                    ResetAutoSolveVisualStateImmediate();
                    _autoSolvePlaybackActive = false;
                    RestartCurrentLevel();
                    _isAutoSolving = false;
                    _inputLocked = false;
                    yield break;
                }

                _autoSolvePlaybackActive = false;
            }

            ResetAutoSolveVisualStateImmediate();
            _isAutoSolving = false;
            _inputLocked = false;
        }

        private IEnumerator AnimateAutoSolveDrag(int sourceIndex, int targetIndex)
        {
            var sourceView = GetBottleView(sourceIndex);
            var targetView = GetBottleView(targetIndex);
            if (sourceView == null || targetView == null)
            {
                yield break;
            }

            var sourceRect = sourceView.transform as RectTransform;
            var targetRect = targetView.transform as RectTransform;
            if (sourceRect == null || targetRect == null)
            {
                yield break;
            }

            Vector2 start = sourceRect.anchoredPosition;
            Vector2 end = targetRect.anchoredPosition;
            float maxDistance = ResolveMaxBottleDistance();
            float distance = Vector2.Distance(start, end);
            float normalizedDistance = maxDistance > 0f ? Mathf.Clamp01(distance / maxDistance) : 0f;
            float duration = Mathf.Lerp(AutoSolveMinDragSeconds, AutoSolveMaxDragSeconds, normalizedDistance)
                             * AutoSolveDragSlowdownMultiplier;
            float lift = Mathf.Lerp(18f, 52f, normalizedDistance);

            if (_autoSolveReturnRoutine != null)
            {
                StopCoroutine(_autoSolveReturnRoutine);
                _autoSolveReturnRoutine = null;
            }

            _autoSolveActiveRect = sourceRect;
            _autoSolveStartAnchoredPosition = start;
            _autoSolveStartLocalRotation = sourceRect.localRotation;
            _autoSolveVisualActive = true;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                Vector2 planar = Vector2.Lerp(start, end, t);
                float arc = Mathf.Sin(t * Mathf.PI) * lift;
                float tiltProgress = Mathf.Clamp01((t - AutoSolveTiltStartNormalized) / (1f - AutoSolveTiltStartNormalized));
                float tiltAngle = Mathf.Lerp(0f, AutoSolveDragTiltDegrees, tiltProgress);
                sourceRect.anchoredPosition = planar + Vector2.up * arc;
                sourceRect.localRotation = Quaternion.Euler(0f, 0f, -tiltAngle);
                yield return null;
            }

            sourceRect.anchoredPosition = end;
            sourceRect.localRotation = Quaternion.Euler(0f, 0f, -AutoSolveDragTiltDegrees);
            yield return null;
        }

        private void StartAutoSolveReturn(float duration)
        {
            if (!_autoSolveVisualActive || _autoSolveActiveRect == null)
            {
                return;
            }

            if (_autoSolveReturnRoutine != null)
            {
                StopCoroutine(_autoSolveReturnRoutine);
            }

            _autoSolveReturnRoutine = StartCoroutine(AnimateAutoSolveReturn(duration));
        }

        private IEnumerator AnimateAutoSolveReturn(float duration)
        {
            if (!_autoSolveVisualActive || _autoSolveActiveRect == null)
            {
                _autoSolveReturnRoutine = null;
                yield break;
            }

            RectTransform rect = _autoSolveActiveRect;
            Vector2 fromPosition = rect.anchoredPosition;
            Quaternion fromRotation = rect.localRotation;
            Vector2 toPosition = _autoSolveStartAnchoredPosition;
            Quaternion toRotation = _autoSolveStartLocalRotation;
            float returnDuration = Mathf.Max(AutoSolveReturnMinSeconds, duration);

            float elapsed = 0f;
            while (elapsed < returnDuration)
            {
                if (rect == null)
                {
                    ClearAutoSolveVisualState();
                    _autoSolveReturnRoutine = null;
                    yield break;
                }

                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / returnDuration);
                float eased = t * t * (3f - 2f * t);
                rect.anchoredPosition = Vector2.Lerp(fromPosition, toPosition, eased);
                rect.localRotation = Quaternion.Slerp(fromRotation, toRotation, eased);
                yield return null;
            }

            if (rect != null)
            {
                rect.anchoredPosition = toPosition;
                rect.localRotation = toRotation;
            }

            ClearAutoSolveVisualState();
            _autoSolveReturnRoutine = null;
        }

        private void ResetAutoSolveVisualStateImmediate()
        {
            if (_autoSolveReturnRoutine != null)
            {
                StopCoroutine(_autoSolveReturnRoutine);
                _autoSolveReturnRoutine = null;
            }

            if (_autoSolveVisualActive && _autoSolveActiveRect != null)
            {
                _autoSolveActiveRect.anchoredPosition = _autoSolveStartAnchoredPosition;
                _autoSolveActiveRect.localRotation = _autoSolveStartLocalRotation;
            }

            ClearAutoSolveVisualState();
        }

        private void ClearAutoSolveVisualState()
        {
            _autoSolveActiveRect = null;
            _autoSolveStartAnchoredPosition = Vector2.zero;
            _autoSolveStartLocalRotation = Quaternion.identity;
            _autoSolveVisualActive = false;
        }

        private float ResolveMaxBottleDistance()
        {
            if (bottleViews == null || bottleViews.Count < 2)
            {
                return 1f;
            }

            float maxDistance = 1f;
            for (int i = 0; i < bottleViews.Count; i++)
            {
                var a = bottleViews[i];
                var aRect = a != null ? a.transform as RectTransform : null;
                if (aRect == null || !a.gameObject.activeInHierarchy) continue;

                for (int j = i + 1; j < bottleViews.Count; j++)
                {
                    var b = bottleViews[j];
                    var bRect = b != null ? b.transform as RectTransform : null;
                    if (bRect == null || !b.gameObject.activeInHierarchy) continue;

                    float distance = Vector2.Distance(aRect.anchoredPosition, bRect.anchoredPosition);
                    if (distance > maxDistance)
                    {
                        maxDistance = distance;
                    }
                }
            }

            return maxDistance;
        }

        private static float ResolveAutoSolveMoveDuration(int levelIndex, int moveIndex)
        {
            unchecked
            {
                int hash = levelIndex * 73856093 ^ moveIndex * 19349663;
                int bucket = Math.Abs(hash % 5);
                return 0.8f + bucket * 0.1f;
            }
        }

        private int CalculateStars(int optimalMoves, int movesUsed, int movesAllowed)
        {
            return ScoreCalculator.CalculateStars(optimalMoves, movesUsed, movesAllowed);
        }

        private bool TryApplyMoveAndScore(int sourceIndex, int targetIndex, out int poured)
        {
            poured = 0;
            if (_state == null) return false;
            if (sourceIndex < 0 || sourceIndex >= _state.Bottles.Count) return false;
            if (targetIndex < 0 || targetIndex >= _state.Bottles.Count) return false;
            bool applied = _state.TryApplyMove(sourceIndex, targetIndex, out poured);
            if (!applied) return false;

            _scoreSession?.UpdateProvisional(_state.OptimalMoves, _state.MovesUsed, _state.MovesAllowed, _currentDifficulty100, IsCleanSolve);

#if DEVELOPMENT_BUILD || UNITY_EDITOR
            Debug.Log($"Decantra ScoreUpdate level={_state.LevelIndex} movesUsed={_state.MovesUsed} optimal={_state.OptimalMoves} provisional={_scoreSession?.ProvisionalScore ?? 0}");
            AppendDebugLog($"ScoreUpdate level={_state.LevelIndex} movesUsed={_state.MovesUsed} optimal={_state.OptimalMoves} provisional={_scoreSession?.ProvisionalScore ?? 0}");
#endif

            return true;
        }

        private bool IsCleanSolve => !_usedUndo && !_usedHints && !_usedRestart;

        private void PlayPourSfx(float previousFillRatio, float newFillRatio)
        {
            if (!_sfxEnabled || _audioManager == null || _state == null) return;
            _audioManager.PlayPourSegment(previousFillRatio, newFillRatio);
        }

        private float ResolvePourWindowDuration(float previousFillRatio, float newFillRatio, int poured)
        {
            if (_audioManager == null)
            {
                return Mathf.Max(0.2f, 0.12f * poured);
            }

            return _audioManager.CalculatePourWindowDuration(previousFillRatio, newFillRatio);
        }

        private void SetupAudio()
        {
            _audioManager = gameObject.GetComponent<AudioManager>() ?? gameObject.AddComponent<AudioManager>();
            _audioManager.SetEnabled(_sfxEnabled);
            _audioManager.SetVolume01(_sfxVolume01);
        }

        private void PlayButtonSfx()
        {
            if (!_sfxEnabled || _audioManager == null) return;
            _audioManager.PlayButtonClick();
        }

        private void PlayLevelCompleteSfx()
        {
            if (!_sfxEnabled || _audioManager == null) return;
            _audioManager.PlayLevelComplete();
        }

        private void PlayBottleFullSfx()
        {
            if (!_sfxEnabled || _audioManager == null) return;
            _audioManager.PlayBottleFull();
        }

        private void ApplyAccessibilitySettings()
        {
            for (int i = 0; i < bottleViews.Count; i++)
            {
                var view = bottleViews[i];
                if (view == null) continue;
                view.SetAccessibleColorsEnabled(_colorBlindAssistEnabled);
            }

            if (_highContrastOverlay != null)
            {
                _highContrastOverlay.SetActive(_highContrastEnabled);
            }
        }

        public bool TryGetSinkBottleObjectName(out string objectName)
        {
            objectName = null;
            if (_state == null) return false;

            for (int i = 0; i < _state.Bottles.Count && i < bottleViews.Count; i++)
            {
                var bottle = _state.Bottles[i];
                if (bottle == null || !bottle.IsSink) continue;
                var view = bottleViews[i];
                if (view == null) continue;
                objectName = view.gameObject.name;
                return true;
            }

            return false;
        }

        public bool IsCurrentLevelCompleted(int levelIndex)
        {
            if (_progress?.CompletedLevels == null) return false;
            return _progress.CompletedLevels.Contains(levelIndex);
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

        private int CalculateSeedForLevel(int targetLevel)
        {
            int seed = 0;
            int clampedLevel = Mathf.Max(1, targetLevel);
            for (int level = 1; level <= clampedLevel; level++)
            {
                seed = NextSeed(level, seed);
            }
            return seed;
        }

        private IEnumerator TransitionToLevel(int nextLevel, int seed)
        {
            TryApplyCompletedPrecompute("transition-start");
            EmitPrecomputeDiagnostic("TransitionStart", $"nextLevel={nextLevel} seed={seed} precomputeReady={_nextState != null}");
            ApplyBackgroundVariation(nextLevel, seed, ResolveBackgroundPaletteIndex(nextLevel, seed));

            if (_nextState != null && _nextLevel == nextLevel)
            {
                EmitCompletionToReadyMetric(nextLevel, "precomputed");
                _currentDifficulty100 = _nextDifficulty100;
                ApplyLoadedState(_nextState, _nextLevel, _nextSeed);
                _inputLocked = false;
                yield break;
            }

            CancelPrecompute();

            GeneratedLevel loaded = default;
            bool hasLoaded = false;
            int attempt = 0;
            int currentSeed = seed;
            while (!hasLoaded && attempt < 2)
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

                    if (task.IsCompletedSuccessfully && task.Result.State != null)
                    {
                        loaded = task.Result;
                        hasLoaded = true;
                    }
                    else
                    {
                        attempt++;
                        currentSeed = NextSeed(nextLevel, currentSeed + 97);
                    }
                }
            }

            if (!hasLoaded)
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

                    if (task.IsCompletedSuccessfully && task.Result.State != null)
                    {
                        loaded = task.Result;
                        hasLoaded = true;
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

            if (!hasLoaded)
            {
                loaded = GenerateLevelWithRetry(nextLevel, currentSeed, 2);
                hasLoaded = loaded.State != null;
            }

            if (!hasLoaded)
            {
                int emergencyAttempts = 0;
                int emergencySeed = NextSeed(nextLevel, currentSeed + 97);
                while (!hasLoaded && emergencyAttempts < 2)
                {
                    loaded = GenerateLevelWithRetry(nextLevel, emergencySeed, 2);
                    hasLoaded = loaded.State != null;
                    emergencyAttempts++;
                    emergencySeed = NextSeed(nextLevel, emergencySeed + 97);
                }
            }

            if (!hasLoaded)
            {
#if UNITY_EDITOR
                Debug.LogWarning($"TransitionToLevel failed to generate level={nextLevel}");
#endif
                _inputLocked = false;
                yield break;
            }

            EmitCompletionToReadyMetric(nextLevel, "synchronous-fallback");
            _currentDifficulty100 = loaded.Difficulty100;
            ApplyLoadedState(loaded.State, nextLevel, currentSeed);
            _inputLocked = false;
        }

        public void RequestRestartGame()
        {
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
            _usedRestart = false;
            _selectedIndex = -1;
            _isCompleting = false;
            _isFailing = false;

            _progress = new ProgressData
            {
                HighestUnlockedLevel = 1,
                CurrentLevel = 1,
                CurrentSeed = 0,
                CurrentScore = 0,
                StarBalance = 0,
                HighScore = 0,
                CompletedLevels = new List<int>(),
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
            _usedRestart = false;
            _isCurrentLevelAssisted = false;
            _levelResetCount = 0;
            _isAutoSolving = false;
            _autoSolvePlaybackActive = false;
            int attemptTotal = _progress?.CurrentScore ?? _scoreSession?.TotalScore ?? 0;
            _scoreSession?.BeginAttempt(attemptTotal);
            _nextState = null;
            ApplyBackgroundVariation(_currentLevel, _currentSeed, _state?.BackgroundPaletteIndex ?? -1);
            StartPrecomputeNextLevel();
            PersistCurrentProgress(_currentLevel, _currentSeed);
            InitializeLevelAudio(_currentLevel, _currentSeed);
            Render();

#if DEVELOPMENT_BUILD || UNITY_EDITOR
            Debug.Log($"Decantra LevelLoaded level={_currentLevel} seed={_currentSeed}");
#endif
            TrackLevelLoadPrecomputeExpectations(_currentLevel);
            StartCoroutine(EmitNextLevelFirstFrame(_levelSessionId, _currentLevel));
        }

        private void InitializeLevelAudio(int levelIndex, int seed)
        {
            if (_audioManager == null) return;

            _audioManager.SelectPourClipForLevel(levelIndex, seed);
        }

        private void TryPlayStageUnlockedTransitionSfx(int nextLevel)
        {
            if (_audioManager == null) return;

            if (!_sfxEnabled) return;
            if (nextLevel <= 0 || nextLevel % 10 != 0) return;
            if (_lastStageUnlockSfxLevel == nextLevel) return;

            _lastStageUnlockSfxLevel = nextLevel;
            _audioManager.PlayStageUnlocked();
        }

        private void ApplyBackgroundVariation(int levelIndex, int seed, int backgroundPaletteIndex)
        {
            var archetype = BackgroundGeneratorRegistry.SelectArchetypeForLevel(levelIndex, seed);
            UpdateStarfieldState(levelIndex, archetype);
            if (backgroundImage == null) return;

#if DEVELOPMENT_BUILD || UNITY_EDITOR
            var backgroundTimer = System.Diagnostics.Stopwatch.StartNew();
#endif

            // Update structural background sprites based on level (deterministic per level)
            SceneBootstrap.UpdateBackgroundSpritesForLevel(levelIndex, seed, backgroundFlow, backgroundShapes, backgroundBubbles, backgroundLargeStructure, backgroundDetail);

            int zoneIndex = BackgroundRules.GetZoneIndex(levelIndex);
            var levelVariant = BackgroundRules.GetLevelVariant(levelIndex, seed, BackgroundPalettes.Length);
            var zoneLayout = GetZoneLayout(zoneIndex, seed);
            int backgroundKey = (int)archetype;

            int paletteIndex = backgroundPaletteIndex >= 0
                ? backgroundPaletteIndex
                : levelVariant.PaletteIndex;
            var palette = BackgroundPalettes[paletteIndex];

            float colorJitter = Mathf.Repeat(levelVariant.PhaseOffset / (Mathf.PI * 2f), 1f);
            float colorJitter2 = Mathf.Repeat(levelVariant.PhaseOffset * 0.37f, 1f);

            var family = GetBackgroundFamilyProfile(archetype, levelVariant, palette, zoneLayout);
            float hue = Mathf.Repeat(family.Hue, 1f);
            float sat = Mathf.Clamp01(family.Saturation + Mathf.Lerp(-0.03f, 0.03f, colorJitter2));
            float val = Mathf.Clamp01(family.Value + Mathf.Lerp(-0.04f, 0.04f, 1f - colorJitter));

            Color baseTint = Color.HSVToRGB(hue, sat, val);
            baseTint.a = 1f;

            Color detailTint = Color.HSVToRGB(Mathf.Repeat(hue + 0.02f, 1f), palette.DetailSaturation, Mathf.Clamp01(palette.DetailValue + 0.04f * (colorJitter - 0.5f)));
            detailTint.a = Mathf.Lerp(0.1f, 0.2f, colorJitter2) * family.DetailAlphaScale;

            Color flowTint = Color.HSVToRGB(Mathf.Repeat(hue + 0.08f, 1f), palette.FlowSaturation, palette.FlowValue);
            flowTint.a = Mathf.Lerp(palette.FlowAlpha * 0.75f, palette.FlowAlpha * 1.35f, colorJitter) * family.FlowAlphaScale;

            Color shapeTint = Color.HSVToRGB(Mathf.Repeat(hue - 0.05f, 1f), palette.ShapeSaturation, palette.ShapeValue);
            shapeTint.a = Mathf.Lerp(palette.ShapeAlpha * 0.7f, palette.ShapeAlpha * 1.25f, colorJitter2) * family.ShapeAlphaScale;

            Color macroTint = Color.HSVToRGB(Mathf.Repeat(hue + 0.03f, 1f), palette.ShapeSaturation * 0.95f, Mathf.Clamp01(palette.ShapeValue + 0.03f));
            macroTint.a = Mathf.Lerp(palette.ShapeAlpha * 0.6f, palette.ShapeAlpha * 1.15f, colorJitter) * family.MacroAlphaScale;

            Color bubbleTint = Color.HSVToRGB(Mathf.Repeat(hue + 0.12f, 1f), palette.DetailSaturation * 0.9f, Mathf.Clamp01(palette.DetailValue + 0.02f));
            bubbleTint.a = Mathf.Lerp(palette.FlowAlpha * 0.4f, palette.FlowAlpha * 0.9f, colorJitter2) * family.BubbleAlphaScale;

            // Levels 1-9: "Midnight Ocean" — deep blue/indigo intro theme
            if (levelIndex <= 9)
            {
                baseTint = new Color(0.03f, 0.06f, 0.18f, 1f);
                family.GradientTop = new Color(0.04f, 0.08f, 0.22f, 0.24f);
                family.GradientBottom = new Color(0.015f, 0.03f, 0.14f, 0.18f);
                detailTint = new Color(0.10f, 0.18f, 0.34f, 0.30f);
                flowTint = new Color(0.08f, 0.14f, 0.30f, 0.26f);
                shapeTint = new Color(0.10f, 0.18f, 0.32f, 0.22f);
                macroTint = new Color(0.08f, 0.14f, 0.26f, 0.20f);
                bubbleTint = new Color(0.08f, 0.16f, 0.28f, 0.16f);
            }

            // Vignette effect completely disabled
            float vignetteAlpha = 0f;

            // Fix: Scale overlays by 1.25x to ensure they cover screen corners even when rotated
            const float SafeScaleMultiplier = 1.25f;

            float detailScale = zoneLayout.DetailScale * family.DetailScale * SafeScaleMultiplier;
            Vector2 detailOffset = zoneLayout.DetailOffset;

            float flowScale = zoneLayout.FlowScale * family.FlowScale * SafeScaleMultiplier;
            Vector2 flowOffset = zoneLayout.FlowOffset;

            float shapeScale = zoneLayout.ShapeScale * family.ShapeScale * SafeScaleMultiplier;
            Vector2 shapeOffset = zoneLayout.ShapeOffset;

            float flowRotation = zoneLayout.FlowRotation;
            float shapeRotation = zoneLayout.ShapeRotation;

            float macroScale = zoneLayout.MacroScale * family.MacroScale * SafeScaleMultiplier;
            Vector2 macroOffset = zoneLayout.MacroOffset;
            float macroRotation = zoneLayout.MacroRotation;

            float bubbleScale = zoneLayout.BubbleScale * family.BubbleScale * SafeScaleMultiplier;
            Vector2 bubbleOffset = zoneLayout.BubbleOffset;
            float bubbleRotation = zoneLayout.BubbleRotation;

            if (_currentBackgroundFamily != backgroundKey)
            {
                if (_backgroundTransition != null)
                {
                    StopCoroutine(_backgroundTransition);
                }
                _backgroundTransition = StartCoroutine(AnimateBackgroundTransition(baseTint, detailTint, flowTint, shapeTint, macroTint, bubbleTint, vignetteAlpha, detailScale, detailOffset, flowScale, flowOffset, flowRotation, shapeScale, shapeOffset, shapeRotation, macroScale, macroOffset, macroRotation, bubbleScale, bubbleOffset, bubbleRotation, backgroundKey, family));
                _currentBackgroundFamily = backgroundKey;
            }
            else
            {
                ApplyBackgroundVisuals(baseTint, detailTint, flowTint, shapeTint, macroTint, bubbleTint, vignetteAlpha, detailScale, detailOffset, flowScale, flowOffset, flowRotation, shapeScale, shapeOffset, shapeRotation, macroScale, macroOffset, macroRotation, bubbleScale, bubbleOffset, bubbleRotation, backgroundKey, family);
            }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
            backgroundTimer.Stop();
            Debug.Log($"Decantra Background level={levelIndex} seed={seed} palette={paletteIndex} archetype={archetype} base={backgroundImage.color} applyMs={backgroundTimer.Elapsed.TotalMilliseconds:0.0}");
            AppendDebugLog($"Background level={levelIndex} seed={seed} palette={paletteIndex} archetype={archetype} applyMs={backgroundTimer.Elapsed.TotalMilliseconds:0.0}");
#endif
        }

        private void UpdateStarfieldState(int levelIndex, GeneratorArchetype archetype)
        {
            if (backgroundStars == null) return;
            // User's starfield toggle overrides per-level archetype logic
            bool userEnabled = _starfieldConfig != null && _starfieldConfig.Enabled;
            bool enabled = userEnabled && ShouldEnableStars(levelIndex, archetype);
            if (backgroundStars.activeSelf != enabled)
            {
                backgroundStars.SetActive(enabled);
            }
        }

        /// <summary>
        /// Stars are shown on cosmic / hazy / cloud-like archetypes and hidden on
        /// clearly terrestrial / botanical / crystalline ones.
        /// </summary>
        private static bool ShouldEnableStars(int levelIndex, GeneratorArchetype archetype)
        {
            _ = levelIndex;
            return IsStarfieldTheme(archetype);
        }

        /// <summary>
        /// Returns true for archetypes whose visual character is cloud-like, hazy,
        /// or could be associated with cosmic/universe imagery.  Majority of
        /// archetypes (9 of 16) qualify; the remaining 7 are clearly terrestrial
        /// (botanical, crystalline, floral).
        /// </summary>
        internal static bool IsStarfieldTheme(GeneratorArchetype archetype)
        {
            switch (archetype)
            {
                case GeneratorArchetype.DomainWarpedClouds:
                case GeneratorArchetype.CurlFlowAdvection:
                case GeneratorArchetype.AtmosphericWash:
                case GeneratorArchetype.NebulaGlow:
                case GeneratorArchetype.MarbledFlow:
                case GeneratorArchetype.ConcentricRipples:
                case GeneratorArchetype.ImplicitBlobHaze:
                case GeneratorArchetype.OrganicCells:
                case GeneratorArchetype.FractalEscapeDensity:
                    return true;
                default:
                    return false;
            }
        }

        private static ZoneLayout GetZoneLayout(int zoneIndex, int seed)
        {
            var key = new ZoneLayoutKey(seed, zoneIndex);
            if (ZoneLayouts.TryGetValue(key, out var cached))
            {
                return cached;
            }

            ulong zoneSeed = BackgroundRules.GetZoneSeed(seed, zoneIndex);
            var rng = new LayoutRng(zoneSeed ^ 0xC15E7F1Bu);

            var layout = new ZoneLayout
            {
                DetailScale = NextRange(rng, 0.95f, 1.15f),
                DetailOffset = new Vector2(NextRange(rng, -10f, 10f), NextRange(rng, -10f, 10f)),
                FlowScale = NextRange(rng, 1.02f, 1.28f),
                FlowOffset = new Vector2(NextRange(rng, -18f, 18f), NextRange(rng, -18f, 18f)),
                FlowRotation = NextRange(rng, -6f, 6f),
                ShapeScale = NextRange(rng, 1.0f, 1.26f),
                ShapeOffset = new Vector2(NextRange(rng, -14f, 14f), NextRange(rng, -14f, 14f)),
                ShapeRotation = NextRange(rng, -5f, 5f),
                MacroScale = NextRange(rng, 0.98f, 1.22f),
                MacroOffset = new Vector2(NextRange(rng, -22f, 22f), NextRange(rng, -22f, 22f)),
                MacroRotation = NextRange(rng, -4f, 4f),
                BubbleScale = NextRange(rng, 0.98f, 1.18f),
                BubbleOffset = new Vector2(NextRange(rng, -16f, 16f), NextRange(rng, -16f, 16f)),
                BubbleRotation = NextRange(rng, -6f, 6f),
                GradientDirection = NextRange(rng, 0f, 360f),
                GradientIntensity = NextRange(rng, 0.25f, 0.6f)
            };

            ZoneLayouts[key] = layout;
            return layout;
        }
        private static float NextRange(LayoutRng rng, float min, float max)
        {
            return Mathf.Lerp(min, max, rng.NextFloat());
        }

        private sealed class LayoutRng
        {
            private uint _state;

            public LayoutRng(ulong seed)
            {
                _state = (uint)(seed ^ (seed >> 32));
                if (_state == 0)
                {
                    _state = 0x9E3779B9u;
                }
            }

            public float NextFloat()
            {
                _state = 1664525u * _state + 1013904223u;
                return (_state & 0x00FFFFFF) / 16777216f;
            }
        }

        private static BackgroundFamilyProfile GetBackgroundFamilyProfile(GeneratorArchetype archetype, LevelVariant levelVariant, BackgroundPalette palette, ZoneLayout zoneLayout)
        {
            float hue = Mathf.Repeat(palette.Hue + levelVariant.HueShift, 1f);
            float saturation = Mathf.Lerp(levelVariant.SaturationLow, levelVariant.SaturationHigh, 0.5f);
            float value = Mathf.Lerp(levelVariant.ValueLow, levelVariant.ValueHigh, 0.5f);
            float gradientShift = (zoneLayout.GradientIntensity - 0.2f) * 0.08f;

            float topValue = Mathf.Clamp01(value + zoneLayout.GradientIntensity * 0.18f);
            float bottomValue = Mathf.Clamp01(value - zoneLayout.GradientIntensity * 0.18f);

            Color top = Color.HSVToRGB(hue, saturation, topValue);
            Color bottom = Color.HSVToRGB(hue, saturation, bottomValue);
            // Make gradient translucent to allow starfield to show through
            // Very low alpha (0.25-0.35) ensures stars remain clearly visible
            top.a = 0.26f;
            bottom.a = 0.30f;

            GetLayerWeightsForArchetype(archetype, out float macroWeight, out float mesoWeight, out float microWeight);

            float accentStrength = Mathf.Lerp(0.65f, 1.25f, levelVariant.AccentStrength);

            if (levelVariant.LevelIndex <= 3)
            {
                float calmT = Mathf.Clamp01((levelVariant.LevelIndex - 1f) / 2f);
                float calmValue = Mathf.Lerp(0.26f, 0.42f, calmT);
                value = Mathf.Min(value, calmValue);
                saturation = Mathf.Lerp(saturation, 0.38f, 0.7f);
                accentStrength *= Mathf.Lerp(0.6f, 0.8f, calmT);

                topValue = Mathf.Clamp01(value + zoneLayout.GradientIntensity * 0.12f);
                bottomValue = Mathf.Clamp01(value - zoneLayout.GradientIntensity * 0.12f);
                top = Color.HSVToRGB(hue, saturation, topValue);
                bottom = Color.HSVToRGB(hue, saturation, bottomValue);
            }
            float detailAlpha = Mathf.Lerp(0.85f, 1.2f, microWeight) * accentStrength;
            float flowAlpha = Mathf.Lerp(0.9f, 1.2f, mesoWeight) * accentStrength;
            float shapeAlpha = Mathf.Lerp(0.85f, 1.2f, macroWeight) * accentStrength;
            float macroAlpha = Mathf.Lerp(0.8f, 1.25f, macroWeight) * accentStrength;
            float bubbleAlpha = Mathf.Lerp(0.75f, 1.2f, microWeight) * accentStrength;
            // Vignette effect completely disabled

            float detailScale = Mathf.Lerp(0.95f, 1.25f, microWeight);
            float flowScale = Mathf.Lerp(0.95f, 1.3f, mesoWeight);
            float shapeScale = Mathf.Lerp(0.95f, 1.35f, macroWeight);
            float macroScale = Mathf.Lerp(0.95f, 1.3f, macroWeight);
            float bubbleScale = Mathf.Lerp(0.95f, 1.2f, microWeight);

            return new BackgroundFamilyProfile
            {
                Hue = hue,
                Saturation = saturation,
                Value = value,
                DetailAlphaScale = detailAlpha,
                FlowAlphaScale = flowAlpha,
                ShapeAlphaScale = shapeAlpha,
                MacroAlphaScale = macroAlpha,
                BubbleAlphaScale = bubbleAlpha,
                DetailScale = detailScale,
                FlowScale = flowScale,
                ShapeScale = shapeScale,
                MacroScale = macroScale,
                BubbleScale = bubbleScale,
                GradientTop = top,
                GradientBottom = bottom,
                GradientDirection = zoneLayout.GradientDirection
            };
        }

        private static void GetLayerWeightsForArchetype(GeneratorArchetype archetype, out float macroWeight, out float mesoWeight, out float microWeight)
        {
            switch (archetype)
            {
                case GeneratorArchetype.AtmosphericWash:
                case GeneratorArchetype.DomainWarpedClouds:
                    macroWeight = 0.25f;
                    mesoWeight = 0.45f;
                    microWeight = 0.3f;
                    break;
                case GeneratorArchetype.CurlFlowAdvection:
                case GeneratorArchetype.MarbledFlow:
                case GeneratorArchetype.ConcentricRipples:
                    macroWeight = 0.35f;
                    mesoWeight = 0.4f;
                    microWeight = 0.25f;
                    break;
                case GeneratorArchetype.FractalEscapeDensity:
                case GeneratorArchetype.OrganicCells:
                    macroWeight = 0.32f;
                    mesoWeight = 0.33f;
                    microWeight = 0.35f;
                    break;
                default:
                    macroWeight = 0.38f;
                    mesoWeight = 0.34f;
                    microWeight = 0.28f;
                    break;
            }
        }

        private void ApplyBackgroundVisuals(Color baseTint, Color detailTint, Color flowTint, Color shapeTint, Color macroTint, Color bubbleTint, float vignetteAlpha, float detailScale, Vector2 detailOffset, float flowScale, Vector2 flowOffset, float flowRotation, float shapeScale, Vector2 shapeOffset, float shapeRotation, float macroScale, Vector2 macroOffset, float macroRotation, float bubbleScale, Vector2 bubbleOffset, float bubbleRotation, int familyIndex, BackgroundFamilyProfile family)
        {
            if (backgroundImage != null)
            {
                backgroundImage.color = baseTint;
                backgroundImage.sprite = GetFamilyGradientSprite(familyIndex, family);
                backgroundImage.rectTransform.localEulerAngles = new Vector3(0f, 0f, family.GradientDirection);
                // Fix: Scale up significantly to cover screen corners when rotated (diagonal coverage)
                backgroundImage.rectTransform.localScale = new Vector3(2.5f, 2.5f, 1f);
            }

            if (backgroundDetail != null)
            {
                backgroundDetail.color = detailTint;
                backgroundDetail.rectTransform.localScale = new Vector3(detailScale, detailScale, 1f);
                backgroundDetail.rectTransform.anchoredPosition = detailOffset;
            }

            if (backgroundFlow != null)
            {
                backgroundFlow.color = flowTint;
                backgroundFlow.rectTransform.localEulerAngles = new Vector3(0f, 0f, flowRotation);
                backgroundFlow.rectTransform.localScale = new Vector3(flowScale, flowScale, 1f);
                backgroundFlow.rectTransform.anchoredPosition = flowOffset;
            }

            if (backgroundShapes != null)
            {
                backgroundShapes.color = shapeTint;
                backgroundShapes.rectTransform.localEulerAngles = new Vector3(0f, 0f, shapeRotation);
                backgroundShapes.rectTransform.localScale = new Vector3(shapeScale, shapeScale, 1f);
                backgroundShapes.rectTransform.anchoredPosition = shapeOffset;
            }

            if (backgroundLargeStructure != null)
            {
                backgroundLargeStructure.color = macroTint;
                backgroundLargeStructure.rectTransform.localEulerAngles = new Vector3(0f, 0f, macroRotation);
                backgroundLargeStructure.rectTransform.localScale = new Vector3(macroScale, macroScale, 1f);
                backgroundLargeStructure.rectTransform.anchoredPosition = macroOffset;
            }

            if (backgroundBubbles != null)
            {
                backgroundBubbles.color = bubbleTint;
                backgroundBubbles.rectTransform.localEulerAngles = new Vector3(0f, 0f, bubbleRotation);
                backgroundBubbles.rectTransform.localScale = new Vector3(bubbleScale, bubbleScale, 1f);
                backgroundBubbles.rectTransform.anchoredPosition = bubbleOffset;
            }

            if (backgroundVignette != null)
            {
                var vignetteColor = backgroundVignette.color;
                vignetteColor.a = vignetteAlpha;
                backgroundVignette.color = vignetteColor;
            }
        }

        private IEnumerator AnimateBackgroundTransition(Color baseTint, Color detailTint, Color flowTint, Color shapeTint, Color macroTint, Color bubbleTint, float vignetteAlpha, float detailScale, Vector2 detailOffset, float flowScale, Vector2 flowOffset, float flowRotation, float shapeScale, Vector2 shapeOffset, float shapeRotation, float macroScale, Vector2 macroOffset, float macroRotation, float bubbleScale, Vector2 bubbleOffset, float bubbleRotation, int familyIndex, BackgroundFamilyProfile family)
        {
            float time = 0f;
            Color startBase = backgroundImage != null ? backgroundImage.color : baseTint;
            Color startDetail = backgroundDetail != null ? backgroundDetail.color : detailTint;
            Color startFlow = backgroundFlow != null ? backgroundFlow.color : flowTint;
            Color startShape = backgroundShapes != null ? backgroundShapes.color : shapeTint;
            Color startMacro = backgroundLargeStructure != null ? backgroundLargeStructure.color : macroTint;
            Color startBubble = backgroundBubbles != null ? backgroundBubbles.color : bubbleTint;
            float startVignette = backgroundVignette != null ? backgroundVignette.color.a : vignetteAlpha;

            Vector3 startDetailScale = backgroundDetail != null ? backgroundDetail.rectTransform.localScale : Vector3.one;
            Vector2 startDetailOffset = backgroundDetail != null ? backgroundDetail.rectTransform.anchoredPosition : Vector2.zero;
            Vector3 startFlowScale = backgroundFlow != null ? backgroundFlow.rectTransform.localScale : Vector3.one;
            Vector2 startFlowOffset = backgroundFlow != null ? backgroundFlow.rectTransform.anchoredPosition : Vector2.zero;
            float startFlowRotation = backgroundFlow != null ? backgroundFlow.rectTransform.localEulerAngles.z : 0f;
            Vector3 startShapeScale = backgroundShapes != null ? backgroundShapes.rectTransform.localScale : Vector3.one;
            Vector2 startShapeOffset = backgroundShapes != null ? backgroundShapes.rectTransform.anchoredPosition : Vector2.zero;
            float startShapeRotation = backgroundShapes != null ? backgroundShapes.rectTransform.localEulerAngles.z : 0f;
            Vector3 startMacroScale = backgroundLargeStructure != null ? backgroundLargeStructure.rectTransform.localScale : Vector3.one;
            Vector2 startMacroOffset = backgroundLargeStructure != null ? backgroundLargeStructure.rectTransform.anchoredPosition : Vector2.zero;
            float startMacroRotation = backgroundLargeStructure != null ? backgroundLargeStructure.rectTransform.localEulerAngles.z : 0f;
            Vector3 startBubbleScale = backgroundBubbles != null ? backgroundBubbles.rectTransform.localScale : Vector3.one;
            Vector2 startBubbleOffset = backgroundBubbles != null ? backgroundBubbles.rectTransform.anchoredPosition : Vector2.zero;
            float startBubbleRotation = backgroundBubbles != null ? backgroundBubbles.rectTransform.localEulerAngles.z : 0f;

            while (time < BackgroundFamilyTransitionSeconds)
            {
                time += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, time / BackgroundFamilyTransitionSeconds);
                ApplyBackgroundVisuals(
                    Color.Lerp(startBase, baseTint, t),
                    Color.Lerp(startDetail, detailTint, t),
                    Color.Lerp(startFlow, flowTint, t),
                    Color.Lerp(startShape, shapeTint, t),
                    Color.Lerp(startMacro, macroTint, t),
                    Color.Lerp(startBubble, bubbleTint, t),
                    Mathf.Lerp(startVignette, vignetteAlpha, t),
                    Mathf.Lerp(startDetailScale.x, detailScale, t),
                    Vector2.Lerp(startDetailOffset, detailOffset, t),
                    Mathf.Lerp(startFlowScale.x, flowScale, t),
                    Vector2.Lerp(startFlowOffset, flowOffset, t),
                    Mathf.Lerp(startFlowRotation, flowRotation, t),
                    Mathf.Lerp(startShapeScale.x, shapeScale, t),
                    Vector2.Lerp(startShapeOffset, shapeOffset, t),
                    Mathf.Lerp(startShapeRotation, shapeRotation, t),
                    Mathf.Lerp(startMacroScale.x, macroScale, t),
                    Vector2.Lerp(startMacroOffset, macroOffset, t),
                    Mathf.Lerp(startMacroRotation, macroRotation, t),
                    Mathf.Lerp(startBubbleScale.x, bubbleScale, t),
                    Vector2.Lerp(startBubbleOffset, bubbleOffset, t),
                    Mathf.Lerp(startBubbleRotation, bubbleRotation, t),
                    familyIndex,
                    family);
                yield return null;
            }

            ApplyBackgroundVisuals(baseTint, detailTint, flowTint, shapeTint, macroTint, bubbleTint, vignetteAlpha, detailScale, detailOffset, flowScale, flowOffset, flowRotation, shapeScale, shapeOffset, shapeRotation, macroScale, macroOffset, macroRotation, bubbleScale, bubbleOffset, bubbleRotation, familyIndex, family);
            _backgroundTransition = null;
        }

        private static Sprite GetFamilyGradientSprite(int familyIndex, BackgroundFamilyProfile family)
        {
            var key = new GradientCacheKey(familyIndex, family.GradientTop, family.GradientBottom);
            if (BackgroundFamilyGradients.TryGetValue(key, out var sprite) && sprite != null)
            {
                return sprite;
            }

            var created = CreateGradientSprite(familyIndex, family.GradientTop, family.GradientBottom);
            BackgroundFamilyGradients[key] = created;
            return created;
        }

        private static Sprite CreateGradientSprite(int familyIndex, Color top, Color bottom)
        {
            const int width = 128;
            const int height = 256;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            var rng = new DeterministicRng(unchecked((ulong)(familyIndex * 1000003 + 0x9E3779B9)));
            float warpOffsetX = rng.NextFloat() * 10f;
            float warpOffsetY = rng.NextFloat() * 10f;

            for (int y = 0; y < height; y++)
            {
                float ny = y / (float)(height - 1);
                for (int x = 0; x < width; x++)
                {
                    float nx = x / (float)(width - 1);
                    float warp = rng.FBm(nx * 2.2f + warpOffsetX, ny * 2.0f + warpOffsetY, 3, 2f, 0.5f);
                    float warp2 = rng.FBm(nx * 4.1f + warpOffsetX + 3.3f, ny * 4.1f + warpOffsetY + 1.7f, 2, 2f, 0.55f);
                    float warpedNy = Mathf.Clamp01(ny + (warp - 0.5f) * 0.26f + (warp2 - 0.5f) * 0.12f);
                    float curve = Mathf.SmoothStep(0f, 1f, warpedNy);
                    var color = Color.Lerp(bottom, top, curve);
                    float band = Mathf.Clamp01(0.5f + (warp - 0.5f) * 2.2f + (warp2 - 0.5f) * 1.6f);
                    float brightness = Mathf.Lerp(0.35f, 2.4f, band);
                    float alphaBoost = Mathf.Lerp(0.3f, 2.0f, band);
                    color = new Color(
                        Mathf.Clamp01(color.r * brightness),
                        Mathf.Clamp01(color.g * brightness),
                        Mathf.Clamp01(color.b * brightness),
                        Mathf.Clamp01(color.a * alphaBoost));
                    texture.SetPixel(x, y, color);
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 96f);
        }

        private int ResolveBackgroundPaletteIndex(int levelIndex, int seed)
        {
            if (levelIndex <= 3)
            {
                var earlyVariant = BackgroundRules.GetLevelVariant(levelIndex, seed ^ 0x71F04C3A, BackgroundPalettes.Length);
                return earlyVariant.PaletteIndex;
            }
            var variant = BackgroundRules.GetLevelVariant(levelIndex, seed, BackgroundPalettes.Length);
            return variant.PaletteIndex;
        }

        private LevelState EnsureBackground(LevelState state, int levelIndex, int seed)
        {
            if (state == null) return null;
            if (state.BackgroundPaletteIndex >= 0) return state;
            int paletteIndex = ResolveBackgroundPaletteIndex(levelIndex, seed);
            return new LevelState(state.Bottles, state.MovesUsed, state.MovesAllowed, state.OptimalMoves, state.LevelIndex, state.Seed, state.ScrambleMoves, paletteIndex);
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

        private void EmitAutoSolveDiagnostic(string eventName, string context)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (!EnableAutoSolveDiagnostics) return;

            string suffix = string.IsNullOrWhiteSpace(context) ? string.Empty : $" {context}";
            string line = $"Decantra AutoSolve {eventName}{suffix}";
            Debug.Log(line);
            AppendDebugLog(line);
#endif
        }

        private static bool UseWebGlMainThreadPrecompute()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return true;
#else
            return Application.platform == RuntimePlatform.WebGLPlayer;
#endif
        }

        private IEnumerator PrecomputeNextLevelOnMainThread(int level, int seed)
        {
            yield return null;
            var generated = GenerateLevelWithRetry(level, seed, 6);
            if (_nextLevel != level || _nextSeed != seed) yield break;
            _nextState = generated.State;
            _nextDifficulty100 = generated.Difficulty100;
            _webGlPrecomputeRoutine = null;
            EmitPrecomputeDiagnostic("Complete", $"mode=webgl-mainthread nextLevel={level} hasState={generated.State != null}");
        }

        private void TryApplyCompletedPrecompute(string context)
        {
            if (_nextState != null) return;
            if (_precomputeTask == null) return;
            if (!_precomputeTask.IsCompleted) return;

            if (_precomputeTask.IsCompletedSuccessfully)
            {
                var precomputed = _precomputeTask.Result;
                _nextState = precomputed.State;
                _nextDifficulty100 = precomputed.Difficulty100;
                EmitPrecomputeDiagnostic("Complete", $"mode=task context={context} nextLevel={_nextLevel} hasState={precomputed.State != null}");
                return;
            }

            if (_precomputeTask.IsCanceled)
            {
                EmitPrecomputeDiagnostic("Canceled", $"context={context} nextLevel={_nextLevel}");
                return;
            }

            if (_precomputeTask.IsFaulted)
            {
                EmitPrecomputeDiagnostic("Faulted", $"context={context} nextLevel={_nextLevel} exception={_precomputeTask.Exception?.GetBaseException().Message}");
            }
        }

        private IEnumerator EmitNextLevelFirstFrame(int sessionId, int levelIndex)
        {
            yield return new WaitForEndOfFrame();
            if (sessionId != _levelSessionId) yield break;
            EmitPrecomputeDiagnostic("NextLevelFirstFrame", $"level={levelIndex} session={sessionId}");
        }

        private void TrackLevelLoadPrecomputeExpectations(int levelIndex)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            _levelLoadRealtime = Time.realtimeSinceStartup;
            _levelCompleteRealtime = -1f;
            int frameDelta = _precomputeStartFrame >= 0 ? Time.frameCount - _precomputeStartFrame : -1;
            bool precomputeStartedWithinBudget = frameDelta >= 0 && frameDelta <= PrecomputeStartFrameBudget;
            EmitPrecomputeDiagnostic("LevelStart", $"level={levelIndex} nextLevel={_nextLevel} precomputeStartedWithinFrames={precomputeStartedWithinBudget} frameDelta={frameDelta} frameBudget={PrecomputeStartFrameBudget}");
            if (!precomputeStartedWithinBudget)
            {
                Debug.LogWarning($"Decantra Precompute assert failed: precompute did not start for level={levelIndex}");
            }
#endif
        }

        private void EmitCompletionToReadyMetric(int nextLevel, string path)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (_levelCompleteRealtime < 0f) return;
            float elapsed = Time.realtimeSinceStartup - _levelCompleteRealtime;
            EmitPrecomputeDiagnostic("LevelCompleteToTransitionReady", $"nextLevel={nextLevel} path={path} elapsedMs={(elapsed * 1000f):0.0}");
#endif
        }

        private void EmitPrecomputeDiagnostic(string eventName, string context)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (!EnablePrecomputeDiagnostics) return;
            string suffix = string.IsNullOrWhiteSpace(context) ? string.Empty : $" {context}";
            int threadId = Thread.CurrentThread.ManagedThreadId;
            string line = $"Decantra Precompute {eventName} ts={DateTime.UtcNow:O} platform={Application.platform} thread={threadId}{suffix}";
            Debug.Log(line);
            AppendDebugLog(line);
#endif
        }

        private void CancelPrecompute()
        {
            if (_precomputeCts != null)
            {
                _precomputeCts.Cancel();
                _precomputeCts.Dispose();
                _precomputeCts = null;
            }
            if (_webGlPrecomputeRoutine != null)
            {
                StopCoroutine(_webGlPrecomputeRoutine);
                _webGlPrecomputeRoutine = null;
            }
            _precomputeTask = null;
            _nextState = null;
            _nextDifficulty100 = 0;
        }
    }
}
