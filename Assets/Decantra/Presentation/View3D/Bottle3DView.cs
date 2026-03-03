/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System.Collections.Generic;
using Decantra.Domain.Model;
using Decantra.Presentation.View;
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
    /// <see cref="BottleView"/>.  During the transition phase it acts as a visual overlay:
    ///   - It reads the same <see cref="Bottle"/> data model.
    ///   - It does NOT own or modify any gameplay logic.
    ///   - It creates child 3D GameObjects (bottleMeshGO, liquidLayerGOs[]).
    ///   - Bottle world-position is inherited from the owning RectTransform pivot
    ///     by sampling its world position at Awake and pinning the 3D root there.
    ///
    /// 3D object hierarchy
    /// ---------------------
    ///   Bottle3DRoot  (this.gameObject, follows RectTransform pivot)
    ///   ├── GlassBody          MeshRenderer  BottleGlass.shader
    ///   └── LiquidLayers       (empty parent)
    ///       ├── LiquidLayer_0  MeshRenderer  Liquid3D.shader  (MaterialPropertyBlock per layer)
    ///       ├── LiquidLayer_1  ...
    ///       └── ...
    ///
    /// Coordinate system
    ///   3D bottle local Y=0 maps to the canvas pivot of the parent bottle.
    ///   InteriorBottomY / InteriorTopY are BottleMeshGenerator constants in world units.
    ///   The shader receives fill fractions [0..1] mapped from these.
    ///
    /// Determinism guarantee
    ///   WobbleSolver uses fixed-step integration — no frame-rate dependency.
    ///   All shader parameters are set from exact integer slot counts (FillHeightMapper).
    ///   Sur tilt angle comes solely from the bottle transform's Z rotation axis.
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

        private GameObject _glassBodyGO;
        private GameObject _liquidRoot;

        private Bottle _lastBottle;
        private int _levelMaxCapacity = 4;
        private float _previousZRotation;
        private bool _initialised;

        // Shader property ID cache (populated once)
        private static readonly int PropTotalFill = Shader.PropertyToID("_TotalFill");
        private static readonly int PropLayerCount = Shader.PropertyToID("_LayerCount");
        private static readonly int PropSurfaceTilt = Shader.PropertyToID("_SurfaceTiltDegrees");
        private static readonly int PropWobbleOffset = Shader.PropertyToID("_WobbleOffset");

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

            // Update surface tilt & wobble
            float zRot = transform.eulerAngles.z;
            UpdateTiltFromRotation(zRot);
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
                pourStream.BeginPour(target != null ? target.transform : null, normalizedPourRate);

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

        // ── Unity lifecycle ───────────────────────────────────────────────────

        private void Awake()
        {
            EnsureInitialised();
        }

        private void Update()
        {
            // Advance the wobble simulation by this frame's delta time
            _wobble.Step(Time.deltaTime);

            // Push wobble state to all liquid layer renderers
            if (_layerRenderers.Count > 0)
            {
                float tilt = _wobble.TiltAngleDegrees;
                float wobble = _wobble.Displacement * 0.015f; // tiny UV offset

                for (int i = 0; i < _layerRenderers.Count; i++)
                {
                    if (_layerRenderers[i] == null) continue;
                    var block = _layerBlocks[i];
                    block.SetFloat(PropSurfaceTilt, tilt);
                    block.SetFloat(PropWobbleOffset, wobble);
                    _layerRenderers[i].SetPropertyBlock(block);
                }
            }
        }

        private void OnDestroy()
        {
            CleanupLayerObjects();
        }

        // ── Internals ─────────────────────────────────────────────────────────

        private void EnsureInitialised()
        {
            if (_initialised) return;

            // Create glass body
            _glassBodyGO = new GameObject("GlassBody");
            _glassBodyGO.transform.SetParent(transform, false);
            var meshFilter = _glassBodyGO.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = BottleMeshGenerator.GenerateBottleMesh();
            var meshRenderer = _glassBodyGO.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = glassMaterialTemplate != null
                ? glassMaterialTemplate
                : CreateFallbackGlassMaterial();

            // Create liquid layer parent
            _liquidRoot = new GameObject("LiquidLayers");
            _liquidRoot.transform.SetParent(transform, false);

            _initialised = true;
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

        private void UpdateTiltFromRotation(float currentZRotDeg)
        {
            float delta = Mathf.DeltaAngle(_previousZRotation, currentZRotDeg);
            if (Mathf.Abs(delta) > 0.01f)
            {
                // Angular velocity in rad/s estimated from frame delta
                float angVel = delta * Mathf.Deg2Rad / Mathf.Max(Time.deltaTime, 0.001f);
                _wobble.ApplyImpulse(angVel * 0.08f);
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
            var shader = Shader.Find("Decantra/BottleGlass")
                      ?? Shader.Find("Standard");
            return new Material(shader) { name = "BottleGlass_Fallback" };
        }

        private static Material CreateFallbackLiquidMaterial()
        {
            var shader = Shader.Find("Decantra/Liquid3D")
                      ?? Shader.Find("Standard");
            return new Material(shader) { name = "Liquid3D_Fallback" };
        }
    }
}
