using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Decantra.Tests.EditMode.Visual
{
    public sealed class BottleMeshGeometryTests
    {
        private const string BottleMeshGeneratorPath = "Assets/Decantra/Presentation/View3D/BottleMeshGenerator.cs";
        private const string BottleMeshGeneratorTypeName = "Decantra.Presentation.View3D.BottleMeshGenerator, Decantra.Presentation.View3D";

        private static System.Type ResolveBottleMeshGeneratorType()
        {
            var type = System.Type.GetType(BottleMeshGeneratorTypeName);
            Assert.NotNull(type, "BottleMeshGenerator type could not be resolved.");
            return type;
        }

        private static float GetStaticFloat(string fieldName)
        {
            var field = ResolveBottleMeshGeneratorType().GetField(fieldName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            Assert.NotNull(field, $"BottleMeshGenerator field '{fieldName}' was not found.");
            return (float)field.GetValue(null);
        }

        private static int GetStaticInt(string fieldName)
        {
            var field = ResolveBottleMeshGeneratorType().GetField(fieldName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            Assert.NotNull(field, $"BottleMeshGenerator field '{fieldName}' was not found.");
            return (int)field.GetValue(null);
        }

        private static float InvokeStaticFloatMethod(string methodName, params object[] args)
        {
            var method = ResolveBottleMeshGeneratorType().GetMethod(methodName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            Assert.NotNull(method, $"BottleMeshGenerator method '{methodName}' was not found.");
            return (float)method.Invoke(null, args);
        }

        private static Mesh GenerateBottleMesh(bool keepReadable)
        {
            var method = ResolveBottleMeshGeneratorType().GetMethod("GenerateBottleMesh", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            Assert.NotNull(method, "BottleMeshGenerator.GenerateBottleMesh was not found.");
            return (Mesh)method.Invoke(null, new object[] { 1f, keepReadable });
        }

        [Test]
        public void NeckOverlayMesh_BeginsAtBodyTop_AndExtendsToRimTop()
        {
            string source = File.ReadAllText(BottleMeshGeneratorPath);

            Assert.That(source, Does.Contain("public static Mesh GenerateNeckOverlayMesh"));
            Assert.That(source, Does.Contain("float bodyTop = bodyBottom + scaledBodyHeight;"));
            Assert.That(source, Does.Contain("float rimTop = neckTop + RimLipHeight;"));
            Assert.That(source, Does.Contain("y0: bodyTop, y1: shoulderTop"));
            Assert.That(source, Does.Contain("y0: rimMid, y1: rimTop"));
        }

        [Test]
        public void TopBoundaryCollarMesh_AnchorsToBodyTopBoundary()
        {
            string source = File.ReadAllText(BottleMeshGeneratorPath);

            Assert.That(source, Does.Contain("float boundaryY = topBoundary ? GetBodyTopY(capped) : InteriorBottomY;"));
            Assert.That(source, Does.Contain("float y0 = topBoundary ? boundaryY - collarHeight : boundaryY;"));
            Assert.That(source, Does.Contain("float y1 = topBoundary ? boundaryY : boundaryY + collarHeight;"));
        }

        [Test]
        public void BottomBoundaryCollarMesh_AnchorsToInteriorBottomBoundary()
        {
            string source = File.ReadAllText(BottleMeshGeneratorPath);

            Assert.That(source, Does.Contain("float boundaryY = topBoundary ? GetBodyTopY(capped) : InteriorBottomY;"));
            Assert.That(source, Does.Contain("float collarHeight = 0.012f;"));
            Assert.That(source, Does.Contain("topBoundary ? \"BottleTopBoundaryCollar\" : \"BottleBottomBoundaryCollar\""));
        }

        [Test]
        public void FlatBase_UsesShortRoundedFoot_WhileKeepingLiquidFloorAnchor()
        {
            float domeRadius = GetStaticFloat("DomeRadius");
            float baseHeight = GetStaticFloat("BaseHeight");
            float interiorBottomY = GetStaticFloat("InteriorBottomY");
            float exteriorBottomY = GetStaticFloat("ExteriorBottomY");
            float bodyHeight = GetStaticFloat("BodyHeight");

            Assert.AreEqual(domeRadius * 0.21f, baseHeight, 0.0001f,
                "Flat base height should remain much shorter than the previous semicircular base radius.");
            Assert.AreEqual(
                interiorBottomY - baseHeight,
                exteriorBottomY,
                0.0001f,
                "The liquid floor anchor must remain unchanged above the new base.");
            Assert.AreEqual(
                bodyHeight,
                InvokeStaticFloatMethod("GetBodyTopY", 1f) - interiorBottomY,
                0.0001f,
                "The fillable body height must remain unchanged.");
        }

        [Test]
        public void BottleMesh_BaseHasFlatSupportAndRoundedCornerTransition()
        {
            float bodyRadius = GetStaticFloat("BodyRadius");
            float interiorBottomY = GetStaticFloat("InteriorBottomY");
            int segments = GetStaticInt("Segments");

            Mesh mesh = GenerateBottleMesh(keepReadable: true);
            try
            {
                Vector3[] vertices = mesh.vertices;
                float minY = vertices.Min(vertex => vertex.y);
                var bottomVertices = vertices
                    .Where(vertex => Mathf.Abs(vertex.y - minY) < 0.0001f)
                    .ToArray();

                Assert.That(bottomVertices.Length, Is.GreaterThan(segments / 2),
                    "A flat-bottom bottle should expose a broad support plane at the minimum Y.");

                float maxBottomRadius = bottomVertices.Max(vertex => Mathf.Sqrt(vertex.x * vertex.x + vertex.z * vertex.z));
                Assert.Greater(maxBottomRadius, bodyRadius * 0.92f,
                    "The support plane should span most of the body width so the base reads visibly flat in gameplay.");
                Assert.Less(maxBottomRadius, bodyRadius - 0.005f,
                    "The bottom support plane should sit inside the body wall so the lower edges remain rounded.");

                bool foundRoundedTransition = vertices.Any(vertex =>
                {
                    if (vertex.y <= minY + 0.0001f || vertex.y >= interiorBottomY - 0.0001f)
                    {
                        return false;
                    }

                    float radius = Mathf.Sqrt(vertex.x * vertex.x + vertex.z * vertex.z);
                    return radius > maxBottomRadius + 0.005f && radius < bodyRadius - 0.0025f;
                });

                Assert.IsTrue(foundRoundedTransition,
                    "Expected intermediate radii between the flat base and full body wall, proving the lower edge is rounded rather than sharp.");
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void FlatBase_ProfileRowsUseInclusiveSeamVertexMatchingStride()
        {
            string source = File.ReadAllText(BottleMeshGeneratorPath);

            Assert.That(source, Does.Contain("int stride = Segments + 1;"));
            Assert.That(source, Does.Contain("for (int lon = 0; lon <= Segments; lon++)"));
        }
    }
}