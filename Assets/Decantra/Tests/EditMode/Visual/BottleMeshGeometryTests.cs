using System.IO;
using NUnit.Framework;

namespace Decantra.Tests.EditMode.Visual
{
    public sealed class BottleMeshGeometryTests
    {
        private const string BottleMeshGeneratorPath = "Assets/Decantra/Presentation/View3D/BottleMeshGenerator.cs";

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
    }
}