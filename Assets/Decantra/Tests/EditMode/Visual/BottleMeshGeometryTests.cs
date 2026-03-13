using Decantra.Presentation.View3D;
using NUnit.Framework;
using UnityEngine;

namespace Decantra.Tests.EditMode.Visual
{
    public sealed class BottleMeshGeometryTests
    {
        [Test]
        public void NeckOverlayMesh_StartsAtFillBoundary_AndEndsAtRimTop()
        {
            Mesh mesh = BottleMeshGenerator.GenerateNeckOverlayMesh(1f, keepReadable: true);

            Assert.That(mesh.bounds.min.y, Is.EqualTo(BottleMeshGenerator.GetBodyTopY(1f)).Within(0.005f));
            Assert.That(mesh.bounds.max.y, Is.EqualTo(BottleMeshGenerator.GetRimTopY(1f)).Within(0.005f));

            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void TopBoundaryCollarMesh_StaysAtTopFillBoundary()
        {
            const float CapacityRatio = 0.75f;
            Mesh mesh = BottleMeshGenerator.GenerateBoundaryCollarMesh(CapacityRatio, topBoundary: true, keepReadable: true);
            float bodyTop = BottleMeshGenerator.GetBodyTopY(CapacityRatio);

            Assert.That(mesh.bounds.max.y, Is.LessThanOrEqualTo(bodyTop + 0.0001f));
            Assert.That(mesh.bounds.min.y, Is.GreaterThan(bodyTop - 0.02f));

            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void BottomBoundaryCollarMesh_StaysAtBottomFillBoundary()
        {
            Mesh mesh = BottleMeshGenerator.GenerateBoundaryCollarMesh(1f, topBoundary: false, keepReadable: true);
            float bodyBottom = BottleMeshGenerator.InteriorBottomY;

            Assert.That(mesh.bounds.min.y, Is.GreaterThanOrEqualTo(bodyBottom - 0.0001f));
            Assert.That(mesh.bounds.max.y, Is.LessThanOrEqualTo(bodyBottom + 0.02f));

            Object.DestroyImmediate(mesh);
        }
    }
}