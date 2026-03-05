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
    ///   ├── ContactShadow      MeshRenderer  Unlit/Color
    ///   └── PourStream_N       PourStreamController  (reparented from Canvas in Start)
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

        [Tooltip("Reference to the pour stream / bubble controller for this bottle.")]
        [SerializeField] private PourStreamController pourStream;

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
        private GameObject _liquidRoot;
        private GameObject _contactShadowGO;

        private Bottle _lastBottle;
        private int _levelMaxCapacity = 4;
        private float _capacityRatio = 1f;
        private float _previousZRotation;
        private float _previousAngularVelocityRad;
        private float _currentSurfaceTiltDeg;
        private bool _hasPreviousRotationSample;
        private bool _initialised;
        private bool _isSinkOnly;

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
        /// Global uniform scale applied to the 3D world root so that the tallest bottle
        /// never overlaps an adjacent bottle or intrudes into HUD space.  An 8% reduction
        /// eliminates the level-20 and level-36 layout violations while preserving all
        /// relative capacity-ratio height differences.
        /// </summary>
        private const float VisualScale = 0.92f;

        // Glass body property IDs
        private static readonly int PropSinkOnly = Shader.PropertyToID("_SinkOnly");
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
            float maxMeshHeight = (BottleMeshGenerator.BodyHeight
                                   + BottleMeshGenerator.DomeRadius
                                   + BottleMeshGenerator.ShoulderHeight
                                   + BottleMeshGenerator.NeckHeight
                                   + BottleMeshGenerator.RimLipHeight) * VisualScale;
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
            EnsureLayerObjects(_layers.Count, bottle.Capacity);

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
            if (pourStream != null)
                pourStream.BeginPour(target != null ? target.WorldRootTransform : null, normalizedPourRate);

            _wobble.ApplyImpulse(normalizedPourRate * 2f);
        }

        /// <summary>End the pour animation.</summary>
        public void EndPour()
        {
            pourStream?.EndPour();
            _wobble.ApplyImpulse(-0.3f); // gentle counter-slosh when liquid settles
        }

        /// <summary>Reset wobble state (call on level reload).</summary>
        public void ResetWobble()
        {
            _wobble.Reset();
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
            if (_worldRoot != null)
                _worldRoot.SetActive(false);
            s_activeViews.Remove(this);
        }

        private void Start()
        {
            // WirePourStream is deferred to Start because pourStream is set via reflection
            // by SceneBootstrap AFTER AddComponent (and therefore after Awake runs).
            WirePourStreamToWorldRoot();
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
                float wobble = _wobble.Displacement * 0.015f; // tiny UV offset
                float agitation = Mathf.Clamp01(
                    Mathf.Abs(_wobble.Displacement) / Mathf.Max(0.0001f, WobbleSolver.MaxDisplacement));

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
            s_activeViews.Remove(this);
            CleanupLayerObjects();
            if (_contactShadowGO != null)
            {
                var mf = _contactShadowGO.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                    Destroy(mf.sharedMesh);
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

            // Destroy the scene-root world object; this also destroys GlassBody,
            // LiquidLayers, ContactShadow, PourStream, and Stopper children.
            if (_worldRoot != null)
            {
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
            int count = s_activeViews.Count;
            if (count == 0) return;

            // Collect world-space bounds from each active bottle's glass mesh renderer.
            var bounds = new Bounds[count];
            bool allValid = true;
            for (int i = 0; i < count; i++)
            {
                var v = s_activeViews[i];
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

            // Compute per-level sink and topper counts from current active views.
            int sinkBottleCount = 0;
            int topperCount = 0;
            for (int i = 0; i < count; i++)
            {
                var v = s_activeViews[i];
                if (v != null)
                {
                    if (v._isSinkOnly) sinkBottleCount++;
                    if (v._wasCompleted) topperCount++;
                }
            }

            // Write v2 layout report to device persistent data path.
            // This file is pulled by capture_screenshots.sh for verification.
            WriteLayoutReport(count, overlapDetected, hudIntrusionDetected, topperCount, sinkBottleCount);
        }

        /// <summary>
        /// Write the v2 layout report JSON to the device persistent data path so it can
        /// be pulled by the screenshot capture script.
        /// </summary>
        private static void WriteLayoutReport(int activeBottleCount, bool overlapDetected, bool hudIntrusionDetected, int topperCount, int sinkBottleCount)
        {
            try
            {
                string overlapStr = overlapDetected ? "true" : "false";
                string hudStr = hudIntrusionDetected ? "true" : "false";
                // corkCount == completedBottleCount is the key invariant checked by
                // the automated convergence loop.  topperCount kept for back-compat.
                string json = "{\n" +
                              $"  \"overlapDetected\": {overlapStr},\n" +
                              $"  \"hudIntrusionDetected\": {hudStr},\n" +
                              $"  \"completedBottleCount\": {topperCount},\n" +
                              $"  \"corkCount\": {topperCount},\n" +
                              $"  \"topperCount\": {topperCount},\n" +
                              $"  \"sinkBottleCount\": {sinkBottleCount},\n" +
                              $"  \"activeBottleCount\": {activeBottleCount},\n" +
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
                          $"overlap={overlapDetected} hud={hudIntrusionDetected} " +
                          $"completedBottleCount={topperCount} corkCount={topperCount} sinks={sinkBottleCount}");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[Bottle3DView] Failed to write layout report: {ex.Message}");
            }
        }

        // ── Internals ─────────────────────────────────────────────────────────

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

            // ── Liquid layer parent ─────────────────────────────────────────────
            _liquidRoot = new GameObject("LiquidLayers");
            _liquidRoot.transform.SetParent(_worldRoot.transform, false);
            _liquidRoot.layer = targetLayer;
            _liquidRoot.transform.localPosition = new Vector3(0f, 0f, -0.008f);

            // ── Contact shadow ──────────────────────────────────────────────────
            _contactShadowGO = new GameObject("ContactShadow");
            _contactShadowGO.transform.SetParent(_worldRoot.transform, false);
            _contactShadowGO.layer = targetLayer;
            _contactShadowGO.transform.localPosition = new Vector3(0f, BottleMeshGenerator.InteriorBottomY - 0.19f, 0.06f);
            _contactShadowGO.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            _contactShadowGO.transform.localScale = new Vector3(0.38f, 0.38f, 1f);
            var shadowFilter = _contactShadowGO.AddComponent<MeshFilter>();
            shadowFilter.sharedMesh = CreateShadowDiskMesh();
            var shadowRenderer = _contactShadowGO.AddComponent<MeshRenderer>();
            shadowRenderer.sharedMaterial = CreateFallbackShadowMaterial();

            // ── Bottle stopper / cork ────────────────────────────────────────────
            // Created hidden — shown only when the bottle becomes completed (IsSolvedBottle).
            // This removes the "floating indicator" that appeared on every bottle.
            // UpdateTopper shows/hides and tints the cork to match the liquid colour.
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
            // Hidden by default — only completed bottles get a visible cork.
            _stopperGO.SetActive(false);

            // ── Interaction collider ────────────────────────────────────────────
            // Kept on the canvas element (this.gameObject) so that existing UI
            // raycasting (blocksRaycasts = true) continues to work for drag/drop input.
            EnsureInteractionCollider();

            // ── Pour stream ─────────────────────────────────────────────────────
            // If already set (unusual — normally null at Awake time because SceneBootstrap
            // wires it after AddComponent), move it now; otherwise deferred to Start().
            if (pourStream != null)
            {
                WirePourStreamToWorldRoot();
            }

            _initialised = true;
        }

        /// <summary>
        /// Move the pour stream controller to (or confirm it is already under) _worldRoot
        /// and position it at the bottle neck in world space.
        /// Safe to call multiple times — idempotent.
        /// </summary>
        private void WirePourStreamToWorldRoot()
        {
            if (pourStream == null || _worldRoot == null) return;

            // Reparent only if currently under a canvas (RectTransform) parent.
            var currentParent = pourStream.transform.parent;
            bool underCanvas = currentParent != null
                               && currentParent.GetComponent<UnityEngine.RectTransform>() != null;
            if (underCanvas || currentParent == null)
            {
                pourStream.transform.SetParent(_worldRoot.transform, false);
            }

            // Set neck position in world space (local to _worldRoot = world units).
            pourStream.transform.localPosition = new Vector3(
                0f,
                BottleMeshGenerator.BodyHeight * 0.5f
                    + BottleMeshGenerator.ShoulderHeight
                    + BottleMeshGenerator.NeckHeight * 0.88f,
                0f);

            // Ensure it renders on the same layer as the rest of the 3D bottle.
            SetLayerRecursively(pourStream.gameObject, _worldRoot.layer);
        }

        /// <summary>Sync the scene-root world node to this transform's world XY each frame.</summary>
        private void SyncWorldRootPosition()
        {
            if (_worldRoot == null) return;
            var p = transform.position;
            _worldRoot.transform.position = new Vector3(p.x, p.y, 0f);
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
                (BottleMeshGenerator.InteriorBottomY + BottleMeshGenerator.InteriorTopY) * 0.5f,
                0f);
            box.size = new Vector3(
                BottleMeshGenerator.BodyRadius * 2.15f,
                BottleMeshGenerator.BodyHeight + BottleMeshGenerator.ShoulderHeight + BottleMeshGenerator.NeckHeight + BottleMeshGenerator.DomeRadius,
                BottleMeshGenerator.BodyRadius * 1.15f);
        }

        private void EnsureLayerObjects(int requiredCount, int bottleCapacity)
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
                var mf = go.AddComponent<MeshFilter>();
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = liquidMaterialTemplate != null
                    ? liquidMaterialTemplate
                    : CreateFallbackLiquidMaterial();
                _layerRenderers.Add(mr);
                _layerBlocks.Add(new MaterialPropertyBlock());
                _cachedFillBounds.Add((-1f, -1f)); // sentinel: force mesh build on first render

                // Placeholder mesh (will be replaced in ApplyLayerProperties)
                mf.sharedMesh = BottleMeshGenerator.GenerateLiquidLayerMesh(0f, 0f, _capacityRatio);
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
            float totalFill = FillHeightMapper.TotalFill(bottle);

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
                block.SetColor(PropLayerColor[0], new Color(layer.R, layer.G, layer.B, 1f));
                block.SetFloat(PropLayerMin[0], 0f);        // mesh UV.y already spans [fillMin..fillMax]
                block.SetFloat(PropLayerMax[0], 1f);        // full mesh = this layer

                // Clear upper layers
                for (int k = 1; k < 9; k++)
                {
                    block.SetFloat(PropLayerMin[k], 0f);
                    block.SetFloat(PropLayerMax[k], 0f);
                }

                block.SetFloat(PropTotalFill, 1f);           // mesh is already bounded
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
                }
            }
            _layerRenderers.Clear();
            _layerBlocks.Clear();
            _cachedFillBounds.Clear();
        }

        /// <summary>
        /// Block E fix: push the <c>_SinkOnly</c> flag to the glass body MaterialPropertyBlock
        /// so that the BottleGlass shader renders dark rim + base-line bands for sink bottles.
        /// Uses a MaterialPropertyBlock (not material instance) to avoid material draw-call break.
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

            // Sync sink-only flag on the glass renderer
            if (_glassBodyGO != null)
            {
                var mr = _glassBodyGO.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    var block = new MaterialPropertyBlock();
                    mr.GetPropertyBlock(block);
                    block.SetFloat(PropSinkOnly, _isSinkOnly ? 1f : 0f);
                    mr.SetPropertyBlock(block);
                }
            }
        }

        // ── Obj-3: Completed bottle cork stopper ───────────────────────────────

        /// <summary>
        /// Show the physical cork stopper only when <paramref name="bottle"/> is fully
        /// solved: completely full + monochrome + not a sink.
        /// Hides the stopper for all other states, eliminating the placeholder
        /// "floating indicator" visible on every bottle in earlier builds.
        ///
        /// Completion condition: <see cref="Bottle.IsSolvedBottle"/> — requires IsFull
        /// so a partially-filled monochrome bottle does NOT show a cork.
        /// </summary>
        private void UpdateTopper(Bottle bottle, List<LiquidLayerData> layers)
        {
            // IsSolvedBottle() requires IsEmpty==false, IsFull, and single-colour slots.
            bool isCompleted = !bottle.IsSink && bottle.IsSolvedBottle();
            if (isCompleted == _wasCompleted) return; // state unchanged — nothing to do
            _wasCompleted = isCompleted;

            // Show or hide the cork GameObject.
            if (_stopperGO != null)
                _stopperGO.SetActive(isCompleted);

            // When showing: tint the cork to match the liquid colour exactly (spec requirement).
            if (isCompleted && _stopperRenderer != null && layers.Count > 0)
            {
                var liquidColor = new Color(layers[0].R, layers[0].G, layers[0].B, 1f);
                var block = new MaterialPropertyBlock();
                _stopperRenderer.GetPropertyBlock(block);
                block.SetColor(PropStopperColor, liquidColor);
                _stopperRenderer.SetPropertyBlock(block);
            }
        }

        /// <summary>
        /// Short cylinder mesh for the cork stopper: fits snugly in the bottle neck
        /// and peeks <see cref="BottleMeshGenerator.StopperPeekHeight"/> above the rim.
        /// Geometry: cylindrical side walls + flat top disk facing the camera (-Z).
        /// </summary>
        private static Mesh CreateStopperMesh()
        {
            const int segments = 28;
            float radius = BottleMeshGenerator.StopperRadius;
            float height = BottleMeshGenerator.StopperTotalHeight;

            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            // ── Cylindrical side wall (two rings) ────────────────────────────
            int stride = segments + 1;
            for (int ring = 0; ring <= 1; ring++)
            {
                float y = ring == 0 ? 0f : height;
                for (int i = 0; i <= segments; i++)
                {
                    float angle = (float)i / segments * Mathf.PI * 2f;
                    float cos = Mathf.Cos(angle);
                    float sin = Mathf.Sin(angle);
                    verts.Add(new Vector3(cos * radius, y, sin * radius));
                    norms.Add(new Vector3(cos, 0f, sin)); // outward radial normal
                    uvs.Add(new Vector2((float)i / segments, (float)ring));
                }
            }
            for (int i = 0; i < segments; i++)
            {
                int b0 = i, b1 = i + 1;
                int t0 = stride + i, t1 = stride + i + 1;
                // CCW winding = outward normals face away from camera → visible from outside
                tris.Add(b0); tris.Add(t0); tris.Add(b1);
                tris.Add(b1); tris.Add(t0); tris.Add(t1);
            }

            // ── Flat top cap (facing -Z so the camera-facing face is lit) ────
            int capBase = verts.Count;
            verts.Add(new Vector3(0f, height, 0f));
            norms.Add(Vector3.back);
            uvs.Add(new Vector2(0.5f, 0.5f));
            for (int i = 0; i <= segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);
                verts.Add(new Vector3(cos * radius, height, sin * radius));
                norms.Add(Vector3.back);
                uvs.Add(new Vector2(cos * 0.5f + 0.5f, sin * 0.5f + 0.5f));
            }
            for (int i = 0; i < segments; i++)
            {
                int c = capBase;
                int a = capBase + 1 + i;
                int b = capBase + 1 + (i + 1 <= segments ? i + 1 : 0);
                tris.Add(c); tris.Add(b); tris.Add(a); // CCW = front-face toward -Z
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
        /// Flat-colour material for the cork stopper.
        /// Uses Sprites/Default (always present in Unity, works on all platforms including
        /// Android with IL2CPP) so the stopper reliably shows the liquid colour.
        /// Rendered at Transparent+2 (queue 3002) so it composites over the transparent
        /// glass shell (queue 3001) and appears in front of the neck.
        /// </summary>
        private static Material CreateStopperMaterial(Color color)
        {
            // Sprites/Default is always compiled and available; it uses _Color as the tint.
            // Fall back to Unlit/Color if, for some reason, Sprites/Default is stripped.
            var shader = Shader.Find("Sprites/Default")
                      ?? Shader.Find("Unlit/Color")
                      ?? Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                Debug.LogWarning("Bottle3DView: cannot resolve stopper shader.");
                return null;
            }

            var mat = new Material(shader) { name = "BottleStopper_Mat" };
            // Sprites/Default uses _Color; Unlit/Color also accepts _Color.
            mat.SetColor("_Color", color);
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
            int count = s_activeViews.Count;
            var bounds = new Bounds[count];
            bool overlapDet = false;
            bool hudDet = false;
            int sinks = 0;
            int toppers = 0;
            const float HudBoundaryY = 4.35f;

            for (int i = 0; i < count; i++)
            {
                var v = s_activeViews[i];
                if (v == null) continue;
                if (v._isSinkOnly) sinks++;
                if (v._wasCompleted) toppers++;
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

            WriteLayoutReport(count, overlapDet, hudDet, toppers, sinks);
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
            material.color = new Color(0f, 0f, 0f, 0.12f);
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
