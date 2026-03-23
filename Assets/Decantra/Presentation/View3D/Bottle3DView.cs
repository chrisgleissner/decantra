/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System.Collections.Generic;
using System.IO;
using Decantra.Domain.Model;
using Decantra.Presentation.Visual.Simulation;
using UnityEngine;

namespace Decantra.Presentation.View3D
{
    /// <summary>
    /// Drives a 3D mesh bottle visual from game state with deterministic liquid simulation.
    ///
    /// Relationship to legacy BottleView
    /// ------------------------------------
    /// This component is intended to sit alongside (or replace) the existing Canvas-UI
    /// BottleView.  During the transition phase it acts as a visual overlay:
    ///   - It reads the same <see cref="Bottle"/> data model.
    ///   - It does NOT own or modify any gameplay logic.
    ///   - It creates child 3D GameObjects (bottleMeshGO, liquidLayerGOs[]).
    ///   - Bottle world-position is inherited from the owning RectTransform pivot
    ///     by sampling its world position each frame and mirroring it to a
    ///     scene-root _worldRoot that is NOT parented under the Canvas.
    ///
    /// Why _worldRoot must be scene-root (not a Canvas child)
    /// -------------------------------------------------------
    /// Canvas elements rendered in ScreenSpaceCamera mode have a world lossyScale of
    /// approximately 0.005 (canvas pixels → world units for a 1920-tall reference
    /// at orthographic size 5).  If mesh GameObjects are parented here they inherit
    /// that scale and appear ~200x too small.  Additionally, Camera_Game culls only
    /// the "Game" layer; default-layer children are invisible.
    /// _worldRoot solves both: it lives at scene root (scale = 1, 1, 1) and carries
    /// the correct layer, so meshes render at their designed world-unit sizes while
    /// being visible to Camera_Game.
    ///
    /// 3D object hierarchy
    /// ---------------------
    ///   _worldRoot  (scene-root GO, updated to match canvas bottle world XY each frame)
    ///   ├── GlassBody          MeshRenderer  BottleGlass.shader
    ///   ├── LiquidLayers       (empty parent, localZ = -0.008)
    ///   │   ├── LiquidLayer_0  MeshRenderer  Liquid3D.shader  (MaterialPropertyBlock)
    ///   │   ├── LiquidLayer_1  ...
    ///   │   └── ...
    ///   └── ContactShadow      MeshRenderer  Unlit/Color
    ///
    /// Coordinate system
    ///   _worldRoot Y tracks the canvas element's world Y (centre of 420px bottle cell).
    ///   BottleMeshGenerator values are in world units; at orthoSize=5 (10-unit view)
    ///   a 420px / 1920px cell = 2.19 world units ≈ mesh body+shoulder+neck height.
    ///
    /// Determinism guarantee
    ///   WobbleSolver uses fixed-step integration — no frame-rate dependency.
    ///   All shader parameters are set from exact integer slot counts (FillHeightMapper).
    ///   Surface tilt angle comes solely from the bottle transform's Z rotation axis.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Bottle3DView : MonoBehaviour
    {
        // ── Static layout diagnostics registry ────────────────────────────────
        // All currently-active Bottle3DView instances register here so the layout
        // safety check can examine all bottles in a single pass without FindObjectsOfType.
        private static readonly System.Collections.Generic.List<Bottle3DView> s_activeViews =
            new System.Collections.Generic.List<Bottle3DView>(9);

        // ── Inspector fields ──────────────────────────────────────────────────
        [Tooltip("Material templated from BottleGlass.shader for the glass body.")]
        [SerializeField] private Material glassMaterialTemplate;

        [Tooltip("Material templated from Liquid3D.shader for each liquid layer.")]
        [SerializeField] private Material liquidMaterialTemplate;

        [Tooltip("Material templated from BottleOutline.shader for the bottle silhouette shell.")]
        [SerializeField] private Material outlineMaterialTemplate;

        // ── Internal state ────────────────────────────────────────────────────
        private readonly WobbleSolver _wobble = new WobbleSolver();
        private readonly List<LiquidLayerData> _layers = new List<LiquidLayerData>(9);
        private readonly List<MeshRenderer> _layerRenderers = new List<MeshRenderer>(9);
        private readonly List<MaterialPropertyBlock> _layerBlocks = new List<MaterialPropertyBlock>(9);
        // Cache fill bounds per layer to avoid mesh rebuild when nothing changed
        private readonly List<(float min, float max)> _cachedFillBounds = new List<(float, float)>(9);

        /// <summary>
        /// Scene-root GameObject that holds all 3D mesh children.
        /// NOT parented under the Canvas — see class doc for rationale.
        /// Its world XY is synced to this transform's world XY every frame.
        /// </summary>
        private GameObject _worldRoot;

        private GameObject _glassBodyGO;
        private GameObject _outlineGO;
        private GameObject _liquidRoot;
        private GameObject _contactShadowGO;
        private GameObject _neckOverlayGO;
        private GameObject _topBoundaryCollarGO;
        private GameObject _bottomBoundaryCollarGO;
        private MeshRenderer _outlineRenderer;
        private MeshRenderer _neckOverlayRenderer;
        private MeshRenderer _topBoundaryCollarRenderer;
        private MeshRenderer _bottomBoundaryCollarRenderer;

        private bool _isHighlighted;
        private bool _isEmptyBottle;
        private static readonly Color RimSheenDefaultColor = new Color(0.90f, 0.95f, 1.00f, 1f);
        private static readonly Color RimSheenSinkColor = new Color(0.84f, 0.88f, 0.94f, 1f);
        private static readonly Color RimSheenHighlightColor = new Color(0.96f, 0.98f, 1f, 1f);
        private const float RimSheenDefaultIntensity = 0.10f;
        private const float RimSheenSinkIntensity = 0.08f;
        private const float RimSheenHighlightIntensity = 0.26f;
        private const float RimSheenDefaultPower = 4.5f;
        private const float RimSheenSinkPower = 4.7f;
        private const float RimSheenHighlightPower = 3.8f;
        private static readonly Color OutlineSinkColor = new Color(0f, 0f, 0f, 0.94f);
        private static readonly Color OutlineHighlightColor = new Color(0.94f, 0.98f, 1f, 0.38f);
        private static readonly Color RegularNeckOverlayColor = new Color(0.92f, 0.96f, 1.00f, 0.18f);
        private static readonly Color SinkNeckOverlayColor = new Color(0.04f, 0.04f, 0.06f, 0.84f);
        private static readonly Color BoundaryCollarColor = new Color(0.14f, 0.15f, 0.17f, 0.26f);
        private const float OutlineSinkWidth = 0.043f;
        private const float OutlineHighlightWidth = 0.05f;
        private const float NeckOverlayOutset = 0.0025f;

        private Bottle _lastBottle;
        private int _levelMaxCapacity = 4;
        private float _capacityRatio = 1f;
        private float _previousZRotation;
        private float _previousAngularVelocityRad;
        private float _currentSurfaceTiltDeg;
        private bool _hasPreviousRotationSample;
        private bool _initialised;
        private bool _isSinkOnly;

        // ── Pour animation interpolation ──────────────────────────────────────
        // Source drain: the top liquid layer's _TotalFill is animated from 1→fraction
        // each frame of AnimateMove so the level visibly drops during the pour.
        private bool _isDrainAnimating;
        private float _drainTopLayerFillMin;   // FillMin of top layer before pour
        private float _drainTopLayerFillMax;   // FillMax of top layer before pour
        private float _drainTargetFill;        // total fill fraction after pour
        private int _drainTopLayerIndex;     // index into _layerRenderers

        // Target receive: a temporary mesh GO is shown animating from 0→1 fill
        // to represent the incoming liquid arriving during the pour.
        private bool _isReceiveAnimating;
        private float _receiveFillFrom;
        private float _receiveFillTo;
        private Color _receiveColor;
        private GameObject _receiveLayerGO;
        private MeshRenderer _receiveLayerRenderer;
        private MaterialPropertyBlock _receiveLayerBlock;

        // ── Bottle stopper / cork ────────────────────────────────────────
        // A short cylinder that sits in the neck and peeks above the rim, like a
        // wine-bottle cork.  Always visible.  Coloured with the single liquid colour
        // when the bottle is monochrome-complete; neutral beige when empty or mixed.
        // Does NOT affect glass body MeshRenderer.bounds used by CheckLayoutSafety.
        private static readonly Color StopperNeutralColor = new Color(0.80f, 0.76f, 0.70f, 1f);
        private GameObject _stopperGO;
        private MeshRenderer _stopperRenderer;
        private bool _wasCompleted;   // last-known completion state (used by WriteReport)

        // 3D drag rotation — applied to _worldRoot; does not affect canvas layout/gameplay
        private float _targetDragYaw;
        private float _currentDragYaw;
        private float _targetDragRoll;
        private float _currentDragRoll;

        private const float AngularVelocityImpulseScale = 0.08f;
        private const float AngularAccelerationImpulseScale = 0.02f;

        /// <summary>
        /// Global uniform scale applied to the 3D world root so the tallest bottles remain
        /// fully below the HUD button row while preserving the established board framing.
        /// </summary>
        // VisualScale is kept for the SetLevelMaxCapacity diagnostic log.
        // The worldRoot localScale is set dynamically in SyncWorldRootPosition based on
        // the actual canvas cell world height so the bottle fits any runtime resolution.
        public const float VisualScale = 0.88f;

        // Fraction of the canvas cell height the bottle should occupy.
        // Raised slightly to reclaim the vertical space freed by the flatter base.
        public const float HeightFitFraction = 0.99f;

        // Additional horizontal enlargement so bottles read slightly wider without
        // changing the shader, interaction logic, or bottle internals.
        public const float WidthFitMultiplier = 1.065f;

        // The canvas-side interaction collider remains on the unscaled UI object, so
        // its local height still needs to preserve the pre-flat-base hit target.
        private const float LegacyColliderFitFraction = 0.90f;

        private static readonly float LegacyColliderHeight =
            BottleMeshGenerator.BodyHeight
            + BottleMeshGenerator.ShoulderHeight
            + BottleMeshGenerator.NeckHeight
            + BottleMeshGenerator.DomeRadius;

        // Full mesh height (cap ratio 1.0) in local world units before any worldRoot scale.
        private static readonly float MeshFullHeight = BottleMeshGenerator.ReferenceMeshHeight;

        // Glass body property IDs
        private static readonly int PropSinkOnly = Shader.PropertyToID("_SinkOnly");
        private static readonly int PropRimSheenColor = Shader.PropertyToID("_RimSheenColor");
        private static readonly int PropRimSheenIntensity = Shader.PropertyToID("_RimSheenIntensity");
        private static readonly int PropRimSheenPower = Shader.PropertyToID("_RimSheenPower");
        private static readonly int PropEmptyBottleBoost = Shader.PropertyToID("_EmptyBottleBoost");
        private static readonly int PropOutlineGlowColor = Shader.PropertyToID("_GlowColor");
        private static readonly int PropOutlineWidth = Shader.PropertyToID("_OutlineWidth");
        // Sprites/Default (stopper shader) uses _Color for the tint.
        private static readonly int PropStopperColor = Shader.PropertyToID("_Color");

        // Shader property ID cache (populated once)
        private static readonly int PropTotalFill = Shader.PropertyToID("_TotalFill");
        private static readonly int PropLayerCount = Shader.PropertyToID("_LayerCount");
        private static readonly int PropSurfaceTilt = Shader.PropertyToID("_SurfaceTiltDegrees");
        private static readonly int PropWobbleOffset = Shader.PropertyToID("_WobbleOffset");
        private static readonly int PropAgitation = Shader.PropertyToID("_Agitation");
        // Per-layer color/fill property IDs (indexed 0–8)
        private static readonly int[] PropLayerColor = new int[9];
        private static readonly int[] PropLayerMin = new int[9];
        private static readonly int[] PropLayerMax = new int[9];

        static Bottle3DView()
        {
            for (int i = 0; i < 9; i++)
            {
                PropLayerColor[i] = Shader.PropertyToID($"_Layer{i}Color");
                PropLayerMin[i] = Shader.PropertyToID($"_Layer{i}Min");
                PropLayerMax[i] = Shader.PropertyToID($"_Layer{i}Max");
            }
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Set the level's maximum capacity so fill heights are proportional.
        /// Call before the first <see cref="Render"/> call.
        /// </summary>
        public void SetLevelMaxCapacity(int maxCapacity)
        {
            _levelMaxCapacity = Mathf.Max(1, maxCapacity);
            // Block C diagnostic: log expected max bottle height for this capacity setting.
            float maxMeshHeight = BottleMeshGenerator.ReferenceMeshHeight * VisualScale;
            Debug.Log($"[Bottle3DView] SetLevelMaxCapacity={maxCapacity}  " +
                      $"maxBottleHeight≈{maxMeshHeight:F3}wu  " +
                      $"activeViews={s_activeViews.Count}");
        }

        /// <summary>
        /// Update all 3D visuals to match <paramref name="bottle"/> state.
        /// Must be called whenever the bottle's liquid content changes.
        /// </summary>
        public void Render(Bottle bottle, System.Func<int, (float r, float g, float b)> colorResolver = null)
        {
            if (bottle == null) return;
            _lastBottle = bottle;
            _isEmptyBottle = bottle.IsEmpty;

            EnsureInitialised();

            // Block E fix: sync sink-only visual state from bottle state.
            bool bottleIsSink = bottle.IsSink;
            if (_isSinkOnly != bottleIsSink)
            {
                _isSinkOnly = bottleIsSink;
                ApplySinkOnlyToGlass();
            }

            // Build layer data from current bottle state
            FillHeightMapper.Build(bottle, _layers, colorResolver);

            // Rebuild liquid layer GameObjects if slot count changed
            EnsureLayerObjects(_layers.Count);

            // Block B fix: drive per-bottle capacity ratio into the glass and liquid
            // shaders so only the cylindrical body scales; the hemispherical dome (bottom)
            // and the neck+rim (top) stay at their full original size.
            // Must run after EnsureLayerObjects so all layers (including newly created
            // ones) receive the ratio.
            {
                float ratio = _levelMaxCapacity > 0
                    ? Mathf.Clamp01((float)bottle.Capacity / _levelMaxCapacity)
                    : 1f;
                ApplyCapacityRatio(ratio);
            }

            // Apply per-layer shader properties
            ApplyLayerProperties(_layers, bottle);

            // Obj-3: show / hide the coloured topper cap on completed bottles.
            UpdateTopper(bottle, _layers);
        }

        /// <summary>
        /// Trigger a slosh impulse when the bottle is selected, poured from, or dropped.
        /// <paramref name="angularImpulse"/> in rad/s — positive = rightward slosh.
        /// </summary>
        public void ApplySloshImpulse(float angularImpulse)
        {
            _wobble.ApplyImpulse(angularImpulse);
        }

        /// <summary>
        /// Begin a pour animation from this bottle into <paramref name="target"/>.
        /// Does NOT change game state — purely visual.
        /// </summary>
        public void BeginPour(Bottle3DView target, float normalizedPourRate)
        {
            _wobble.ApplyImpulse(normalizedPourRate * 2f);
        }

        /// <summary>End the pour animation.</summary>
        public void EndPour()
        {
            _wobble.ApplyImpulse(-0.3f); // gentle counter-slosh when liquid settles
        }

        /// <summary>Reset wobble state (call on level reload).</summary>
        public void ResetWobble()
        {
            _wobble.Reset();
        }

        /// <summary>
        /// Returns the current combined surface tilt in degrees (base tilt + wobble).
        /// Used by GameController to sample tilt for the pour report.
        /// </summary>
        public float CurrentSurfaceTiltDegrees =>
            Mathf.Clamp(
                _currentSurfaceTiltDeg + _wobble.TiltAngleDegrees,
                -WobbleSolver.MaxTiltDegrees,
                WobbleSolver.MaxTiltDegrees);

        // ── Pour interpolation API ────────────────────────────────────────────
        // Called by GameController.AnimateMove each frame to keep 3D liquid levels
        // in sync with the 2D canvas animation rather than jumping at the end.

        /// <summary>
        /// Prepare the source-bottle drain animation.
        /// Must be called BEFORE the AnimateMove loop starts, while _layers still
        /// reflects the pre-pour bottle state.
        /// <paramref name="totalFillTo"/> is the fill fraction after the pour completes
        /// (i.e. (bottle.Count - poured) / bottle.Capacity).
        /// </summary>
        public void BeginSourceDrain(float totalFillTo)
        {
            if (_layers.Count == 0 || _layerRenderers.Count == 0) return;
            int topIdx = _layers.Count - 1;
            var topLayer = _layers[topIdx];
            _isDrainAnimating = true;
            _drainTopLayerFillMin = topLayer.FillMin;
            _drainTopLayerFillMax = topLayer.FillMax;
            _drainTargetFill = Mathf.Clamp01(totalFillTo);
            _drainTopLayerIndex = topIdx;
        }

        /// <summary>
        /// Prepare the target-bottle receive animation.
        /// Creates a temporary liquid-layer mesh spanning [<paramref name="fillFrom"/>,
        /// <paramref name="fillTo"/>] that fades in as t goes 0→1.
        /// </summary>
        public void BeginTargetReceive(float fillFrom, float fillTo, float r, float g, float b)
        {
            _isReceiveAnimating = true;
            _receiveFillFrom = Mathf.Clamp01(fillFrom);
            _receiveFillTo = Mathf.Clamp01(fillTo);
            _receiveColor = LiquidColorTuning.ApplyGameplayVibrancy(new Color(r, g, b, 1f));
            EnsureReceiveLayerGO();
        }

        /// <summary>
        /// Drive both drain and receive animations for normalised pour progress
        /// <paramref name="t"/> ∈ [0..1].  Call once per frame inside AnimateMove.
        /// </summary>
        public void SetPourT(float t)
        {
            if (_isDrainAnimating)
            {
                UpdateDrainAtT(t);

                if (_drainTopLayerIndex < _layerBlocks.Count
                    && _drainTopLayerIndex < _layerRenderers.Count
                    && _layerRenderers[_drainTopLayerIndex] != null)
                {
                    float pourPeak = Mathf.Sin(t * Mathf.PI); // bell: 0 → 1 → 0
                    var block = _layerBlocks[_drainTopLayerIndex];
                    block.SetFloat(PropAgitation, Mathf.Max(0.35f, pourPeak));
                    _layerRenderers[_drainTopLayerIndex].SetPropertyBlock(block);
                }
            }

            if (_isReceiveAnimating)
            {
                // Small phase offset: the receiving liquid visibly arrives a beat
                // after the source pour begins, simulating liquid travel time.
                float receiveT = Mathf.Clamp01((t - 0.05f) / 0.95f);
                if (_receiveLayerRenderer != null && _receiveLayerBlock != null)
                {
                    SetReceiveLayerTotalFill(Mathf.Lerp(_receiveFillFrom, _receiveFillTo, receiveT));
                    // Agitation starts high (arriving liquid splashes) and settles.
                    _receiveLayerBlock.SetFloat(PropAgitation, Mathf.Max(0.4f, 1f - receiveT));
                    _receiveLayerRenderer.SetPropertyBlock(_receiveLayerBlock);
                }
            }
        }

        /// <summary>
        /// Tear down pour animation state.  Call after the AnimateMove loop ends,
        /// BEFORE TryApplyMoveAndScore + Render() so the 3D views are clean for
        /// the authoritative state update.
        /// </summary>
        public void ClearPourAnimation()
        {
            // Restore the drain layer to full visibility (Render() will re-set it
            // correctly from the new game state, but be explicit for safety).
            if (_isDrainAnimating
                && _drainTopLayerIndex < _layerBlocks.Count
                && _drainTopLayerIndex < _layerRenderers.Count
                && _layerRenderers[_drainTopLayerIndex] != null)
            {
                var block = _layerBlocks[_drainTopLayerIndex];
                block.SetFloat(PropTotalFill, _drainTopLayerFillMax);
                _layerRenderers[_drainTopLayerIndex].SetPropertyBlock(block);
            }
            _isDrainAnimating = false;

            // Destroy the temporary receive layer GO.
            if (_receiveLayerGO != null)
            {
                var mf = _receiveLayerGO.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                    Destroy(mf.sharedMesh);
                if (liquidMaterialTemplate == null
                    && _receiveLayerRenderer != null
                    && _receiveLayerRenderer.sharedMaterial != null)
                {
                    Destroy(_receiveLayerRenderer.sharedMaterial);
                }
                Destroy(_receiveLayerGO);
                _receiveLayerGO = null;
                _receiveLayerRenderer = null;
                _receiveLayerBlock = null;
            }
            _isReceiveAnimating = false;
        }
        /// <summary>
        /// Mark this bottle as a sink-only bottle so the glass shader renders dark rim
        /// and base-line bands.  Call before or during <see cref="Render"/>.
        /// </summary>
        public void SetSinkOnly(bool isSink)
        {
            if (_isSinkOnly == isSink) return;
            _isSinkOnly = isSink;
            ApplySinkOnlyToGlass();
        }

        /// <summary>
        /// Highlight this bottle as the active pour target.
        /// The highlight is rendered as a stronger white rim response on the glass shader,
        /// so it follows the true curved silhouette rather than drawing a separate flat shell.
        /// </summary>
        public void SetHighlight(bool highlighted)
        {
            if (_isHighlighted == highlighted) return;
            _isHighlighted = highlighted;
            ApplySinkOnlyToGlass();
        }
        /// <summary>
        /// Apply a 3D yaw (Y-axis) and optional roll to the world-root mesh during drag.
        /// Provides visible parallax / specular-shift cue without affecting canvas layout.
        /// Smoothly lerps back to zero when <see cref="ClearDragRotation"/> is called.
        /// </summary>
        public void SetDragRotation(float yawDeg, float rollDeg = 0f)
        {
            _targetDragYaw = Mathf.Clamp(yawDeg, -15f, 15f);
            _targetDragRoll = Mathf.Clamp(rollDeg, -10f, 10f);
        }

        /// <summary>Clear drag rotation; world root smoothly returns to neutral.</summary>
        public void ClearDragRotation()
        {
            _targetDragYaw = 0f;
            _targetDragRoll = 0f;
        }

        /// <summary>
        /// World-space Transform of the 3D mesh root (scene root, correct scale).
        /// Use this instead of <c>transform</c> when supplying positions to 3D systems
        /// (pour stream target, Physics raycasting, etc.).
        /// </summary>
        public Transform WorldRootTransform => _worldRoot != null ? _worldRoot.transform : transform;

        // ── Unity lifecycle ───────────────────────────────────────────────────

        private void Awake()
        {
            EnsureInitialised();
        }

        // ── Block D fix: propagate canvas GO active-state to scene-root _worldRoot ──
        // When GameController.Render() calls bottleViews[i].gameObject.SetActive(false)
        // (for bottles beyond the level's count), this MonoBehaviour's Update() stops
        // but _worldRoot — a scene-root GO — remains active at its last position.
        // This caused 9 visible 3D bottle GOs on a 5-bottle level (Level 10 regression).
        private void OnEnable()
        {
            if (_worldRoot != null)
                _worldRoot.SetActive(true);
            if (!s_activeViews.Contains(this))
                s_activeViews.Add(this);
        }

        private void OnDisable()
        {
            CancelInvoke(nameof(CheckLayoutSafety));
            if (_worldRoot != null)
                _worldRoot.SetActive(false);
            s_activeViews.Remove(this);
        }

        private void Start()
        {
            // Schedule layout safety check after all bottles have initialised this frame.
            Invoke(nameof(CheckLayoutSafety), 0.5f);
        }

        private void Update()
        {
            // Keep the world-space root aligned to this canvas element every frame.
            // This must run before wobble/tilt so that any LateUpdate reading
            // _worldRoot.transform is already at the correct position.
            SyncWorldRootPosition();

            float dt = Time.deltaTime;

            // Smooth drag rotation toward target and apply to world root.
            // Rate = 10 s⁻¹ → 90% settled in ~0.23 s, feels snappy but not jerky.
            float lerpRate = dt * 10f;
            _currentDragYaw = Mathf.Lerp(_currentDragYaw, _targetDragYaw, lerpRate);
            _currentDragRoll = Mathf.Lerp(_currentDragRoll, _targetDragRoll, lerpRate);
            if (_worldRoot != null)
            {
                // Normalize canvas Z to signed angle so Euler doesn't wrap unexpectedly.
                float canvasZ = Mathf.DeltaAngle(0f, transform.eulerAngles.z);
                _worldRoot.transform.rotation = Quaternion.Euler(
                    -_currentDragRoll * 0.4f,  // slight pitch for roll realism
                    _currentDragYaw,            // horizontal-drag yaw
                    canvasZ);                   // preserve pour-tilt from canvas rotation
            }

            // Update base tilt and derive deterministic slosh impulses from rotation dynamics.
            UpdateTiltFromRotation(transform.eulerAngles.z, dt);

            // Advance the wobble simulation by this frame's delta time
            _wobble.Step(dt);

            // Push wobble state to all liquid layer renderers
            if (_layerRenderers.Count > 0)
            {
                float tilt = Mathf.Clamp(
                    _currentSurfaceTiltDeg + _wobble.TiltAngleDegrees,
                    -WobbleSolver.MaxTiltDegrees,
                    WobbleSolver.MaxTiltDegrees);
                float normalizedWobble = _wobble.Displacement / Mathf.Max(0.0001f, WobbleSolver.MaxDisplacement);
                float wobble = normalizedWobble * 0.015f;
                float agitation = Mathf.Clamp01(Mathf.Abs(normalizedWobble));

                for (int i = 0; i < _layerRenderers.Count; i++)
                {
                    if (_layerRenderers[i] == null) continue;
                    var block = _layerBlocks[i];
                    block.SetFloat(PropSurfaceTilt, tilt);
                    block.SetFloat(PropWobbleOffset, wobble);
                    block.SetFloat(PropAgitation, agitation);
                    _layerRenderers[i].SetPropertyBlock(block);
                }
            }
        }

        private void OnDestroy()
        {
            CancelInvoke(nameof(CheckLayoutSafety));
            s_activeViews.Remove(this);
            ClearPourAnimation();
            CleanupLayerObjects();

            if (_glassBodyGO != null)
            {
                var glassFilter = _glassBodyGO.GetComponent<MeshFilter>();
                if (glassFilter != null && glassFilter.sharedMesh != null)
                    Destroy(glassFilter.sharedMesh);

                if (glassMaterialTemplate == null)
                {
                    var glassRenderer = _glassBodyGO.GetComponent<MeshRenderer>();
                    if (glassRenderer != null && glassRenderer.sharedMaterial != null)
                        Destroy(glassRenderer.sharedMaterial);
                }
            }

            if (_contactShadowGO != null)
            {
                var mf = _contactShadowGO.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                    Destroy(mf.sharedMesh);

                var mr = _contactShadowGO.GetComponent<MeshRenderer>();
                if (mr != null && mr.sharedMaterial != null)
                    Destroy(mr.sharedMaterial);
            }

            // Stopper cleanup: mesh and material are unique instances; destroy explicitly.
            if (_stopperGO != null)
            {
                var mf = _stopperGO.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                    Destroy(mf.sharedMesh);
                // _stopperGO is a child of _worldRoot so the GO is destroyed below;
                // material instance needs explicit disposal.
                if (_stopperRenderer != null && _stopperRenderer.sharedMaterial != null)
                    Destroy(_stopperRenderer.sharedMaterial);
            }

            if (_neckOverlayGO != null)
            {
                var mf = _neckOverlayGO.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                    Destroy(mf.sharedMesh);
                if (_neckOverlayRenderer != null && _neckOverlayRenderer.sharedMaterial != null)
                    Destroy(_neckOverlayRenderer.sharedMaterial);
            }

            if (_topBoundaryCollarGO != null)
            {
                var mf = _topBoundaryCollarGO.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                    Destroy(mf.sharedMesh);
                if (_topBoundaryCollarRenderer != null && _topBoundaryCollarRenderer.sharedMaterial != null)
                    Destroy(_topBoundaryCollarRenderer.sharedMaterial);
            }

            if (_bottomBoundaryCollarGO != null)
            {
                var mf = _bottomBoundaryCollarGO.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                    Destroy(mf.sharedMesh);
                if (_bottomBoundaryCollarRenderer != null && _bottomBoundaryCollarRenderer.sharedMaterial != null)
                    Destroy(_bottomBoundaryCollarRenderer.sharedMaterial);
            }

            if (_outlineGO != null)
            {
                var mf = _outlineGO.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                    Destroy(mf.sharedMesh);
            }

            // Destroy the scene-root world object; this also destroys GlassBody,
            // BottleOutline, LiquidLayers, ContactShadow, and Stopper children.
            if (_worldRoot != null)
            {
                if (outlineMaterialTemplate == null
                    && _outlineRenderer != null
                    && _outlineRenderer.sharedMaterial != null)
                {
                    Destroy(_outlineRenderer.sharedMaterial);
                }

                Destroy(_worldRoot);
                _worldRoot = null;
            }
        }

        /// <summary>
        /// Block C: Layout safety diagnostic.  Runs 0.5 s after Start so all bottles in
        /// the level are fully initialised before the check.  Logs expected vs spawned
        /// bottle count, maximum bottle height, vertical spacing, and reports any
        /// bottle-to-bottle or bottle-to-HUD bound overlaps as errors.
        /// </summary>
        private void CheckLayoutSafety()
        {
            var boardViews = CollectViewsForLayoutRoot(GetLayoutGroupRoot());
            if (boardViews.Count == 0 || boardViews[0] != this)
                return;

            int count = boardViews.Count;
            if (count == 0) return;

            // Collect world-space bounds from each active bottle's glass mesh renderer.
            var bounds = new Bounds[count];
            bool allValid = true;
            for (int i = 0; i < count; i++)
            {
                var v = boardViews[i];
                if (v == null || v._glassBodyGO == null) { allValid = false; continue; }
                var mr = v._glassBodyGO.GetComponent<MeshRenderer>();
                if (mr == null) { allValid = false; continue; }
                bounds[i] = mr.bounds;
            }

            if (!allValid) return;

            // Compute diagnostic metrics.
            float maxHeight = 0f;
            float minY = float.MaxValue, maxY = float.MinValue;
            for (int i = 0; i < count; i++)
            {
                float h = bounds[i].size.y;
                if (h > maxHeight) maxHeight = h;
                if (bounds[i].min.y < minY) minY = bounds[i].min.y;
                if (bounds[i].max.y > maxY) maxY = bounds[i].max.y;
            }

            // Estimate vertical spacing from distinct Y centre positions.
            var yCentres = new System.Collections.Generic.List<float>(count);
            for (int i = 0; i < count; i++)
                yCentres.Add(bounds[i].center.y);
            yCentres.Sort();
            float verticalSpacing = float.NaN;
            if (yCentres.Count >= 2)
                verticalSpacing = yCentres[yCentres.Count / 2] - yCentres[yCentres.Count / 2 - 1];

            Debug.Log($"[Bottle3DView] LayoutDiag spawnedBottleVisualCount={count}  " +
                      $"maximumBottleHeight={maxHeight:F3}wu  " +
                      $"verticalSpacing≈{verticalSpacing:F3}wu  " +
                      $"worldYRange=[{minY:F3},{maxY:F3}]");

            // HUD safety check: Camera_Game orthographic size = 5 → top world Y = +5.
            // Assume HUD occupies top ≈13% of screen height (~0.65 wu safe margin).
            const float HudBoundaryY = 4.35f;
            bool hudIntrusionDetected = false;
            for (int i = 0; i < count; i++)
            {
                if (bounds[i].max.y > HudBoundaryY)
                {
                    hudIntrusionDetected = true;
                    Debug.LogError($"[Bottle3DView] LAYOUT VIOLATION: bottle index {i} " +
                                   $"top={bounds[i].max.y:F3} > HUD boundary {HudBoundaryY:F3}wu");
                }
            }

            // Bottle-to-bottle overlap check (axis-aligned bounds in world space).
            bool overlapDetected = false;
            for (int i = 0; i < count; i++)
            {
                for (int j = i + 1; j < count; j++)
                {
                    if (bounds[i].Intersects(bounds[j]))
                    {
                        overlapDetected = true;
                        Debug.LogError($"[Bottle3DView] LAYOUT VIOLATION: bottles {i} and {j} " +
                                       $"bounds overlap  " +
                                       $"({bounds[i].center} ↔ {bounds[j].center})");
                    }
                }
            }

            // Shadow-to-bottle overlap check.
            // Use full shadow renderer bounds intersection against all OTHER bottle bounds
            // (stronger than prior center-point heuristic and aligned with spec).
            bool shadowOverlapDetected = false;
            float shadowLengthRatioMax = 0f;
            for (int i = 0; i < count; i++)
            {
                var vi = boardViews[i];
                if (vi == null || vi._contactShadowGO == null) continue;
                var shadowMr = vi._contactShadowGO.GetComponent<MeshRenderer>();
                if (shadowMr == null) continue;
                var shadowBounds = shadowMr.bounds;

                // Constraint metric: shadow length <= 0.35 * bottle height.
                // Use max(X,Z) world span as effective shadow length.
                float bottleHeight = bounds[i].size.y;
                if (bottleHeight > 1e-5f)
                {
                    float shadowLength = Mathf.Max(shadowBounds.size.x, shadowBounds.size.z);
                    float ratio = shadowLength / bottleHeight;
                    if (ratio > shadowLengthRatioMax) shadowLengthRatioMax = ratio;
                }

                for (int j = 0; j < count; j++)
                {
                    if (i == j) continue;
                    if (shadowBounds.Intersects(bounds[j]))
                    {
                        shadowOverlapDetected = true;
                        Debug.LogError($"[Bottle3DView] SHADOW VIOLATION: shadow of bottle {i} " +
                                       $"bounds {shadowBounds} intersects bottle {j} bounds {bounds[j]}");
                    }
                }
            }

            // Compute per-level sink and topper counts from current active views.
            int sinkBottleCount = 0;
            int topperCount = 0;
            float corkAspectRatioMin = float.MaxValue;
            float corkAspectRatioMax = 0f;
            float corkCenterOffsetRatioMax = 0f;
            float corkInsertionDepthRatioMin = float.MaxValue;
            float corkInsertionDepthRatioMax = 0f;
            for (int i = 0; i < count; i++)
            {
                var v = boardViews[i];
                if (v != null)
                {
                    if (v._isSinkOnly) sinkBottleCount++;
                    if (v._wasCompleted)
                    {
                        topperCount++;
                        float aspect = BottleMeshGenerator.StopperTotalHeight / Mathf.Max(BottleMeshGenerator.StopperRadius, 1e-5f);
                        if (aspect < corkAspectRatioMin) corkAspectRatioMin = aspect;
                        if (aspect > corkAspectRatioMax) corkAspectRatioMax = aspect;

                        if (v._stopperGO != null)
                        {
                            float rimTopY = BottleMeshGenerator.GetRimTopY(v._capacityRatio);
                            float insertionDepth = rimTopY - v._stopperGO.transform.localPosition.y;
                            float insertionDepthRatio = insertionDepth / Mathf.Max(BottleMeshGenerator.StopperTotalHeight, 1e-5f);
                            if (insertionDepthRatio < corkInsertionDepthRatioMin) corkInsertionDepthRatioMin = insertionDepthRatio;
                            if (insertionDepthRatio > corkInsertionDepthRatioMax) corkInsertionDepthRatioMax = insertionDepthRatio;

                            float centerOffset = Mathf.Sqrt(
                                v._stopperGO.transform.localPosition.x * v._stopperGO.transform.localPosition.x +
                                v._stopperGO.transform.localPosition.z * v._stopperGO.transform.localPosition.z);
                            float centerOffsetRatio = centerOffset / Mathf.Max(BottleMeshGenerator.NeckRadius, 1e-5f);
                            if (centerOffsetRatio > corkCenterOffsetRatioMax) corkCenterOffsetRatioMax = centerOffsetRatio;
                        }
                    }
                }
            }

            if (corkAspectRatioMin == float.MaxValue)
                corkAspectRatioMin = 0f;
            if (corkInsertionDepthRatioMin == float.MaxValue)
                corkInsertionDepthRatioMin = 0f;

            // Write v2 layout report to device persistent data path.
            // This file is pulled by capture_screenshots.sh for verification.
            WriteLayoutReport(
                count,
                overlapDetected,
                shadowOverlapDetected,
                hudIntrusionDetected,
                topperCount,
                sinkBottleCount,
                corkAspectRatioMin,
                corkAspectRatioMax,
                corkCenterOffsetRatioMax,
                corkInsertionDepthRatioMin,
                corkInsertionDepthRatioMax,
                shadowLengthRatioMax);
        }

        /// <summary>
        /// Write the v2 layout report JSON to the device persistent data path so it can
        /// be pulled by the screenshot capture script.
        /// </summary>
        private static void WriteLayoutReport(
            int activeBottleCount,
            bool overlapDetected,
            bool shadowOverlapDetected,
            bool hudIntrusionDetected,
            int topperCount,
            int sinkBottleCount,
            float corkAspectRatioMin,
            float corkAspectRatioMax,
            float corkCenterOffsetRatioMax,
            float corkInsertionDepthRatioMin,
            float corkInsertionDepthRatioMax,
            float shadowLengthRatioMax)
        {
            try
            {
                string overlapStr = overlapDetected ? "true" : "false";
                string shadowOverlapStr = shadowOverlapDetected ? "true" : "false";
                string hudStr = hudIntrusionDetected ? "true" : "false";
                string corkValidStr = (corkAspectRatioMin >= 1.2f && corkAspectRatioMax <= 2.0f && corkCenterOffsetRatioMax <= 0.02f)
                    ? "true"
                    : "false";
                string insertionDepthValidStr = (corkInsertionDepthRatioMin >= 0.7f && corkInsertionDepthRatioMax <= 0.8f)
                    ? "true"
                    : "false";
                string shadowLengthValidStr = shadowLengthRatioMax <= 0.35f ? "true" : "false";
                // corkCount == completedBottleCount is the key invariant checked by
                // the automated convergence loop.  topperCount kept for back-compat.
                string json = "{\n" +
                              $"  \"bottleCount\": {activeBottleCount},\n" +
                              $"  \"bottleOverlapDetected\": {overlapStr},\n" +
                              $"  \"shadowOverlapDetected\": {shadowOverlapStr},\n" +
                              $"  \"hudIntrusionDetected\": {hudStr},\n" +
                              $"  \"completedBottleCount\": {topperCount},\n" +
                              $"  \"corkCount\": {topperCount},\n" +
                              $"  \"corkAspectRatioMin\": {corkAspectRatioMin:F4},\n" +
                              $"  \"corkAspectRatioMax\": {corkAspectRatioMax:F4},\n" +
                              $"  \"corkCenterOffsetRatioMax\": {corkCenterOffsetRatioMax:F6},\n" +
                              $"  \"corkInsertionDepthRatioMin\": {corkInsertionDepthRatioMin:F4},\n" +
                              $"  \"corkInsertionDepthRatioMax\": {corkInsertionDepthRatioMax:F4},\n" +
                              $"  \"corkInsertionDepthValid\": {insertionDepthValidStr},\n" +
                              $"  \"corkValidationPassed\": {corkValidStr},\n" +
                              $"  \"shadowLengthRatioMax\": {shadowLengthRatioMax:F4},\n" +
                              $"  \"shadowLengthConstraintPassed\": {shadowLengthValidStr},\n" +
                              $"  \"topperCount\": {topperCount},\n" +
                              $"  \"sinkBottleCount\": {sinkBottleCount},\n" +
                              $"  \"activeBottleCount\": {activeBottleCount},\n" +
                              $"  \"overlapDetected\": {overlapStr},\n" +
                              $"  \"generatedAt\": \"{System.DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}\"\n" +
                              "}";
                string screenshotsDir = Path.Combine(Application.persistentDataPath, "DecantraScreenshots");
                Directory.CreateDirectory(screenshotsDir);

                // Write as both the v2 name (back-compat) and the new cork-layout-report name.
                string pathV2 = Path.Combine(screenshotsDir, "v2-layout-report.json");
                string pathCork = Path.Combine(screenshotsDir, "cork-layout-report.json");
                File.WriteAllText(pathV2, json);
                File.WriteAllText(pathCork, json);

                Debug.Log($"[Bottle3DView] cork-layout-report written to {pathCork}: " +
                          $"overlap={overlapDetected} shadow={shadowOverlapDetected} hud={hudIntrusionDetected} " +
                          $"completedBottleCount={topperCount} corkCount={topperCount} sinks={sinkBottleCount} " +
                          $"corkAspect=[{corkAspectRatioMin:F3},{corkAspectRatioMax:F3}] " +
                          $"corkInsertionDepth=[{corkInsertionDepthRatioMin:F3},{corkInsertionDepthRatioMax:F3}] " +
                          $"corkCenterOffsetRatioMax={corkCenterOffsetRatioMax:F5} " +
                          $"shadowLengthRatioMax={shadowLengthRatioMax:F3}");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[Bottle3DView] Failed to write layout report: {ex.Message}");
            }
        }

        // ── Internals ─────────────────────────────────────────────────────────

        // ── Pour animation helpers ────────────────────────────────────────────

        private void UpdateDrainAtT(float t)
        {
            if (_drainTopLayerIndex >= _layerBlocks.Count
                || _drainTopLayerIndex >= _layerRenderers.Count
                || _layerRenderers[_drainTopLayerIndex] == null)
                return;

            float range = _drainTopLayerFillMax - _drainTopLayerFillMin;
            float animFill;
            if (range > 1e-5f)
            {
                // Lerp the visible top of the mesh: starts at FillMax, ends at drainTargetFill.
                animFill = Mathf.Lerp(_drainTopLayerFillMax, _drainTargetFill, t);
            }
            else
            {
                animFill = _drainTargetFill;
            }

            var block = _layerBlocks[_drainTopLayerIndex];
            block.SetFloat(PropTotalFill, animFill);
            _layerRenderers[_drainTopLayerIndex].SetPropertyBlock(block);
        }

        private void EnsureReceiveLayerGO()
        {
            // Destroy any stale receive layer first.
            if (_receiveLayerGO != null)
            {
                var old = _receiveLayerGO.GetComponent<MeshFilter>();
                if (old != null && old.sharedMesh != null)
                    Destroy(old.sharedMesh);
                Destroy(_receiveLayerGO);
                _receiveLayerGO = null;
            }

            if (_liquidRoot == null) return;

            _receiveLayerGO = new GameObject("LiquidLayer_Receive");
            _receiveLayerGO.transform.SetParent(_liquidRoot.transform, false);
            _receiveLayerGO.layer = _liquidRoot.layer;

            var mf = _receiveLayerGO.AddComponent<MeshFilter>();
            // Extend mesh slightly below _receiveFillFrom so the shader's boundary-arc
            // curved surface has geometry to render on, eliminating the transient gap
            // between existing liquid and incoming liquid during pour.  The shader's own
            // _Layer0Min + arc-offset logic prevents any visible over-draw.
            const float kArcOverlap = 0.015f; // just above _SurfaceArcHeight (0.012)
            float meshFillMin = Mathf.Max(0f, _receiveFillFrom - kArcOverlap);
            mf.sharedMesh = BottleMeshGenerator.GenerateLiquidLayerMesh(
                meshFillMin, _receiveFillTo, _capacityRatio);

            _receiveLayerRenderer = _receiveLayerGO.AddComponent<MeshRenderer>();
            _receiveLayerRenderer.sharedMaterial = liquidMaterialTemplate != null
                ? liquidMaterialTemplate
                : CreateFallbackLiquidMaterial();

            _receiveLayerBlock = new MaterialPropertyBlock();
            _receiveLayerBlock.SetColor(PropLayerColor[0], _receiveColor);
            _receiveLayerBlock.SetFloat(PropLayerMin[0], _receiveFillFrom);
            _receiveLayerBlock.SetFloat(PropLayerMax[0], _receiveFillTo);
            for (int k = 1; k < 9; k++)
            {
                _receiveLayerBlock.SetFloat(PropLayerMin[k], 0f);
                _receiveLayerBlock.SetFloat(PropLayerMax[k], 0f);
            }
            _receiveLayerBlock.SetInt(PropLayerCount, 1);
            _receiveLayerBlock.SetFloat(PropTotalFill, _receiveFillFrom);
            _receiveLayerRenderer.SetPropertyBlock(_receiveLayerBlock);
        }

        private void SetReceiveLayerTotalFill(float t)
        {
            if (_receiveLayerRenderer == null || _receiveLayerBlock == null) return;
            _receiveLayerBlock.SetFloat(
                PropTotalFill,
                Mathf.Clamp(t, _receiveFillFrom, _receiveFillTo));
            _receiveLayerRenderer.SetPropertyBlock(_receiveLayerBlock);
        }

        private void EnsureInitialised()
        {
            if (_initialised) return;

            // ── Create the scene-root world node ────────────────────────────────
            // This must NOT be parented under the Canvas hierarchy.  Canvas elements in
            // ScreenSpaceCamera mode carry a world lossyScale of ~0.005 (canvas pixels /
            // screen pixel height * orthographic view height).  Any mesh child would
            // inherit that scale and be ~200× too small.  Staying at scene root gives us
            // scale (1,1,1) so mesh units match world units directly.
            //
            // Layer must be "Game" so Camera_Game (cullingMask = Game only) can see it.
            int targetLayer = gameObject.layer;  // inherited "Game" layer from Canvas setup
            var worldPos = transform.position;

            _worldRoot = new GameObject($"Bottle3DWorld_{gameObject.name}");
            _worldRoot.transform.position = new Vector3(worldPos.x, worldPos.y, 0f);
            _worldRoot.transform.rotation = Quaternion.identity;
            // Initial scale — immediately overridden by SyncWorldRootPosition once the
            // canvas has been laid out, which gives us the actual cell world height.
            _worldRoot.transform.localScale = new Vector3(VisualScale, VisualScale, VisualScale);
            _worldRoot.layer = targetLayer;

            // ── Glass body ──────────────────────────────────────────────────────
            _glassBodyGO = new GameObject("GlassBody");
            _glassBodyGO.transform.SetParent(_worldRoot.transform, false);
            _glassBodyGO.layer = targetLayer;
            var meshFilter = _glassBodyGO.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = BottleMeshGenerator.GenerateBottleMesh(_capacityRatio);
            var meshRenderer = _glassBodyGO.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = glassMaterialTemplate != null
                ? glassMaterialTemplate
                : CreateFallbackGlassMaterial();

            _outlineGO = new GameObject("BottleOutline");
            _outlineGO.transform.SetParent(_worldRoot.transform, false);
            _outlineGO.layer = targetLayer;
            var outlineFilter = _outlineGO.AddComponent<MeshFilter>();
            outlineFilter.sharedMesh = BottleMeshGenerator.GenerateBottleMesh(_capacityRatio);
            _outlineRenderer = _outlineGO.AddComponent<MeshRenderer>();
            _outlineRenderer.sharedMaterial = outlineMaterialTemplate != null
                ? outlineMaterialTemplate
                : CreateFallbackOutlineMaterial();

            // ── Liquid layer parent ─────────────────────────────────────────────
            _liquidRoot = new GameObject("LiquidLayers");
            _liquidRoot.transform.SetParent(_worldRoot.transform, false);
            _liquidRoot.layer = targetLayer;
            _liquidRoot.transform.localPosition = new Vector3(0f, 0f, -0.008f);

            // ── Contact shadow ──────────────────────────────────────────────────
            _contactShadowGO = new GameObject("ContactShadow");
            _contactShadowGO.transform.SetParent(_worldRoot.transform, false);
            _contactShadowGO.layer = targetLayer;
            // Keep the contact shadow compact and close to the bottle base so it does not
            // visually read as part of the bottle on the row below.
            _contactShadowGO.transform.localPosition = new Vector3(0f, BottleMeshGenerator.ExteriorBottomY - 0.028f, 0.06f);
            // Euler(90,0,0) rotates the XZ-plane disk to lie flat in XY as a ground shadow.
            _contactShadowGO.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            _contactShadowGO.transform.localScale = new Vector3(0.07f, 0.07f, 0.025f);
            var shadowFilter = _contactShadowGO.AddComponent<MeshFilter>();
            shadowFilter.sharedMesh = CreateShadowDiskMesh();
            var shadowRenderer = _contactShadowGO.AddComponent<MeshRenderer>();
            shadowRenderer.sharedMaterial = CreateFallbackShadowMaterial();

            _neckOverlayGO = new GameObject("BottleNeckOverlay");
            _neckOverlayGO.transform.SetParent(_worldRoot.transform, false);
            _neckOverlayGO.layer = targetLayer;
            var neckOverlayFilter = _neckOverlayGO.AddComponent<MeshFilter>();
            neckOverlayFilter.sharedMesh = BottleMeshGenerator.GenerateNeckOverlayMesh(_capacityRatio, NeckOverlayOutset);
            _neckOverlayRenderer = _neckOverlayGO.AddComponent<MeshRenderer>();
            _neckOverlayRenderer.sharedMaterial = CreateDetailMaterial("BottleNeckOverlay_Mat", RegularNeckOverlayColor, 3002);

            _topBoundaryCollarGO = new GameObject("BottleTopBoundaryCollar");
            _topBoundaryCollarGO.transform.SetParent(_worldRoot.transform, false);
            _topBoundaryCollarGO.layer = targetLayer;
            var topCollarFilter = _topBoundaryCollarGO.AddComponent<MeshFilter>();
            topCollarFilter.sharedMesh = BottleMeshGenerator.GenerateBoundaryCollarMesh(_capacityRatio, topBoundary: true);
            _topBoundaryCollarRenderer = _topBoundaryCollarGO.AddComponent<MeshRenderer>();
            _topBoundaryCollarRenderer.sharedMaterial = CreateDetailMaterial("BottleTopBoundaryCollar_Mat", BoundaryCollarColor, 3002);

            _bottomBoundaryCollarGO = new GameObject("BottleBottomBoundaryCollar");
            _bottomBoundaryCollarGO.transform.SetParent(_worldRoot.transform, false);
            _bottomBoundaryCollarGO.layer = targetLayer;
            var bottomCollarFilter = _bottomBoundaryCollarGO.AddComponent<MeshFilter>();
            bottomCollarFilter.sharedMesh = BottleMeshGenerator.GenerateBoundaryCollarMesh(_capacityRatio, topBoundary: false);
            _bottomBoundaryCollarRenderer = _bottomBoundaryCollarGO.AddComponent<MeshRenderer>();
            _bottomBoundaryCollarRenderer.sharedMaterial = CreateDetailMaterial("BottleBottomBoundaryCollar_Mat", BoundaryCollarColor, 3002);

            // ── Bottle stopper / cork ────────────────────────────────────────────
            // Keep the cork visible for the closed-bottle silhouette. Its tint changes
            // with bottle state, but the geometry remains present so each bottle still
            // reads as physically closed from gameplay distance.
            _stopperGO = new GameObject("BottleStopper");
            _stopperGO.transform.SetParent(_worldRoot.transform, false);
            _stopperGO.layer = targetLayer;
            _stopperGO.transform.localPosition = new Vector3(
                0f,
                BottleMeshGenerator.GetStopperBaseY(_capacityRatio),
                -0.006f);  // in front of glass so cork renders over glass at rim
            var stopperMf = _stopperGO.AddComponent<MeshFilter>();
            stopperMf.sharedMesh = CreateStopperMesh();
            _stopperRenderer = _stopperGO.AddComponent<MeshRenderer>();
            _stopperRenderer.sharedMaterial = CreateStopperMaterial(StopperNeutralColor);
            _stopperGO.SetActive(false);

            // ── Interaction collider ────────────────────────────────────────────
            // Kept on the canvas element (this.gameObject) so that existing UI
            // raycasting (blocksRaycasts = true) continues to work for drag/drop input.
            EnsureInteractionCollider();

            ApplySinkOnlyToGlass();

            _initialised = true;
        }

        /// <summary>Sync the scene-root world node to this transform's world XY each frame.
        /// Also recomputes the worldRoot scale based on the actual canvas cell world height so
        /// the bottle fits its cell correctly regardless of runtime screen resolution or rotation.
        /// </summary>
        private void SyncWorldRootPosition()
        {
            if (_worldRoot == null) return;
            var p = transform.position;
            _worldRoot.transform.position = new Vector3(p.x, p.y, 0f);

            // Dynamic scale: derive the world-unit height of this bottle's canvas cell
            // from the RectTransform.  TransformVector applies lossyScale, converting the
            // reference-pixel cell height to world units for whatever runtime resolution.
            var rt = GetComponent<RectTransform>();
            if (rt != null)
            {
                float cellWorldH = rt.TransformVector(new Vector3(0f, rt.rect.height, 0f)).magnitude;
                if (cellWorldH > 0.01f)
                {
                    float newScale = cellWorldH * HeightFitFraction / MeshFullHeight;
                    var cur = _worldRoot.transform.localScale;
                    float targetXzScale = newScale * WidthFitMultiplier;
                    if (Mathf.Abs(targetXzScale - cur.x) > 1e-4f || Mathf.Abs(newScale - cur.y) > 1e-4f)
                        _worldRoot.transform.localScale = new Vector3(targetXzScale, newScale, targetXzScale);
                }
            }
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            if (root == null) return;
            root.layer = layer;
            for (int i = 0; i < root.transform.childCount; i++)
            {
                var child = root.transform.GetChild(i);
                if (child != null)
                    SetLayerRecursively(child.gameObject, layer);
            }
        }

        private void EnsureInteractionCollider()
        {
            var box = GetComponent<BoxCollider>();
            if (box == null)
            {
                box = gameObject.AddComponent<BoxCollider>();
            }

            box.isTrigger = true;
            box.center = new Vector3(
                0f,
                BottleMeshGenerator.ExteriorBottomY + BottleMeshGenerator.ReferenceMeshHeight * 0.5f,
                0f);
            box.size = new Vector3(
                BottleMeshGenerator.BodyRadius * 2.15f * WidthFitMultiplier,
                LegacyColliderHeight * (HeightFitFraction / LegacyColliderFitFraction),
                BottleMeshGenerator.BodyRadius * 1.15f * WidthFitMultiplier);
        }

        private void EnsureLayerObjects(int requiredCount)
        {
            // Deactivate excess layers
            for (int i = requiredCount; i < _layerRenderers.Count; i++)
            {
                if (_layerRenderers[i] != null)
                    _layerRenderers[i].gameObject.SetActive(false);
            }

            // Create missing layers
            for (int i = _layerRenderers.Count; i < requiredCount; i++)
            {
                var go = new GameObject($"LiquidLayer_{i}");
                go.transform.SetParent(_liquidRoot.transform, false);
                go.layer = _worldRoot != null ? _worldRoot.layer : gameObject.layer;
                go.AddComponent<MeshFilter>();
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = liquidMaterialTemplate != null
                    ? liquidMaterialTemplate
                    : CreateFallbackLiquidMaterial();
                _layerRenderers.Add(mr);
                _layerBlocks.Add(new MaterialPropertyBlock());
                _cachedFillBounds.Add((-1f, -1f)); // sentinel: force mesh build on first render
            }

            // Activate layers up to required count
            for (int i = 0; i < requiredCount; i++)
            {
                if (_layerRenderers[i] != null)
                    _layerRenderers[i].gameObject.SetActive(true);
            }
        }

        private void ApplyLayerProperties(List<LiquidLayerData> layers, Bottle bottle)
        {
            float topSurfaceFill = FillHeightMapper.TopSurfaceFill(bottle);

            for (int i = 0; i < layers.Count; i++)
            {
                var layer = layers[i];

                // Only rebuild mesh when fill bounds changed (avoids per-frame GPU upload)
                var mf = _layerRenderers[i].GetComponent<MeshFilter>();
                if (mf != null)
                {
                    var cached = _cachedFillBounds[i];
                    bool boundsChanged = Mathf.Abs(cached.Item1 - layer.FillMin) > 1e-5f
                                     || Mathf.Abs(cached.Item2 - layer.FillMax) > 1e-5f;
                    if (boundsChanged)
                    {
                        if (mf.sharedMesh != null)
                            Destroy(mf.sharedMesh);  // async destroy (not DestroyImmediate)
                        mf.sharedMesh = BottleMeshGenerator.GenerateLiquidLayerMesh(layer.FillMin, layer.FillMax, _capacityRatio);
                        _cachedFillBounds[i] = (layer.FillMin, layer.FillMax);
                    }
                }

                var block = _layerBlocks[i];

                // Set all 9 layer colors/fills on the material (shader reads all 9)
                // We use a single shared layer per mesh so index 0 is always "this layer"
                block.SetColor(PropLayerColor[0], LiquidColorTuning.ApplyGameplayVibrancy(new Color(layer.R, layer.G, layer.B, 1f)));
                block.SetFloat(PropLayerMin[0], layer.FillMin);
                block.SetFloat(PropLayerMax[0], layer.FillMax);

                // Clear upper layers
                for (int k = 1; k < 9; k++)
                {
                    block.SetFloat(PropLayerMin[k], 0f);
                    block.SetFloat(PropLayerMax[k], 0f);
                }

                block.SetFloat(PropTotalFill, topSurfaceFill);
                block.SetInt(PropLayerCount, 1);            // single layer per mesh

                _layerRenderers[i].SetPropertyBlock(block);
            }
        }

        private void UpdateTiltFromRotation(float currentZRotDeg, float dt)
        {
            _currentSurfaceTiltDeg = SurfaceTiltCalculator.ComputeTiltDegrees(
                currentZRotDeg,
                WobbleSolver.MaxTiltDegrees);

            if (dt <= 0f)
            {
                _previousZRotation = currentZRotDeg;
                return;
            }

            if (_hasPreviousRotationSample)
            {
                float delta = Mathf.DeltaAngle(_previousZRotation, currentZRotDeg);
                float angularVelocityRad = delta * Mathf.Deg2Rad / dt;
                float angularAccelerationRad = (angularVelocityRad - _previousAngularVelocityRad) / dt;
                float impulse = angularVelocityRad * AngularVelocityImpulseScale
                              + angularAccelerationRad * AngularAccelerationImpulseScale;

                if (Mathf.Abs(impulse) > 0.0001f)
                {
                    _wobble.ApplyImpulse(impulse);
                }

                _previousAngularVelocityRad = angularVelocityRad;
            }
            else
            {
                _previousAngularVelocityRad = 0f;
                _hasPreviousRotationSample = true;
            }

            _previousZRotation = currentZRotDeg;
        }

        private void CleanupLayerObjects()
        {
            for (int i = 0; i < _layerRenderers.Count; i++)
            {
                if (_layerRenderers[i] != null)
                {
                    var mf = _layerRenderers[i].GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null)
                        Destroy(mf.sharedMesh);

                    if (liquidMaterialTemplate == null && _layerRenderers[i].sharedMaterial != null)
                        Destroy(_layerRenderers[i].sharedMaterial);
                }
            }
            _layerRenderers.Clear();
            _layerBlocks.Clear();
            _cachedFillBounds.Clear();
        }

        /// <summary>
        /// Push the sink/highlight state into the glass rim response and outline shell.
        /// Uses MaterialPropertyBlock updates so the shared materials remain instanced only once.
        /// </summary>
        private void ApplySinkOnlyToGlass()
        {
            if (_glassBodyGO == null) return;
            var mr = _glassBodyGO.GetComponent<MeshRenderer>();
            if (mr == null) return;

            var block = new MaterialPropertyBlock();
            mr.GetPropertyBlock(block);
            block.SetFloat(PropSinkOnly, _isSinkOnly ? 1f : 0f);
            mr.SetPropertyBlock(block);

            Color rimColor = _isHighlighted
                ? RimSheenHighlightColor
                : (_isSinkOnly ? RimSheenSinkColor : RimSheenDefaultColor);
            float rimIntensity = _isHighlighted
                ? RimSheenHighlightIntensity
                : (_isSinkOnly ? RimSheenSinkIntensity : RimSheenDefaultIntensity);
            float rimPower = _isHighlighted
                ? RimSheenHighlightPower
                : (_isSinkOnly ? RimSheenSinkPower : RimSheenDefaultPower);
            float emptyBottleBoost = (!_isHighlighted && (_isEmptyBottle || _isSinkOnly)) ? 1f : 0f;

            block.SetColor(PropRimSheenColor, rimColor);
            block.SetFloat(PropRimSheenIntensity, rimIntensity);
            block.SetFloat(PropRimSheenPower, rimPower);
            block.SetFloat(PropEmptyBottleBoost, emptyBottleBoost);
            mr.SetPropertyBlock(block);

            ApplyOutlineState();
            ApplyNeckOverlayState();
            ApplyBoundaryCollarState();
        }

        private void ApplyNeckOverlayState()
        {
            if (_neckOverlayRenderer == null) return;

            var block = new MaterialPropertyBlock();
            _neckOverlayRenderer.GetPropertyBlock(block);
            block.SetColor(PropStopperColor, _isSinkOnly ? SinkNeckOverlayColor : RegularNeckOverlayColor);
            _neckOverlayRenderer.SetPropertyBlock(block);
        }

        private void ApplyBoundaryCollarState()
        {
            ApplyBoundaryCollarColor(_topBoundaryCollarRenderer);
            ApplyBoundaryCollarColor(_bottomBoundaryCollarRenderer);
        }

        private static void ApplyBoundaryCollarColor(MeshRenderer renderer)
        {
            if (renderer == null) return;

            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            block.SetColor(PropStopperColor, BoundaryCollarColor);
            renderer.SetPropertyBlock(block);
        }

        private void ApplyOutlineState()
        {
            if (_outlineRenderer == null) return;

            bool outlineEnabled = _isHighlighted;
            _outlineRenderer.enabled = outlineEnabled;
            if (!outlineEnabled) return;

            Color outlineColor = _isHighlighted
                ? OutlineHighlightColor
                : OutlineSinkColor;
            float outlineWidth = _isHighlighted
                ? OutlineHighlightWidth
                : OutlineSinkWidth;

            var block = new MaterialPropertyBlock();
            _outlineRenderer.GetPropertyBlock(block);
            block.SetColor(PropOutlineGlowColor, outlineColor);
            block.SetFloat(PropOutlineWidth, outlineWidth);
            _outlineRenderer.SetPropertyBlock(block);
        }

        /// <summary>
        /// Apply <paramref name="ratio"/> (bottle.Capacity / levelMaxCapacity) using the
        /// 2D BottleView body-only stretch philosophy: the glass mesh is regenerated with
        /// the scaled body height baked in, so normals and UVs are always correct.
        /// Only the cylindrical body section scales; the dome (bottom) and shoulder+neck+rim
        /// (top) stay at their reference sizes.
        /// </summary>
        private void ApplyCapacityRatio(float ratio)
        {
            // 2D-style body-only stretch: regenerate the glass mesh with the
            // scaled body height baked in, so normals and UVs are always correct.
            // Only dome (bottom) and shoulder/neck/rim (top) stay at full size.
            bool ratioChanged = Mathf.Abs(ratio - _capacityRatio) > 1e-4f;
            _capacityRatio = ratio;

            // Glass body — rebuild mesh if ratio changed
            if (ratioChanged && _glassBodyGO != null)
            {
                var mf = _glassBodyGO.GetComponent<MeshFilter>();
                if (mf != null)
                {
                    if (mf.sharedMesh != null)
                        Destroy(mf.sharedMesh);
                    mf.sharedMesh = BottleMeshGenerator.GenerateBottleMesh(_capacityRatio);
                }

                if (_outlineGO != null)
                {
                    var outlineMf = _outlineGO.GetComponent<MeshFilter>();
                    if (outlineMf != null)
                    {
                        if (outlineMf.sharedMesh != null)
                            Destroy(outlineMf.sharedMesh);
                        outlineMf.sharedMesh = BottleMeshGenerator.GenerateBottleMesh(_capacityRatio);
                    }
                }

                if (_neckOverlayGO != null)
                {
                    var neckMf = _neckOverlayGO.GetComponent<MeshFilter>();
                    if (neckMf != null)
                    {
                        if (neckMf.sharedMesh != null)
                            Destroy(neckMf.sharedMesh);
                        neckMf.sharedMesh = BottleMeshGenerator.GenerateNeckOverlayMesh(_capacityRatio, NeckOverlayOutset);
                    }
                }

                if (_topBoundaryCollarGO != null)
                {
                    var topCollarMf = _topBoundaryCollarGO.GetComponent<MeshFilter>();
                    if (topCollarMf != null)
                    {
                        if (topCollarMf.sharedMesh != null)
                            Destroy(topCollarMf.sharedMesh);
                        topCollarMf.sharedMesh = BottleMeshGenerator.GenerateBoundaryCollarMesh(_capacityRatio, topBoundary: true);
                    }
                }

                if (_bottomBoundaryCollarGO != null)
                {
                    var bottomCollarMf = _bottomBoundaryCollarGO.GetComponent<MeshFilter>();
                    if (bottomCollarMf != null)
                    {
                        if (bottomCollarMf.sharedMesh != null)
                            Destroy(bottomCollarMf.sharedMesh);
                        bottomCollarMf.sharedMesh = BottleMeshGenerator.GenerateBoundaryCollarMesh(_capacityRatio, topBoundary: false);
                    }
                }

                // Invalidate all liquid layer meshes so they rebuild with the new ratio
                for (int i = 0; i < _cachedFillBounds.Count; i++)
                    _cachedFillBounds[i] = (-1f, -1f);

                // Reposition stopper to match the new neck-top Y for this capacity.
                if (_stopperGO != null)
                    _stopperGO.transform.localPosition = new Vector3(
                        0f,
                        BottleMeshGenerator.GetStopperBaseY(_capacityRatio),
                        -0.006f);

            }

            ApplySinkOnlyToGlass();
        }

        // ── Obj-3: Completed bottle cork stopper ───────────────────────────────

        /// <summary>
        /// Keep the cork visible on closed bottles and tint it to match a solved bottle's
        /// liquid colour. Mixed, empty, and sink bottles use the neutral cork tone.
        /// </summary>
        private void UpdateTopper(Bottle bottle, List<LiquidLayerData> layers)
        {
            _wasCompleted = bottle.IsSolvedBottle();

            if (_stopperGO != null)
            {
                _stopperGO.SetActive(_wasCompleted);
            }

            if (_stopperRenderer == null || !_wasCompleted)
                return;

            Color stopperColor = StopperNeutralColor;
            if (_wasCompleted && layers.Count > 0)
            {
                stopperColor = new Color(layers[0].R, layers[0].G, layers[0].B, 1f);
            }

            var block = new MaterialPropertyBlock();
            _stopperRenderer.GetPropertyBlock(block);
            block.SetColor(PropStopperColor, stopperColor);
            _stopperRenderer.SetPropertyBlock(block);
        }

        /// <summary>
        /// Rounded cork mesh for completed bottles: fits snugly in the bottle neck,
        /// peeks above the rim, and uses a light bevel so the silhouette reads as a cork
        /// rather than a flat white rectangle in front-view captures.
        /// </summary>
        private static Mesh CreateStopperMesh()
        {
            const int segments = 28;
            float radius = BottleMeshGenerator.StopperRadius;
            float height = BottleMeshGenerator.StopperTotalHeight;
            float bevelHeight = Mathf.Min(height * 0.22f, radius * 0.55f);
            float bevelRadius = radius * 0.90f;

            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            // ── Bevelled cork side wall (four rings) ─────────────────────────
            var ringHeights = new[]
            {
                0f,
                bevelHeight,
                height - bevelHeight,
                height
            };
            var ringRadii = new[]
            {
                bevelRadius,
                radius,
                radius,
                bevelRadius
            };

            int stride = segments + 1;
            for (int ring = 0; ring < ringHeights.Length; ring++)
            {
                float y = ringHeights[ring];
                float ringRadius = ringRadii[ring];
                for (int i = 0; i <= segments; i++)
                {
                    float angle = (float)i / segments * Mathf.PI * 2f;
                    float cos = Mathf.Cos(angle);
                    float sin = Mathf.Sin(angle);
                    verts.Add(new Vector3(cos * ringRadius, y, sin * ringRadius));

                    float ny = 0f;
                    if (ring == 0)
                        ny = -0.45f;
                    else if (ring == ringHeights.Length - 1)
                        ny = 0.45f;

                    norms.Add(new Vector3(cos, ny, sin).normalized);
                    uvs.Add(new Vector2((float)i / segments, y / Mathf.Max(height, 1e-5f)));
                }
            }
            for (int ring = 0; ring < ringHeights.Length - 1; ring++)
            {
                int baseIndex = ring * stride;
                int nextIndex = (ring + 1) * stride;
                for (int i = 0; i < segments; i++)
                {
                    int b0 = baseIndex + i;
                    int b1 = baseIndex + i + 1;
                    int t0 = nextIndex + i;
                    int t1 = nextIndex + i + 1;
                    tris.Add(b0); tris.Add(t0); tris.Add(b1);
                    tris.Add(b1); tris.Add(t0); tris.Add(t1);
                }
            }

            // ── Flat top cap ──────────────────────────────────────────────────
            int capBase = verts.Count;
            verts.Add(new Vector3(0f, height, 0f));
            norms.Add(Vector3.up);
            uvs.Add(new Vector2(0.5f, 0.5f));
            for (int i = 0; i <= segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);
                verts.Add(new Vector3(cos * bevelRadius, height, sin * bevelRadius));
                norms.Add(Vector3.up);
                uvs.Add(new Vector2(cos * 0.5f + 0.5f, sin * 0.5f + 0.5f));
            }
            for (int i = 0; i < segments; i++)
            {
                int c = capBase;
                int a = capBase + 1 + i;
                int b = capBase + 1 + (i + 1 <= segments ? i + 1 : 0);
                tris.Add(c); tris.Add(a); tris.Add(b);
            }

            var mesh = new Mesh { name = "BottleStopperMesh" };
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            mesh.UploadMeshData(markNoLongerReadable: true);
            return mesh;
        }

        /// <summary>
        /// Material for the cork stopper.
        /// Prefers the custom Decantra/CorkStopper shader (lit, matte, procedural pore noise).
        /// Falls back through Sprites/Default to Unlit/Color if custom shader is stripped.
        /// </summary>
        private static Material CreateStopperMaterial(Color color)
        {
            // CorkStopper shader: lit Blinn-Phong, procedural pore/grain noise, matte.
            var shader = Shader.Find("Decantra/CorkStopper")
                      ?? Shader.Find("Sprites/Default")
                      ?? Shader.Find("Unlit/Color")
                      ?? Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                Debug.LogWarning("Bottle3DView: cannot resolve stopper shader.");
                return null;
            }

            var mat = new Material(shader) { name = "BottleStopper_Mat" };
            // _Color is used by CorkStopper (tint), Sprites/Default and Unlit/Color.
            mat.SetColor("_Color", color);
            if (shader.name == "Decantra/CorkStopper")
            {
                mat.SetFloat("_Ambient", 0.18f);
                mat.SetFloat("_SpecStr", 0f);
            }
            // Render after transparent glass (queue Transparent = 3000, glass = Transparent+1 = 3001)
            // so the cork appears on top of / through the glass neck. ZWrite off to avoid
            // breaking transparent compositing order for other bottles.
            mat.renderQueue = 3002;
            return mat;
        }

        /// <summary>
        /// Write the v2 layout report by sampling the current active view states.
        /// Call this after a solve sequence to capture topperCount &gt; 0.
        /// </summary>
        public static void WriteReport()
        {
            var boardViews = CollectDominantLayoutViews();
            int count = boardViews.Count;
            var bounds = new Bounds[count];
            bool overlapDet = false;
            bool shadowOverlapDet = false;
            bool hudDet = false;
            int sinks = 0;
            int toppers = 0;
            float corkAspectRatioMin = float.MaxValue;
            float corkAspectRatioMax = 0f;
            float corkCenterOffsetRatioMax = 0f;
            float corkInsertionDepthRatioMin = float.MaxValue;
            float corkInsertionDepthRatioMax = 0f;
            float shadowLengthRatioMax = 0f;
            const float HudBoundaryY = 4.35f;

            for (int i = 0; i < count; i++)
            {
                var v = boardViews[i];
                if (v == null) continue;
                if (v._isSinkOnly) sinks++;
                if (v._wasCompleted)
                {
                    toppers++;
                    float aspect = BottleMeshGenerator.StopperTotalHeight / Mathf.Max(BottleMeshGenerator.StopperRadius, 1e-5f);
                    if (aspect < corkAspectRatioMin) corkAspectRatioMin = aspect;
                    if (aspect > corkAspectRatioMax) corkAspectRatioMax = aspect;

                    if (v._stopperGO != null)
                    {
                        float rimTopY = BottleMeshGenerator.GetRimTopY(v._capacityRatio);
                        float insertionDepth = rimTopY - v._stopperGO.transform.localPosition.y;
                        float insertionDepthRatio = insertionDepth / Mathf.Max(BottleMeshGenerator.StopperTotalHeight, 1e-5f);
                        if (insertionDepthRatio < corkInsertionDepthRatioMin) corkInsertionDepthRatioMin = insertionDepthRatio;
                        if (insertionDepthRatio > corkInsertionDepthRatioMax) corkInsertionDepthRatioMax = insertionDepthRatio;

                        float centerOffset = Mathf.Sqrt(
                            v._stopperGO.transform.localPosition.x * v._stopperGO.transform.localPosition.x +
                            v._stopperGO.transform.localPosition.z * v._stopperGO.transform.localPosition.z);
                        float centerOffsetRatio = centerOffset / Mathf.Max(BottleMeshGenerator.NeckRadius, 1e-5f);
                        if (centerOffsetRatio > corkCenterOffsetRatioMax) corkCenterOffsetRatioMax = centerOffsetRatio;
                    }
                }
                if (v._glassBodyGO != null)
                {
                    var mr = v._glassBodyGO.GetComponent<MeshRenderer>();
                    if (mr != null) bounds[i] = mr.bounds;
                }
            }

            for (int i = 0; i < count; i++)
            {
                if (bounds[i].max.y > HudBoundaryY) hudDet = true;
                for (int j = i + 1; j < count; j++)
                {
                    if (bounds[i].Intersects(bounds[j])) overlapDet = true;
                }
            }

            // Shadow overlap check via renderer bounds intersection (spec-compliant).
            for (int i = 0; i < count; i++)
            {
                var vi = boardViews[i];
                if (vi == null || vi._contactShadowGO == null) continue;
                var shadowMr = vi._contactShadowGO.GetComponent<MeshRenderer>();
                if (shadowMr == null) continue;
                var shadowBounds = shadowMr.bounds;

                float bottleHeight = bounds[i].size.y;
                if (bottleHeight > 1e-5f)
                {
                    float shadowLength = Mathf.Max(shadowBounds.size.x, shadowBounds.size.z);
                    float ratio = shadowLength / bottleHeight;
                    if (ratio > shadowLengthRatioMax) shadowLengthRatioMax = ratio;
                }

                for (int j = 0; j < count; j++)
                {
                    if (i == j) continue;
                    if (shadowBounds.Intersects(bounds[j])) shadowOverlapDet = true;
                }
            }

            if (corkAspectRatioMin == float.MaxValue)
                corkAspectRatioMin = 0f;
            if (corkInsertionDepthRatioMin == float.MaxValue)
                corkInsertionDepthRatioMin = 0f;

            WriteLayoutReport(
                count,
                overlapDet,
                shadowOverlapDet,
                hudDet,
                toppers,
                sinks,
                corkAspectRatioMin,
                corkAspectRatioMax,
                corkCenterOffsetRatioMax,
                corkInsertionDepthRatioMin,
                corkInsertionDepthRatioMax,
                shadowLengthRatioMax);
        }

        private static List<Bottle3DView> CollectDominantLayoutViews()
        {
            PruneInactiveViews();

            Transform dominantRoot = null;
            int dominantCount = 0;
            var rootCounts = new Dictionary<Transform, int>();
            for (int i = 0; i < s_activeViews.Count; i++)
            {
                var view = s_activeViews[i];
                if (!IsLayoutEligible(view))
                    continue;

                Transform root = view.GetLayoutGroupRoot();
                if (root == null)
                    continue;
                if (!rootCounts.TryGetValue(root, out int count))
                    count = 0;

                count++;
                rootCounts[root] = count;
                if (count > dominantCount)
                {
                    dominantCount = count;
                    dominantRoot = root;
                }
            }

            return CollectViewsForLayoutRoot(dominantRoot);
        }

        private static List<Bottle3DView> CollectViewsForLayoutRoot(Transform layoutRoot)
        {
            PruneInactiveViews();

            var views = new List<Bottle3DView>(s_activeViews.Count);
            if (layoutRoot == null)
                return views;

            for (int i = 0; i < s_activeViews.Count; i++)
            {
                var view = s_activeViews[i];
                if (!IsLayoutEligible(view))
                    continue;
                if (view.GetLayoutGroupRoot() != layoutRoot)
                    continue;

                views.Add(view);
            }

            return views;
        }

        private static bool IsLayoutEligible(Bottle3DView view)
        {
            return view != null
                && view.isActiveAndEnabled
                && view._worldRoot != null
                && view._worldRoot.activeInHierarchy;
        }

        private Transform GetLayoutGroupRoot()
        {
            Transform current = transform;
            while (current != null)
            {
                if (current.GetComponent<UnityEngine.UI.GridLayoutGroup>() != null)
                    return current;
                current = current.parent;
            }

            return transform.parent;
        }

        private static void PruneInactiveViews()
        {
            s_activeViews.RemoveAll(view => !IsLayoutEligible(view));
        }

        private static Material CreateFallbackGlassMaterial()
        {
            // Prefer the custom BottleGlass shader first — it has Fresnel + Blinn-Phong
            // specular driven by scene directional lights (visible 3D highlights).
            // Fall back to URP Lit if the custom shader is unavailable (e.g. stripped build).
            var shader = Shader.Find("Decantra/BottleGlass")
                      ?? Shader.Find("Universal Render Pipeline/Lit")
                      ?? Shader.Find("Standard")
                      ?? Shader.Find("Sprites/Default")
                      ?? Shader.Find("Unlit/Color");

            if (shader == null)
            {
                Debug.LogError("Bottle3DView: unable to resolve fallback glass shader.");
                return null;
            }

            var material = new Material(shader) { name = "BottleGlass_Fallback" };

            // URP Lit transparent setup (mobile-safe, no heavy features).
            if (shader != null && shader.name == "Universal Render Pipeline/Lit")
            {
                material.SetFloat("_Surface", 1f); // Transparent
                material.SetFloat("_Blend", 0f); // Alpha
                material.SetFloat("_ZWrite", 0f);
                material.SetFloat("_Cull", 2f);
                material.SetFloat("_Metallic", 0f);
                material.SetFloat("_Smoothness", 0.95f);
                material.SetFloat("_SpecularHighlights", 1f);
                material.SetFloat("_EnvironmentReflections", 1f);
                material.SetColor("_BaseColor", new Color(0.86f, 0.93f, 1f, 0.2f));
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = 3001;
            }

            return material;
        }

        private static Material CreateFallbackOutlineMaterial()
        {
            var shader = Shader.Find("Decantra/BottleOutline");
            if (shader == null)
            {
                Debug.LogError("Bottle3DView: unable to resolve outline shader.");
                return null;
            }

            var material = new Material(shader) { name = "BottleOutline_Fallback" };
            material.renderQueue = 3003;
            return material;
        }

        private static Material CreateFallbackLiquidMaterial()
        {
            var shader = Shader.Find("Decantra/Liquid3D")
                      ?? Shader.Find("Universal Render Pipeline/Lit")
                      ?? Shader.Find("Standard")
                      ?? Shader.Find("Sprites/Default")
                      ?? Shader.Find("Unlit/Color");

            if (shader == null)
            {
                Debug.LogError("Bottle3DView: unable to resolve fallback liquid shader.");
                return null;
            }

            return new Material(shader) { name = "Liquid3D_Fallback" };
        }

        private static Material CreateDetailMaterial(string name, Color color, int renderQueue)
        {
            var shader = Shader.Find("Unlit/Color")
                      ?? Shader.Find("Sprites/Default")
                      ?? Shader.Find("Standard")
                      ?? Shader.Find("Universal Render Pipeline/Unlit");

            if (shader == null)
            {
                Debug.LogError($"Bottle3DView: unable to resolve detail shader for {name}.");
                return null;
            }

            var material = new Material(shader) { name = name };
            material.SetColor("_Color", color);
            material.renderQueue = renderQueue;
            return material;
        }

        private static Material CreateFallbackShadowMaterial()
        {
            var shader = Shader.Find("Unlit/Color")
                      ?? Shader.Find("Sprites/Default")
                      ?? Shader.Find("Standard")
                      ?? Shader.Find("Universal Render Pipeline/Lit");

            if (shader == null)
            {
                Debug.LogError("Bottle3DView: unable to resolve fallback shadow shader.");
                return null;
            }

            var material = new Material(shader) { name = "BottleContactShadow_Fallback" };
            // Keep the shadow understated so it grounds the bottle without competing with
            // the row below.
            // Alpha blending must be enabled so the semi-transparent shadow composes
            // correctly over the game background without occluding other objects.
            material.color = new Color(0f, 0f, 0f, 0.015f);
            if (shader.name != "Unlit/Color")
            {
                // Ensure alpha blending for non-Unlit shaders
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_Blend", 0f);
                material.SetFloat("_ZWrite", 0f);
            }
            return material;
        }

        private static Mesh CreateShadowDiskMesh()
        {
            const int segments = 20;
            var mesh = new Mesh { name = "BottleContactShadowDisk" };
            var vertices = new Vector3[segments + 1];
            var normals = new Vector3[segments + 1];
            var uv = new Vector2[segments + 1];
            var tris = new int[segments * 3];

            vertices[0] = Vector3.zero;
            normals[0] = Vector3.up;
            uv[0] = new Vector2(0.5f, 0.5f);

            for (int i = 0; i < segments; i++)
            {
                float t = i / (float)segments;
                float angle = t * Mathf.PI * 2f;
                float x = Mathf.Cos(angle);
                float z = Mathf.Sin(angle);
                int vi = i + 1;
                vertices[vi] = new Vector3(x, 0f, z);
                normals[vi] = Vector3.up;
                uv[vi] = new Vector2(x * 0.5f + 0.5f, z * 0.5f + 0.5f);

                int ti = i * 3;
                tris[ti] = 0;
                tris[ti + 1] = vi;
                tris[ti + 2] = i == segments - 1 ? 1 : vi + 1;
            }

            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uv;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
