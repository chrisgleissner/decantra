/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System.Collections.Generic;
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
        private float _previousZRotation;
        private float _previousAngularVelocityRad;
        private float _currentSurfaceTiltDeg;
        private bool _hasPreviousRotationSample;
        private bool _initialised;

        // 3D drag rotation — applied to _worldRoot; does not affect canvas layout/gameplay
        private float _targetDragYaw;
        private float _currentDragYaw;
        private float _targetDragRoll;
        private float _currentDragRoll;

        private const float AngularVelocityImpulseScale = 0.08f;
        private const float AngularAccelerationImpulseScale = 0.02f;

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

            // Build layer data from current bottle state
            FillHeightMapper.Build(bottle, _layers, colorResolver);

            // Rebuild liquid layer GameObjects if slot count changed
            EnsureLayerObjects(_layers.Count, bottle.Capacity);

            // Apply per-layer shader properties
            ApplyLayerProperties(_layers, bottle);
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
        /// Apply a 3D yaw (Y-axis) and optional roll to the world-root mesh during drag.
        /// Provides visible parallax / specular-shift cue without affecting canvas layout.
        /// Smoothly lerps back to zero when <see cref="ClearDragRotation"/> is called.
        /// </summary>
        public void SetDragRotation(float yawDeg, float rollDeg = 0f)
        {
            _targetDragYaw  = Mathf.Clamp(yawDeg,  -15f, 15f);
            _targetDragRoll = Mathf.Clamp(rollDeg, -10f, 10f);
        }

        /// <summary>Clear drag rotation; world root smoothly returns to neutral.</summary>
        public void ClearDragRotation()
        {
            _targetDragYaw  = 0f;
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

        private void Start()
        {
            // WirePourStream is deferred to Start because pourStream is set via reflection
            // by SceneBootstrap AFTER AddComponent (and therefore after Awake runs).
            WirePourStreamToWorldRoot();
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
            _currentDragYaw  = Mathf.Lerp(_currentDragYaw,  _targetDragYaw,  lerpRate);
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
            CleanupLayerObjects();
            if (_contactShadowGO != null)
            {
                var mf = _contactShadowGO.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                    Destroy(mf.sharedMesh);
            }

            // Destroy the scene-root world object; this also destroys GlassBody,
            // LiquidLayers, ContactShadow, and PourStream children.
            if (_worldRoot != null)
            {
                Destroy(_worldRoot);
                _worldRoot = null;
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
            _worldRoot.transform.localScale = Vector3.one;
            _worldRoot.layer = targetLayer;

            // ── Glass body ──────────────────────────────────────────────────────
            _glassBodyGO = new GameObject("GlassBody");
            _glassBodyGO.transform.SetParent(_worldRoot.transform, false);
            _glassBodyGO.layer = targetLayer;
            var meshFilter = _glassBodyGO.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = BottleMeshGenerator.GenerateBottleMesh();
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
                mf.sharedMesh = BottleMeshGenerator.GenerateLiquidLayerMesh(0f, 0f);
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
                        mf.sharedMesh = BottleMeshGenerator.GenerateLiquidLayerMesh(layer.FillMin, layer.FillMax);
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
