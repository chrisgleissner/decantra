using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Decantra.Tests.EditMode.Visual
{
    public sealed class BottleMeshGeometryTests
    {
        private static Type BottleMeshGeneratorType => Type.GetType("Decantra.Presentation.View3D.BottleMeshGenerator, Decantra.Presentation.View3D");

        [Test]
        public void NeckOverlayMesh_StartsAtFillBoundary_AndEndsAtRimTop()
        {
            Mesh mesh = InvokeMeshFactory("GenerateNeckOverlayMesh", 1f, 0f, true);
            float bodyTop = InvokeStaticFloatMethod("GetBodyTopY", 1f);
            float rimTop = InvokeStaticFloatMethod("GetRimTopY", 1f);

            Assert.That(mesh.bounds.min.y, Is.EqualTo(bodyTop).Within(0.005f));
            Assert.That(mesh.bounds.max.y, Is.EqualTo(rimTop).Within(0.005f));

            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void TopBoundaryCollarMesh_StaysAtTopFillBoundary()
        {
            const float CapacityRatio = 0.75f;
            Mesh mesh = InvokeMeshFactory("GenerateBoundaryCollarMesh", CapacityRatio, true, true);
            float bodyTop = InvokeStaticFloatMethod("GetBodyTopY", CapacityRatio);

            Assert.That(mesh.bounds.max.y, Is.LessThanOrEqualTo(bodyTop + 0.0001f));
            Assert.That(mesh.bounds.min.y, Is.GreaterThan(bodyTop - 0.02f));

            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void BottomBoundaryCollarMesh_StaysAtBottomFillBoundary()
        {
            Mesh mesh = InvokeMeshFactory("GenerateBoundaryCollarMesh", 1f, false, true);
            float bodyBottom = GetStaticFloatField("InteriorBottomY");

            Assert.That(mesh.bounds.min.y, Is.GreaterThanOrEqualTo(bodyBottom - 0.0001f));
            Assert.That(mesh.bounds.max.y, Is.LessThanOrEqualTo(bodyBottom + 0.02f));

            Object.DestroyImmediate(mesh);
        }

        private static Mesh InvokeMeshFactory(string methodName, params object[] args)
        {
            Assert.NotNull(BottleMeshGeneratorType, "BottleMeshGenerator type was not found.");

            MethodInfo method = BottleMeshGeneratorType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(method, $"Missing method '{methodName}'.");

            return method.Invoke(null, args) as Mesh;
        }

        private static float InvokeStaticFloatMethod(string methodName, params object[] args)
        {
            Assert.NotNull(BottleMeshGeneratorType, "BottleMeshGenerator type was not found.");

            MethodInfo method = BottleMeshGeneratorType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(method, $"Missing method '{methodName}'.");

            return (float)method.Invoke(null, args);
        }

        private static float GetStaticFloatField(string fieldName)
        {
            Assert.NotNull(BottleMeshGeneratorType, "BottleMeshGenerator type was not found.");

            FieldInfo field = BottleMeshGeneratorType.GetField(fieldName, BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(field, $"Missing field '{fieldName}'.");

            return (float)field.GetValue(null);
        }
    }
}