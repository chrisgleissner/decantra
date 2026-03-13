using System.IO;
using NUnit.Framework;

namespace Decantra.Tests.EditMode.Visual
{
    public sealed class LiquidShaderInvariantTests
    {
        private const string LiquidShaderPath = "Assets/Decantra/Presentation/View3D/Shaders/Liquid3D.shader";
        private const string Bottle3DViewPath = "Assets/Decantra/Presentation/View3D/Bottle3DView.cs";

        [Test]
        public void LiquidShader_RemovesBoundaryHighlightAndMeniscusLogic()
        {
            string shader = File.ReadAllText(LiquidShaderPath);

            Assert.That(shader, Does.Not.Contain("_LayerSurfaceHighlight"));
            Assert.That(shader, Does.Not.Contain("_SurfaceRimStrength"));
            Assert.That(shader, Does.Not.Contain("_BoundarySharpness"));
            Assert.That(shader, Does.Not.Contain("_Meniscus"));
            Assert.That(shader, Does.Not.Contain("ComputeMeniscusOffset"));
        }

        [Test]
        public void LiquidShader_UsesHorizontalOnlyCylindricalShading()
        {
            string shader = File.ReadAllText(LiquidShaderPath);

            Assert.That(shader, Does.Contain("ComputeHorizontalShading"));
            Assert.That(shader, Does.Contain("ComputeHorizontalShading(IN.uv.x)"));
            Assert.That(shader, Does.Not.Contain("ComputeHorizontalShading(IN.uv.y)"));
        }

        [Test]
        public void Bottle3DView_DoesNotCreateExplicitFillLineRings()
        {
            string bottleView = File.ReadAllText(Bottle3DViewPath);

            Assert.That(bottleView, Does.Not.Contain("FillLineMin"));
            Assert.That(bottleView, Does.Not.Contain("FillLineMax"));
            Assert.That(bottleView, Does.Not.Contain("CreateFillLineGO"));
        }
    }
}