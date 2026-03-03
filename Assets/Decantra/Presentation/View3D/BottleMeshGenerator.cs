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
        public const int Segments = 24;

        /// <summary>Number of latitude segments for the base dome.</summary>
        public const int DomeLatitudes = 8;

        /// <summary>Number of vertical segments for the shoulder taper.</summary>
        public const int ShoulderSteps = 6;

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
        public static Mesh GenerateBottleMesh(bool keepReadable = false)
        {
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            float totalHeight = DomeRadius + BodyHeight + ShoulderHeight + NeckHeight;
            float yMin = -BodyHalfHeight;

            // ── 1. Hemispherical base dome (bottom) ──────────────────────────
            AppendDome(verts, norms, uvs, tris, yMin, totalHeight, flipY: true);

            // ── 2. Cylindrical body ──────────────────────────────────────────
            float bodyBottom = yMin + DomeRadius * 0.5f;
            float bodyTop = BodyHalfHeight;
            AppendCylinder(verts, norms, uvs, tris,
                           y0: bodyBottom, y1: bodyTop,
                           r0: BodyRadius, r1: BodyRadius,
                           totalHeight: totalHeight);

            // ── 3. Shoulder taper ────────────────────────────────────────────
            float shoulderTop = bodyTop + ShoulderHeight;
            AppendCylinder(verts, norms, uvs, tris,
                           y0: bodyTop, y1: shoulderTop,
                           r0: BodyRadius, r1: NeckRadius,
                           totalHeight: totalHeight,
                           steps: ShoulderSteps);

            // ── 4. Neck cylinder ─────────────────────────────────────────────
            float neckTop = shoulderTop + NeckHeight;
            AppendCylinder(verts, norms, uvs, tris,
                           y0: shoulderTop, y1: neckTop,
                           r0: NeckRadius, r1: NeckRadius,
                           totalHeight: totalHeight);

            // ── 5. Neck top cap ──────────────────────────────────────────────
            AppendDisk(verts, norms, uvs, tris, neckTop, NeckRadius, totalHeight, faceUp: true);

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
        /// of <see cref="InteriorHeight"/>.
        /// </summary>
        public static Mesh GenerateLiquidLayerMesh(float fillMin, float fillMax)
        {
            float yBottom = InteriorBottomY + fillMin * InteriorHeight;
            float yTop = InteriorBottomY + fillMax * InteriorHeight;

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

            var mesh = new Mesh { name = $"LiquidLayer_{fillMin:F2}_{fillMax:F2}" };
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
            float yBase, float totalHeight, bool flipY)
        {
            int baseIdx = verts.Count;
            float ySign = flipY ? -1f : 1f;
            float domeCenter = flipY ? yBase + DomeRadius : yBase - DomeRadius;

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
                        DomeRadius * sinPhi * cosTheta,
                        domeCenter + ySign * DomeRadius * cosPhi,
                        DomeRadius * sinPhi * sinTheta);

                    var n = new Vector3(sinPhi * cosTheta, ySign * cosPhi, sinPhi * sinTheta);
                    if (flipY) n.y = -Mathf.Abs(n.y);

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
                    if (flipY)
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
            float y0, float y1, float r0, float r1, float totalHeight, int steps = 1)
        {
            int baseIdx = verts.Count;
            int stride = Segments + 1;

            for (int step = 0; step <= steps; step++)
            {
                float t = (float)step / steps;
                float y = Mathf.Lerp(y0, y1, t);
                float r = Mathf.Lerp(r0, r1, t);
                float vCoord = (y - (verts.Count > 0 ? -BodyHalfHeight - DomeRadius : y)) / totalHeight;
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
