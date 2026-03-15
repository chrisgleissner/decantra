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
    ///   BaseBottomY   = ExteriorBottomY         (bottom of flat rounded base)
    ///   BodyBottomY   = InteriorBottomY         (top of the opaque glass base)
    ///   ShoulderTopY  = BodyTopY + ShoulderHeight
    ///   NeckBottomY   = ShoulderTopY
    ///   NeckTopY      = NeckBottomY + NeckHeight
    ///
    /// The mesh is built from:
    ///   1. Flat-bottom rounded base (bottom)
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

        /// <summary>Legacy pre-flat-base radius retained as the fill-region reference offset.</summary>
        public const float DomeRadius = BodyRadius;

        /// <summary>World-space height of the opaque glass base below the liquid region.</summary>
        public const float BaseHeight = DomeRadius * 0.21f;

        /// <summary>Corner radius used to softly round the flat base edges.</summary>
        public const float BaseCornerRadius = BaseHeight * 0.25f;

        /// <summary>Number of azimuthal segments (longitude). Higher = smoother cylinder.</summary>
        public const int Segments = 40;

        /// <summary>Number of profile steps used for the rounded base corners.</summary>
        public const int BaseCornerSteps = 5;

        /// <summary>Number of vertical segments for the shoulder taper.</summary>
        public const int ShoulderSteps = 6;

        /// <summary>Nominal wall thickness for the glass shell.</summary>
        public const float GlassThickness = 0.028f;

        /// <summary>Small outer overhang at the rim to form a visible lip.</summary>
        public const float RimLipOverhang = 0.018f;

        /// <summary>Rim lip height.</summary>
        public const float RimLipHeight = 0.035f;

        /// <summary>Extra-thick base glass depth so the bottom reads heavier than the rim.</summary>
        public const float BaseGlassThickness = GlassThickness * 2.5f;

        private const float BaseUvMin = 0.97f;
        private const float BaseUvMax = 1f;

        // ── Stopper / cork geometry ───────────────────────────────────────────
        // Realistic cork proportions (updated from flat-disc spec to proper cylinder):
        //   Cork height  = NeckRadius × 1.6  = 0.224 wu
        //   Cork radius  = NeckRadius × 1.05 = 0.147 wu
        //   Aspect ratio = height / radius   = 0.224 / 0.147 = 1.52  ✓ (spec: 1.2–2.0)
        //   75% inside neck, 25% protrudes above rim
        //
        // NeckDiameter = 2 × NeckRadius = 0.280 wu
        // CorkDiameter = 2 × StopperRadius = 0.294 wu  (1.05× neck — creates seal)
        // CorkHeight   = NeckRadius × 1.6 = 0.224 wu
        //   InsideDepth = 0.224 × 0.75 = 0.168 wu  (75% inside, spec: 70–80%)
        //   PeekHeight  = 0.224 × 0.25 = 0.056 wu  (25% outside, spec: 20–30%)

        /// <summary>
        /// Radius of the cylindrical cork stopper.
        /// Cork diameter ≈ neck diameter × 1.05 so the cork is visibly slightly wider
        /// than the outer neck, creating a physical seal impression.
        /// </summary>
        public const float StopperRadius = NeckRadius * 1.05f;               // 0.147 wu

        /// <summary>
        /// Total height of the cork cylinder.
        /// NeckRadius × 1.6 gives aspect ratio = 0.224 / 0.147 = 1.52, within 1.2–2.0.
        /// </summary>
        public const float StopperTotalHeight = NeckRadius * 1.6f;           // 0.224 wu

        /// <summary>
        /// Depth the stopper is inserted into the neck (75% of total height).
        /// The stopper mesh bottom sits at StopperBaseY; the glass neck walls surround it.
        /// </summary>
        public const float StopperInsideDepth = StopperTotalHeight * 0.75f;  // 0.168 wu

        /// <summary>
        /// Height the stopper protrudes above the outer rim top (25% of total height).
        /// </summary>
        public const float StopperPeekHeight = StopperTotalHeight * 0.25f;   // 0.056 wu

        private const float BodyHalfHeight = BodyHeight * 0.5f;

        /// <summary>
        /// Y position of the interior bottom of the liquid region (top of the base glass).
        /// This remains fixed so the liquid region stays unchanged relative to the body.
        /// </summary>
        public static readonly float InteriorBottomY = -BodyHalfHeight + DomeRadius;

        /// <summary>Y position of the exterior flat base bottom.</summary>
        public static readonly float ExteriorBottomY = InteriorBottomY - BaseHeight;

        /// <summary>
        /// Y position of the interior top of the liquid region (top of body cylinder).
        /// bodyBottom = InteriorBottomY, bodyTop = bodyBottom + BodyHeight.
        /// </summary>
        public static readonly float InteriorTopY = InteriorBottomY + BodyHeight;

        /// <summary>Total interior liquid height in world units.</summary>
        public static float InteriorHeight => InteriorTopY - InteriorBottomY;

        /// <summary>Full exterior bottle height for a reference-capacity bottle.</summary>
        public static float ReferenceMeshHeight => BaseHeight + BodyHeight + ShoulderHeight + NeckHeight + RimLipHeight;

        // Computed Y position of the rim/flange top referenced by Bottle3DView.
        public static readonly float RimTopY =
            InteriorBottomY + BodyHeight + ShoulderHeight + NeckHeight + RimLipHeight;

        // The cork bottom is measured relative to the visible rim/flange top, not the neck top.
        // This keeps the cork visually flush with the bottle top instead of hovering above short bottles.
        public static readonly float StopperBaseY = RimTopY - StopperInsideDepth;

        public static float GetRimTopY(float capacityRatio)
        {
            float capped = Mathf.Clamp(capacityRatio, 0.1f, 1f);
            return InteriorBottomY + BodyHeight * capped + ShoulderHeight + NeckHeight + RimLipHeight;
        }

        /// <summary>
        /// Capacity-aware version of <see cref="StopperBaseY"/>.
        /// Only the cylindrical body height scales with <paramref name="capacityRatio"/>;
        /// shoulder, neck, and rim stay at their reference sizes, so the neck-top Y
        /// depends on the body height baked into the current mesh.
        /// </summary>
        public static float GetStopperBaseY(float capacityRatio)
        {
            return GetRimTopY(capacityRatio) - StopperInsideDepth;
        }

        public static float GetBodyTopY(float capacityRatio)
        {
            float capped = Mathf.Clamp(capacityRatio, 0.1f, 1f);
            return InteriorBottomY + BodyHeight * capped;
        }

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
        /// cylindrical body section scales; the base, shoulder, neck, and rim stay at their
        /// reference sizes — matching the 2D BottleView body-only stretch philosophy.</param>
        public static Mesh GenerateBottleMesh(float capacityRatio = 1f, bool keepReadable = false)
        {
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            // Only the body cylinder scales; all other sections stay fixed.
            float scaledBodyHeight = BodyHeight * Mathf.Clamp(capacityRatio, 0.1f, 1f);
            float yMin = ExteriorBottomY;
            float bodyBottom = InteriorBottomY;
            float bodyTop = bodyBottom + scaledBodyHeight;  // body end floats up/down
            float totalHeight = BaseHeight + scaledBodyHeight + ShoulderHeight + NeckHeight + RimLipHeight;
            float shoulderTop = bodyTop + ShoulderHeight;
            float neckTop = shoulderTop + NeckHeight;
            float rimTop = neckTop + RimLipHeight;

            // Inner shell dimensions (clamped for robustness)
            float innerBodyRadius = Mathf.Max(BodyRadius - GlassThickness, 0.01f);
            float innerNeckRadius = Mathf.Max(NeckRadius - GlassThickness * 0.9f, 0.01f);
            float innerBodyBottom = bodyBottom + GlassThickness * 0.55f;
            float innerBodyTop = bodyTop - GlassThickness * 0.25f;
            float innerShoulderTop = shoulderTop - GlassThickness * 0.15f;
            float innerNeckTop = neckTop + RimLipHeight * 0.58f;
            float innerBottomY = yMin + BaseGlassThickness;
            float innerBaseHeight = Mathf.Max(innerBodyBottom - innerBottomY, 0.04f);
            float innerCornerRadius = Mathf.Clamp(
                BaseCornerRadius - GlassThickness * 1.25f,
                0.02f,
                Mathf.Min(innerBaseHeight - 0.005f, innerBodyRadius - 0.02f));

            // ── Outer shell ───────────────────────────────────────────────────
            AppendRoundedFlatBase(verts, norms, uvs, tris,
                                  topY: bodyBottom,
                                  bottomY: yMin,
                                  sideRadius: BodyRadius,
                                  cornerRadius: BaseCornerRadius,
                                  totalHeight: totalHeight,
                                  invertWinding: false,
                                  invertNormal: false);
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
            AppendRoundedFlatBase(verts, norms, uvs, tris,
                                  topY: innerBodyBottom,
                                  bottomY: innerBottomY,
                                  sideRadius: innerBodyRadius,
                                  cornerRadius: innerCornerRadius,
                                  totalHeight: totalHeight,
                                  invertWinding: true,
                                  invertNormal: true);
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

        public static Mesh GenerateNeckOverlayMesh(float capacityRatio = 1f, float radialOutset = 0f, bool keepReadable = false)
        {
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            float capped = Mathf.Clamp(capacityRatio, 0.1f, 1f);
            float scaledBodyHeight = BodyHeight * capped;
            float bodyBottom = InteriorBottomY;
            float bodyTop = bodyBottom + scaledBodyHeight;
            float totalHeight = BaseHeight + scaledBodyHeight + ShoulderHeight + NeckHeight + RimLipHeight;
            float shoulderTop = bodyTop + ShoulderHeight;
            float neckTop = shoulderTop + NeckHeight;
            float rimTop = neckTop + RimLipHeight;
            float bodyRadius = BodyRadius + radialOutset;
            float neckRadius = NeckRadius + radialOutset;
            float lipRadius = NeckRadius + RimLipOverhang + radialOutset;

            AppendCylinder(verts, norms, uvs, tris,
                           y0: bodyTop, y1: shoulderTop,
                           r0: bodyRadius, r1: neckRadius,
                           totalHeight: totalHeight,
                           steps: ShoulderSteps,
                           invertWinding: false,
                           invertNormal: false);
            AppendCylinder(verts, norms, uvs, tris,
                           y0: shoulderTop, y1: neckTop,
                           r0: neckRadius, r1: neckRadius,
                           totalHeight: totalHeight,
                           invertWinding: false,
                           invertNormal: false);

            float rimMid = neckTop + RimLipHeight * 0.45f;
            AppendCylinder(verts, norms, uvs, tris,
                           y0: neckTop, y1: rimMid,
                           r0: neckRadius, r1: lipRadius,
                           totalHeight: totalHeight,
                           steps: 2,
                           invertWinding: false,
                           invertNormal: false);
            AppendCylinder(verts, norms, uvs, tris,
                           y0: rimMid, y1: rimTop,
                           r0: lipRadius, r1: NeckRadius + RimLipOverhang * 0.75f + radialOutset,
                           totalHeight: totalHeight,
                           steps: 2,
                           invertWinding: false,
                           invertNormal: false);

            var mesh = new Mesh { name = "BottleNeckOverlayMesh" };
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();

            if (!keepReadable)
                mesh.UploadMeshData(markNoLongerReadable: true);

            return mesh;
        }

        public static Mesh GenerateBoundaryCollarMesh(float capacityRatio = 1f, bool topBoundary = true, bool keepReadable = false)
        {
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            float capped = Mathf.Clamp(capacityRatio, 0.1f, 1f);
            float scaledBodyHeight = BodyHeight * capped;
            float totalHeight = BaseHeight + scaledBodyHeight + ShoulderHeight + NeckHeight + RimLipHeight;
            float boundaryY = topBoundary ? GetBodyTopY(capped) : InteriorBottomY;
            float collarHeight = 0.012f;
            float y0 = topBoundary ? boundaryY - collarHeight : boundaryY;
            float y1 = topBoundary ? boundaryY : boundaryY + collarHeight;
            float outerRadius = Mathf.Max(BodyRadius - GlassThickness * 1.15f, 0.01f);
            float innerRadius = Mathf.Max(outerRadius - 0.016f, 0.005f);

            AppendCylinder(verts, norms, uvs, tris,
                           y0: y0, y1: y1,
                           r0: outerRadius, r1: outerRadius,
                           totalHeight: totalHeight,
                           invertWinding: false,
                           invertNormal: false);
            AppendCylinder(verts, norms, uvs, tris,
                           y0: y0, y1: y1,
                           r0: innerRadius, r1: innerRadius,
                           totalHeight: totalHeight,
                           invertWinding: true,
                           invertNormal: true);
            AppendRingBridge(verts, norms, uvs, tris,
                             y: y0,
                             outerRadius: outerRadius,
                             innerRadius: innerRadius,
                             totalHeight: totalHeight,
                             faceUp: false);
            AppendRingBridge(verts, norms, uvs, tris,
                             y: y1,
                             outerRadius: outerRadius,
                             innerRadius: innerRadius,
                             totalHeight: totalHeight,
                             faceUp: true);

            var mesh = new Mesh { name = topBoundary ? "BottleTopBoundaryCollar" : "BottleBottomBoundaryCollar" };
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();

            if (!keepReadable)
                mesh.UploadMeshData(markNoLongerReadable: true);

            return mesh;
        }

        // ── Primitive builders ────────────────────────────────────────────────

        /// <summary>
        /// Append a rotationally-symmetric flat base with softly rounded lower corners.
        /// The entire base UV range is packed into the existing shader band [0.97..1.0]
        /// so sink and regular bottles preserve their prior base-color logic unchanged.
        /// </summary>
        private static void AppendRoundedFlatBase(
            List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs, List<int> tris,
            float topY, float bottomY, float sideRadius, float cornerRadius, float totalHeight,
            bool invertWinding, bool invertNormal)
        {
            float baseHeight = topY - bottomY;
            if (baseHeight <= 0.001f || sideRadius <= 0.01f)
            {
                AppendCylinder(verts, norms, uvs, tris,
                               y0: bottomY, y1: topY,
                               r0: sideRadius, r1: sideRadius,
                               totalHeight: totalHeight,
                               invertWinding: invertWinding,
                               invertNormal: invertNormal);
                return;
            }

            float clampedCorner = Mathf.Min(cornerRadius, Mathf.Min(baseHeight, sideRadius - 0.01f));
            float flatRadius = Mathf.Max(sideRadius - clampedCorner, 0.01f);
            float cornerCenterY = bottomY + clampedCorner;

            var rowYs = new List<float>(BaseCornerSteps + 2) { topY };
            var rowRadii = new List<float>(BaseCornerSteps + 2) { sideRadius };
            var rowRadialNormals = new List<float>(BaseCornerSteps + 2) { 1f };
            var rowVerticalNormals = new List<float>(BaseCornerSteps + 2) { 0f };

            if (topY - cornerCenterY > 0.001f)
            {
                rowYs.Add(cornerCenterY);
                rowRadii.Add(sideRadius);
                rowRadialNormals.Add(1f);
                rowVerticalNormals.Add(0f);
            }

            for (int step = 1; step <= BaseCornerSteps; step++)
            {
                float angle = (float)step / BaseCornerSteps * Mathf.PI * 0.5f;
                float radial = Mathf.Cos(angle);
                float vertical = Mathf.Sin(angle);
                rowYs.Add(cornerCenterY - vertical * clampedCorner);
                rowRadii.Add(flatRadius + radial * clampedCorner);
                rowRadialNormals.Add(radial);
                rowVerticalNormals.Add(-vertical);
            }

            int baseIdx = verts.Count;
            int stride = Segments + 1;
            for (int row = 0; row < rowYs.Count; row++)
            {
                float rowY = rowYs[row];
                float radius = rowRadii[row];
                float radialNormal = rowRadialNormals[row];
                float verticalNormal = rowVerticalNormals[row];
                float t = Mathf.InverseLerp(topY, bottomY, rowY);
                float vFrac = Mathf.Lerp(BaseUvMin, BaseUvMax, t);

                for (int lon = 0; lon < Segments; lon++)
                {
                    float theta = (float)lon / Segments * Mathf.PI * 2f;
                    float cosTheta = Mathf.Cos(theta);
                    float sinTheta = Mathf.Sin(theta);
                    verts.Add(new Vector3(radius * cosTheta, rowY, radius * sinTheta));

                    var normal = new Vector3(
                        radialNormal * cosTheta,
                        verticalNormal,
                        radialNormal * sinTheta);
                    if (invertNormal) normal = -normal;
                    norms.Add(normal.normalized);
                    uvs.Add(new Vector2((float)lon / Segments, vFrac));
                }
            }

            for (int row = 0; row < rowYs.Count - 1; row++)
            {
                int rowBase = baseIdx + row * stride;
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

            AppendDisk(verts, norms, uvs, tris,
                       y: bottomY,
                       r: flatRadius,
                       totalHeight: totalHeight,
                       faceUp: invertWinding,
                       vOverride: BaseUvMax);
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
                float vFrac = Mathf.Clamp01((y - ExteriorBottomY) / totalHeight);

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
            float vFrac = Mathf.Clamp01((y - ExteriorBottomY) / totalHeight);

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
            float y, float r, float totalHeight, bool faceUp, float vOverride = -1f)
        {
            int baseIdx = verts.Count;
            float ny = faceUp ? 1f : -1f;
            float vFrac = vOverride >= 0f ? vOverride : Mathf.Clamp01((y - ExteriorBottomY) / totalHeight);

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
