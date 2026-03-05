/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System.Collections.Generic;
using UnityEngine;

namespace Decantra.Presentation.View3D
{
    /// <summary>
    /// Procedural bottle mesh generator.
    ///
    /// Bottle anatomy (Y axis, local space, origin at bottle centre)
    /// ---------------------------------------------------------------
    ///   BaseDomeY     = -BodyHalfHeight         (bottom of hemispherical dome)
    ///   BodyBottomY   = -BodyHalfHeight + DomeRadius
    ///   BodyTopY      = +BodyHalfHeight
    ///   ShoulderTopY  = BodyTopY + ShoulderHeight
    ///   NeckBottomY   = ShoulderTopY
    ///   NeckTopY      = NeckBottomY + NeckHeight
    ///
    /// The mesh is built from:
    ///   1. Hemispherical base dome (bottom)
    ///   2. Cylindrical body
    ///   3. Tapered shoulder (lerp radius: BodyRadius → NeckRadius)
    ///   4. Cylindrical neck
    ///   5. Flat neck cap (for the stopper / sealed top)
    ///
    /// UV mapping: U = azimuth / 2π, V = normalised world Y in [0..1].
    /// Normals are analytically correct per-section for smooth shading.
    ///
    /// Thread safety: NOT thread-safe. Use only from the Unity main thread.
    /// </summary>
    public static class BottleMeshGenerator
    {
        // ── Geometry parameters (canvas-unit scale matching 2D system) ────────
        /// <summary>World-space height of the cylindrical body section.</summary>
        public const float BodyHeight = 1.6f;

        /// <summary>World-space radius of the cylindrical body.</summary>
        public const float BodyRadius = 0.38f;

        /// <summary>World-space height of the tapered shoulder section.</summary>
        public const float ShoulderHeight = 0.30f;

        /// <summary>World-space radius of the cylindrical neck.</summary>
        public const float NeckRadius = 0.14f;

        /// <summary>World-space height of the cylindrical neck.</summary>
        public const float NeckHeight = 0.22f;

        /// <summary>Hemisphere radius for the base dome.</summary>
        public const float DomeRadius = BodyRadius;

        /// <summary>Number of azimuthal segments (longitude). Higher = smoother cylinder.</summary>
        public const int Segments = 40;

        /// <summary>Number of latitude segments for the base dome.</summary>
        public const int DomeLatitudes = 12;

        /// <summary>Number of vertical segments for the shoulder taper.</summary>
        public const int ShoulderSteps = 6;

        /// <summary>Nominal wall thickness for the glass shell.</summary>
        public const float GlassThickness = 0.028f;

        /// <summary>Small outer overhang at the rim to form a visible lip.</summary>
        public const float RimLipOverhang = 0.018f;

        /// <summary>Rim lip height.</summary>
        public const float RimLipHeight = 0.035f;

        // ── Stopper / cork geometry ───────────────────────────────────────────
        /// <summary>
        /// Radius of the cylindrical cork/stopper that sits in and peeks above the neck.
        /// Slightly smaller than the inner neck wall so the stopper fits snugly.
        /// </summary>
        public const float StopperRadius = NeckRadius - GlassThickness * 0.65f;  // ≈ 0.1218

        /// <summary>
        /// How far below the top of the neck lip the stopper extends (depth inside neck).
        /// </summary>
        public const float StopperInsideDepth = 0.030f;

        /// <summary>
        /// How far the stopper peeks above the outer rim top.
        /// </summary>
        public const float StopperPeekHeight = 0.028f;

        /// <summary>
        /// Total height of the stopper cylinder mesh (inside portion + peek portion).
        /// </summary>
        public const float StopperTotalHeight = StopperInsideDepth + StopperPeekHeight;

        // Computed Y position of the neck top (bottom of rim lip), referenced by Bottle3DView.
        // Formula: BodyHalfHeight + ShoulderHeight + NeckHeight  (same as in GenerateBottleMesh).
        // Exposed as a field so Bottle3DView can position the stopper GO without re-deriving.
        public static readonly float StopperBaseY =
            BodyHeight * 0.5f + ShoulderHeight + NeckHeight - StopperInsideDepth;

        private const float BodyHalfHeight = BodyHeight * 0.5f;

        /// <summary>
        /// Y position of the interior bottom of the liquid region (bottom of body cylinder).
        /// Used by Bottle3DView to anchor liquid fill heights in local space.
        /// </summary>
        public static readonly float InteriorBottomY = -BodyHalfHeight + DomeRadius * 0.5f;

        /// <summary>
        /// Y position of the interior top of the liquid region (top of body cylinder).
        /// </summary>
        public static readonly float InteriorTopY = BodyHalfHeight;

        /// <summary>Total interior liquid height in world units.</summary>
        public static float InteriorHeight => InteriorTopY - InteriorBottomY;

        // ── Mesh generation ───────────────────────────────────────────────────

        /// <summary>
        /// Generate and return a new <see cref="Mesh"/> representing the full bottle exterior.
        ///
        /// The generated mesh is a SINGLE closed surface suitable for the BottleGlass shader
        /// (back-face culling on, double-pass for glass transparency). It does NOT include
        /// the liquid interior — that is handled separately by layered quad meshes in Bottle3DView.
        ///
        /// The mesh is marked as non-readable (no CPU copy kept) after upload to the GPU
        /// unless <paramref name="keepReadable"/> is true.
        /// </summary>
        /// <param name="keepReadable">Keep CPU-side mesh data readable (true for tests).</param>
        /// <returns>A new <see cref="Mesh"/> instance. Caller is responsible for lifetime.</returns>
        /// <param name="capacityRatio">Fraction of full body height to generate, in [0.1..1]. Only the
        /// cylindrical body section scales; the dome (bottom), shoulder, neck, and rim stay at their
        /// reference sizes — matching the 2D BottleView body-only stretch philosophy.</param>
        public static Mesh GenerateBottleMesh(float capacityRatio = 1f, bool keepReadable = false)
        {
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            // Only the body cylinder scales; all other sections stay fixed.
            float scaledBodyHeight = BodyHeight * Mathf.Clamp(capacityRatio, 0.1f, 1f);
            float yMin = -BodyHalfHeight;
            float bodyBottom = yMin + DomeRadius * 0.5f;      // dome always same (-0.61)
            float bodyTop = bodyBottom + scaledBodyHeight;  // body end floats up/down
            float totalHeight = DomeRadius + scaledBodyHeight + ShoulderHeight + NeckHeight + RimLipHeight;
            float shoulderTop = bodyTop + ShoulderHeight;
            float neckTop = shoulderTop + NeckHeight;
            float rimTop = neckTop + RimLipHeight;

            // Inner shell dimensions (clamped for robustness)
            float innerBodyRadius = Mathf.Max(BodyRadius - GlassThickness, 0.01f);
            float innerNeckRadius = Mathf.Max(NeckRadius - GlassThickness * 0.9f, 0.01f);
            float innerDomeRadius = Mathf.Max(DomeRadius - GlassThickness, 0.01f);
            float innerBodyBottom = bodyBottom + GlassThickness * 0.55f;
            float innerBodyTop = bodyTop - GlassThickness * 0.25f;
            float innerShoulderTop = shoulderTop - GlassThickness * 0.15f;
            float innerNeckTop = neckTop + RimLipHeight * 0.58f;

            // ── Outer shell ───────────────────────────────────────────────────
            AppendDome(verts, norms, uvs, tris, yMin, totalHeight, DomeRadius, flipY: true, invertWinding: false, invertNormal: false);
            AppendCylinder(verts, norms, uvs, tris,
                           y0: bodyBottom, y1: bodyTop,
                           r0: BodyRadius, r1: BodyRadius,
                           totalHeight: totalHeight,
                           invertWinding: false,
                           invertNormal: false);
            AppendCylinder(verts, norms, uvs, tris,
                           y0: bodyTop, y1: shoulderTop,
                           r0: BodyRadius, r1: NeckRadius,
                           totalHeight: totalHeight,
                           steps: ShoulderSteps,
                           invertWinding: false,
                           invertNormal: false);
            AppendCylinder(verts, norms, uvs, tris,
                           y0: shoulderTop, y1: neckTop,
                           r0: NeckRadius, r1: NeckRadius,
                           totalHeight: totalHeight,
                           invertWinding: false,
                           invertNormal: false);

            // Rim lip profile (slight flare outward)
            float rimMid = neckTop + RimLipHeight * 0.45f;
            float lipRadius = NeckRadius + RimLipOverhang;
            AppendCylinder(verts, norms, uvs, tris,
                           y0: neckTop, y1: rimMid,
                           r0: NeckRadius, r1: lipRadius,
                           totalHeight: totalHeight,
                           steps: 2,
                           invertWinding: false,
                           invertNormal: false);
            AppendCylinder(verts, norms, uvs, tris,
                           y0: rimMid, y1: rimTop,
                           r0: lipRadius, r1: NeckRadius + RimLipOverhang * 0.75f,
                           totalHeight: totalHeight,
                           steps: 2,
                           invertWinding: false,
                           invertNormal: false);

            // ── Inner shell (inward normals, reversed winding) ──────────────
            AppendDome(verts, norms, uvs, tris, yMin + GlassThickness * 0.55f, totalHeight, innerDomeRadius, flipY: true, invertWinding: true, invertNormal: true);
            AppendCylinder(verts, norms, uvs, tris,
                           y0: innerBodyBottom, y1: innerBodyTop,
                           r0: innerBodyRadius, r1: innerBodyRadius,
                           totalHeight: totalHeight,
                           invertWinding: true,
                           invertNormal: true);
            AppendCylinder(verts, norms, uvs, tris,
                           y0: innerBodyTop, y1: innerShoulderTop,
                           r0: innerBodyRadius, r1: innerNeckRadius,
                           totalHeight: totalHeight,
                           steps: ShoulderSteps,
                           invertWinding: true,
                           invertNormal: true);
            AppendCylinder(verts, norms, uvs, tris,
                           y0: innerShoulderTop, y1: innerNeckTop,
                           r0: innerNeckRadius, r1: innerNeckRadius,
                           totalHeight: totalHeight,
                           invertWinding: true,
                           invertNormal: true);

            // ── Top rim bridge: closes wall thickness at mouth ───────────────
            AppendRingBridge(verts, norms, uvs, tris,
                             y: innerNeckTop,
                             outerRadius: NeckRadius + RimLipOverhang * 0.72f,
                             innerRadius: innerNeckRadius,
                             totalHeight: totalHeight,
                             faceUp: true);

            var mesh = new Mesh
            {
                name = "BottleMesh",
                indexFormat = UnityEngine.Rendering.IndexFormat.UInt32
            };
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();

            if (!keepReadable)
                mesh.UploadMeshData(markNoLongerReadable: true);

            return mesh;
        }

        /// <summary>
        /// Generate a flat quad mesh for a single liquid layer used inside the bottle.
        /// The quad spans the full interior width and occupies [fillMin..fillMax] of
        /// the interior height, in local bottle space.
        ///
        /// The Liquid3D shader samples this mesh's UV.y to determine which layer is
        /// rendered at each fragment.
        ///
        /// <paramref name="fillMin"/> and <paramref name="fillMax"/> are [0..1] fractions
        /// of the scaled interior height (<see cref="InteriorHeight"/> × <paramref name="capacityRatio"/>).
        /// </summary>
        public static Mesh GenerateLiquidLayerMesh(float fillMin, float fillMax, float capacityRatio = 1f)
        {
            float scaledInteriorHeight = InteriorHeight * Mathf.Clamp(capacityRatio, 0.1f, 1f);
            float yBottom = InteriorBottomY + fillMin * scaledInteriorHeight;
            float yTop = InteriorBottomY + fillMax * scaledInteriorHeight;

            // Slightly inset X so layer quad sits just inside the glass wall
            const float inset = BodyRadius * 0.92f;

            var verts = new List<Vector3>
            {
                new(-inset, yBottom, 0f),
                new( inset, yBottom, 0f),
                new( inset, yTop,    0f),
                new(-inset, yTop,    0f),
            };
            var norms = new List<Vector3>
            {
                Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward
            };
            var uv = new List<Vector2>
            {
                new(0f, fillMin),
                new(1f, fillMin),
                new(1f, fillMax),
                new(0f, fillMax),
            };
            var tris = new List<int> { 0, 2, 1, 0, 3, 2 };

            var mesh = new Mesh { name = $"LiquidLayer_{fillMin:F2}_{fillMax:F2}_cap{capacityRatio:F2}" };
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            mesh.UploadMeshData(markNoLongerReadable: true);
            return mesh;
        }

        // ── Primitive builders ────────────────────────────────────────────────

        /// <summary>Append a hemisphere, dome-up or dome-down depending on flipY.</summary>
        private static void AppendDome(
            List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs, List<int> tris,
            float yBase, float totalHeight, float domeRadius, bool flipY, bool invertWinding, bool invertNormal)
        {
            int baseIdx = verts.Count;
            float ySign = flipY ? -1f : 1f;
            float domeCenter = flipY ? yBase + domeRadius : yBase - domeRadius;

            for (int lat = 0; lat <= DomeLatitudes; lat++)
            {
                float t = (float)lat / DomeLatitudes;
                float phi = t * Mathf.PI * 0.5f; // 0..90 degrees
                float sinPhi = Mathf.Sin(phi);
                float cosPhi = Mathf.Cos(phi);

                for (int lon = 0; lon <= Segments; lon++)
                {
                    float theta = (float)lon / Segments * Mathf.PI * 2f;
                    float cosTheta = Mathf.Cos(theta);
                    float sinTheta = Mathf.Sin(theta);

                    var pos = new Vector3(
                        domeRadius * sinPhi * cosTheta,
                        domeCenter + ySign * domeRadius * cosPhi,
                        domeRadius * sinPhi * sinTheta);

                    var n = new Vector3(sinPhi * cosTheta, ySign * cosPhi, sinPhi * sinTheta);
                    if (flipY) n.y = -Mathf.Abs(n.y);
                    if (invertNormal) n = -n;

                    float v = (pos.y - (yBase - totalHeight)) / totalHeight;
                    verts.Add(pos);
                    norms.Add(n.normalized);
                    uvs.Add(new Vector2((float)lon / Segments, Mathf.Clamp01(v)));
                }
            }

            // Triangulate dome rings
            int stride = Segments + 1;
            for (int lat = 0; lat < DomeLatitudes; lat++)
            {
                for (int lon = 0; lon < Segments; lon++)
                {
                    int i0 = baseIdx + lat * stride + lon;
                    int i1 = i0 + 1;
                    int i2 = i0 + stride;
                    int i3 = i2 + 1;
                    if (flipY ^ invertWinding)
                    {
                        tris.Add(i0); tris.Add(i2); tris.Add(i1);
                        tris.Add(i1); tris.Add(i2); tris.Add(i3);
                    }
                    else
                    {
                        tris.Add(i0); tris.Add(i1); tris.Add(i2);
                        tris.Add(i1); tris.Add(i3); tris.Add(i2);
                    }
                }
            }
        }

        /// <summary>Append a truncated cylinder (cone if r0 ≠ r1).</summary>
        private static void AppendCylinder(
            List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs, List<int> tris,
            float y0, float y1, float r0, float r1, float totalHeight, bool invertWinding, bool invertNormal, int steps = 1)
        {
            int baseIdx = verts.Count;
            int stride = Segments + 1;

            for (int step = 0; step <= steps; step++)
            {
                float t = (float)step / steps;
                float y = Mathf.Lerp(y0, y1, t);
                float r = Mathf.Lerp(r0, r1, t);
                float vFrac = Mathf.Clamp01((y + BodyHalfHeight + DomeRadius) / totalHeight);

                // Outward normal tilt for taper
                float dr = r1 - r0;
                float dy = y1 - y0;
                float nSlope = (dy > 0f) ? dr / dy : 0f;

                for (int lon = 0; lon <= Segments; lon++)
                {
                    float theta = (float)lon / Segments * Mathf.PI * 2f;
                    float cosT = Mathf.Cos(theta);
                    float sinT = Mathf.Sin(theta);

                    verts.Add(new Vector3(r * cosT, y, r * sinT));
                    var n = new Vector3(cosT, -nSlope, sinT);
                    if (invertNormal) n = -n;
                    norms.Add(n.normalized);
                    uvs.Add(new Vector2((float)lon / Segments, vFrac));
                }
            }

            for (int step = 0; step < steps; step++)
            {
                int rowBase = baseIdx + step * stride;
                for (int lon = 0; lon < Segments; lon++)
                {
                    int i0 = rowBase + lon;
                    int i1 = i0 + 1;
                    int i2 = i0 + stride;
                    int i3 = i2 + 1;

                    if (invertWinding)
                    {
                        tris.Add(i0); tris.Add(i2); tris.Add(i1);
                        tris.Add(i1); tris.Add(i2); tris.Add(i3);
                    }
                    else
                    {
                        tris.Add(i0); tris.Add(i1); tris.Add(i2);
                        tris.Add(i1); tris.Add(i3); tris.Add(i2);
                    }
                }
            }
        }

        private static void AppendRingBridge(
            List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs, List<int> tris,
            float y, float outerRadius, float innerRadius, float totalHeight, bool faceUp)
        {
            int baseIdx = verts.Count;
            float ny = faceUp ? 1f : -1f;
            float vFrac = Mathf.Clamp01((y + BodyHalfHeight + DomeRadius) / totalHeight);

            for (int lon = 0; lon <= Segments; lon++)
            {
                float theta = (float)lon / Segments * Mathf.PI * 2f;
                float cos = Mathf.Cos(theta);
                float sin = Mathf.Sin(theta);
                verts.Add(new Vector3(outerRadius * cos, y, outerRadius * sin));
                norms.Add(new Vector3(0f, ny, 0f));
                uvs.Add(new Vector2((float)lon / Segments, vFrac));
                verts.Add(new Vector3(innerRadius * cos, y, innerRadius * sin));
                norms.Add(new Vector3(0f, ny, 0f));
                uvs.Add(new Vector2((float)lon / Segments, vFrac));
            }

            int stride = 2;
            for (int lon = 0; lon < Segments; lon++)
            {
                int i0 = baseIdx + lon * stride;
                int i1 = i0 + 1;
                int i2 = i0 + stride;
                int i3 = i2 + 1;

                if (faceUp)
                {
                    tris.Add(i0); tris.Add(i2); tris.Add(i1);
                    tris.Add(i1); tris.Add(i2); tris.Add(i3);
                }
                else
                {
                    tris.Add(i0); tris.Add(i1); tris.Add(i2);
                    tris.Add(i1); tris.Add(i3); tris.Add(i2);
                }
            }
        }

        /// <summary>Append a filled disk cap (for neck top or base centre).</summary>
        private static void AppendDisk(
            List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs, List<int> tris,
            float y, float r, float totalHeight, bool faceUp)
        {
            int baseIdx = verts.Count;
            float ny = faceUp ? 1f : -1f;
            float vFrac = Mathf.Clamp01((y + BodyHalfHeight + DomeRadius) / totalHeight);

            // Centre vertex
            verts.Add(new Vector3(0f, y, 0f));
            norms.Add(new Vector3(0f, ny, 0f));
            uvs.Add(new Vector2(0.5f, vFrac));

            for (int lon = 0; lon <= Segments; lon++)
            {
                float theta = (float)lon / Segments * Mathf.PI * 2f;
                verts.Add(new Vector3(r * Mathf.Cos(theta), y, r * Mathf.Sin(theta)));
                norms.Add(new Vector3(0f, ny, 0f));
                uvs.Add(new Vector2(
                    0.5f + 0.5f * Mathf.Cos(theta),
                    vFrac));
            }

            int centre = baseIdx;
            for (int lon = 0; lon < Segments; lon++)
            {
                int i0 = baseIdx + 1 + lon;
                int i1 = i0 + 1;
                if (faceUp)
                {
                    tris.Add(centre); tris.Add(i0); tris.Add(i1);
                }
                else
                {
                    tris.Add(centre); tris.Add(i1); tris.Add(i0);
                }
            }
        }
    }
}
