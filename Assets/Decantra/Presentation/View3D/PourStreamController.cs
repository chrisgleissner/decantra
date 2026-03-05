/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using UnityEngine;

namespace Decantra.Presentation.View3D
{
    /// <summary>
    /// Manages the pour-stream mesh and bubble particle system during a pour animation.
    ///
    /// Visual approach (no physics engine)
    /// -------------------------------------
    /// A. Pour stream: a dynamically-sized quad mesh that curves from the source
    ///    bottle neck to the target bottle opening, simulating a liquid arc.
    ///    The stream width tapers from NeckRadius at source to a thin line at the midpoint
    ///    then widens again as it splashes into the target.
    ///
    /// B. Bubbles: a bounded <see cref="ParticleSystem"/> that emits particles only
    ///    during active pouring.  Emission rate is proportional to pour velocity.
    ///    Each particle has a fixed maximum lifetime so counts never grow unboundedly.
    ///
    /// Determinism guarantee
    /// ----------------------
    /// • The particle system uses a fixed Random Seed (no auto-random seed) so the
    ///   visual sequence is reproducible for a given level and pour order.
    ///   The seed is derived from the source bottle index to be unique per bottle
    ///   but fully deterministic for a given game state.
    /// • The stream mesh geometry is computed analytically from known positions.
    /// • No Rigidbody, no PhysX simulation.
    ///
    /// Lifecycle
    /// ----------
    ///   BeginPour(targetTransform, rate)  →  activates stream + bubbles
    ///   EndPour()                         →  deactivates stream, stops new emissions
    ///                                        (existing particles drain naturally)
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class PourStreamController : MonoBehaviour
    {
        // ── Configuration ─────────────────────────────────────────────────────
        [SerializeField] private int sourceBottleIndex = 0;
        [SerializeField] private float streamWidthMax = 0.06f;
        [SerializeField] private float streamWidthMin = 0.015f;
        [SerializeField] private int streamSegments = 12;
        [SerializeField] private float gravityScale = 5f;
        [SerializeField] private float maxBubblesPerSec = 40f;
        [SerializeField] private float bubbleLifetime = 0.45f;
        [SerializeField] private float bubbleSpeed = 0.4f;
        [SerializeField] private float bubbleSize = 0.03f;
        [SerializeField] private Color bubbleColor = new Color(1f, 1f, 1f, 0.45f);
        [SerializeField] private Color streamColor = new Color(0.92f, 0.97f, 1f, 0.6f);

        // ── Internal state ────────────────────────────────────────────────────
        private MeshFilter _streamMeshFilter;
        private MeshRenderer _streamRenderer;
        private ParticleSystem _bubbleSystem;
        private ParticleSystem.EmissionModule _emission;
        private bool _pouring;
        private float _pourRate;
        private Transform _targetTransform;
        private Mesh _streamMesh;
        private Material _streamMaterial;

        // ── Unity lifecycle ───────────────────────────────────────────────────

        private void Awake()
        {
            _streamMeshFilter = GetComponent<MeshFilter>();
            _streamRenderer = GetComponent<MeshRenderer>();

            _streamMesh = new Mesh { name = "PourStream" };
            _streamMeshFilter.sharedMesh = _streamMesh;
            _streamMaterial = CreateStreamMaterial();
            _streamRenderer.sharedMaterial = _streamMaterial;

            SetupParticleSystem();

            SetActive(false);
        }

        private void OnDestroy()
        {
            if (_streamMesh != null)
                Destroy(_streamMesh);
            if (_streamMaterial != null)
                Destroy(_streamMaterial);
        }

        private void LateUpdate()
        {
            if (!_pouring) return;
            if (_targetTransform == null)
            {
                EndPour();
                return;
            }

            RebuildStreamMesh();
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Activate the pour-stream visual flowing toward <paramref name="target"/>.
        /// <paramref name="normalizedRate"/> ∈ [0..1]: controls stream width and bubble rate.
        /// </summary>
        public void BeginPour(Transform target, float normalizedRate)
        {
            _targetTransform = target;
            _pourRate = Mathf.Clamp01(normalizedRate);
            _pouring = true;

            // Deterministic bubble seed derived from bottle index
            _bubbleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var mainModule = _bubbleSystem.main;
            _bubbleSystem.useAutoRandomSeed = false;
            _bubbleSystem.randomSeed = (uint)(sourceBottleIndex * 7919 + 1);
            mainModule.useUnscaledTime = false;
            mainModule.loop = true;

            _emission.rateOverTime = maxBubblesPerSec * _pourRate;

            if (_streamMaterial != null)
            {
                float alpha = Mathf.Lerp(0.35f, streamColor.a, _pourRate);
                _streamMaterial.color = new Color(streamColor.r, streamColor.g, streamColor.b, alpha);
            }

            SetActive(true);
            _bubbleSystem.Play();
        }

        /// <summary>
        /// Deactivate the pour stream. Existing particles drain; new emissions stop.
        /// </summary>
        public void EndPour()
        {
            _pouring = false;
            _targetTransform = null;

            _emission.rateOverTime = 0f;
            _bubbleSystem.Stop(withChildren: false,
                               stopBehavior: ParticleSystemStopBehavior.StopEmitting);

            // Clear mesh
            _streamMesh.Clear();
            _streamRenderer.enabled = false;
        }

        // ── Internals ─────────────────────────────────────────────────────────

        private void SetupParticleSystem()
        {
            var psGO = new GameObject("BubbleParticles");
            psGO.transform.SetParent(transform, false);
            _bubbleSystem = psGO.AddComponent<ParticleSystem>();

            var main = _bubbleSystem.main;
            main.loop = true;
            main.startLifetime = bubbleLifetime;
            main.startSpeed = bubbleSpeed;
            main.startSize = bubbleSize;
            main.startColor = bubbleColor;
            main.maxParticles = Mathf.CeilToInt(maxBubblesPerSec * bubbleLifetime * 1.2f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = -0.2f; // slight rise for bubbles

            _emission = _bubbleSystem.emission;
            _emission.enabled = true;
            _emission.rateOverTime = 0f;

            var shape = _bubbleSystem.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.04f;

            // Renderer setup: use default sprite
            var psr = psGO.GetComponent<ParticleSystemRenderer>();
            psr.renderMode = ParticleSystemRenderMode.Billboard;
            psr.sharedMaterial = CreateBubbleMaterial();

            _bubbleSystem.Stop(withChildren: true,
                               stopBehavior: ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private Material CreateBubbleMaterial()
        {
            var bubbleShader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Standard");
            return new Material(bubbleShader) { name = "BubbleMaterial" };
        }

        private Material CreateStreamMaterial()
        {
            var streamShader = Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default") ?? Shader.Find("Standard");
            var material = new Material(streamShader) { name = "PourStreamMaterial" };
            material.color = streamColor;
            return material;
        }

        private void SetActive(bool active)
        {
            _streamRenderer.enabled = active;
            if (_bubbleSystem != null)
                _bubbleSystem.gameObject.SetActive(active);
        }

        /// <summary>
        /// Analytically build a tapered quad-strip mesh representing the pour arc.
        ///
        /// The arc is approximated with a Bezier quadratic:
        ///   P0 = source neck exit (world)
        ///   P1 = midpoint dropped by gravity
        ///   P2 = target bottle opening (world)
        ///
        /// Each segment gets a perpendicular width quad.
        /// Width tapers from streamWidthMax at source to streamWidthMin at lowest point
        /// then back to streamWidthMin * 0.5 at target.
        /// </summary>
        private void RebuildStreamMesh()
        {
            if (_streamMesh == null) return;

            Vector3 p0 = transform.position; // source neck exit
            Vector3 p2 = _targetTransform.position;

            // Bezier control point: midpoint dropped by parabolic gravity
            float dx = p2.x - p0.x;
            float dy = p2.y - p0.y;
            float t0 = 0.5f;
            float dropY = -0.5f * gravityScale * t0 * t0;
            Vector3 p1 = new Vector3(
                p0.x + dx * 0.5f,
                p0.y + dy * 0.5f + dropY,
                p0.z + (p2.z - p0.z) * 0.5f);

            int n = streamSegments;
            var verts = new Vector3[n * 2 + 2];
            var uvs = new Vector2[n * 2 + 2];
            var tris = new int[n * 6];
            var norms = new Vector3[n * 2 + 2];

            Vector3 camFwd = Camera.main != null
                ? Camera.main.transform.forward
                : Vector3.forward;

            for (int i = 0; i <= n; i++)
            {
                float t = (float)i / n;

                // Quadratic Bezier
                Vector3 pos = (1 - t) * (1 - t) * p0
                            + 2 * (1 - t) * t * p1
                            + t * t * p2;

                // Tangent (derivative of Bezier)
                Vector3 tangent = 2 * (1 - t) * (p1 - p0) + 2 * t * (p2 - p1);
                Vector3 right = Vector3.Cross(tangent.normalized, camFwd).normalized;

                // Width taper: max at source, narrows to min near target
                float widthT = 1f - t * (1f - streamWidthMin / streamWidthMax);
                float halfW = streamWidthMax * widthT * _pourRate * 0.5f;

                int vi = i * 2;
                verts[vi] = pos - right * halfW;
                verts[vi + 1] = pos + right * halfW;
                uvs[vi] = new Vector2(0f, t);
                uvs[vi + 1] = new Vector2(1f, t);
                norms[vi] = -camFwd;
                norms[vi + 1] = -camFwd;
            }

            for (int i = 0; i < n; i++)
            {
                int vi = i * 2;
                int ti = i * 6;
                tris[ti] = vi;
                tris[ti + 1] = vi + 2;
                tris[ti + 2] = vi + 1;
                tris[ti + 3] = vi + 1;
                tris[ti + 4] = vi + 2;
                tris[ti + 5] = vi + 3;
            }

            _streamMesh.Clear();
            _streamMesh.SetVertices(verts);
            _streamMesh.SetNormals(norms);
            _streamMesh.SetUVs(0, uvs);
            _streamMesh.SetTriangles(tris, 0);
            _streamMesh.RecalculateBounds();
            _streamRenderer.enabled = true;
        }
    }
}
