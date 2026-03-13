using System.IO;
using NUnit.Framework;

namespace Decantra.Tests.EditMode.Visual
{
    public sealed class LiquidShaderInvariantTests
    {
        private const string LiquidShaderPath = "Assets/Decantra/Presentation/View3D/Shaders/Liquid3D.shader";
        private const string BottleGlassShaderPath = "Assets/Decantra/Presentation/View3D/Shaders/BottleGlass.shader";
        private const string Bottle3DViewPath = "Assets/Decantra/Presentation/View3D/Bottle3DView.cs";
        private const string GameControllerPath = "Assets/Decantra/Presentation/Controller/GameController.cs";

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
            Assert.That(shader, Does.Contain("_CylEdgeBrightness"));
            Assert.That(shader, Does.Contain("_CylCenterBrightness"));
            Assert.That(shader, Does.Not.Contain("_CylRightLift"));
            Assert.That(shader, Does.Contain("return lerp(_CylEdgeBrightness, _CylCenterBrightness, cylindrical);"));
            Assert.That(shader, Does.Contain("ApplyBrightnessPreservingChroma(col.rgb, ComputeHorizontalShading(IN.uv.x))"));
        }

        [Test]
        public void LiquidShader_CurvesEveryLiquidBoundary_UsingSharedArcOffset()
        {
            string shader = File.ReadAllText(LiquidShaderPath);

            Assert.That(shader, Does.Contain("_SurfaceArcHeight"));
            Assert.That(shader, Does.Contain("ComputeBoundaryArcOffset"));
            Assert.That(shader, Does.Contain("float boundaryArcOffset = ComputeBoundaryArcOffset(uX);"));
            Assert.That(shader, Does.Contain("float layerMin = minV + boundaryArcOffset;"));
            Assert.That(shader, Does.Contain("float layerMax = min(maxV, _TotalFill) + boundaryArcOffset;"));
        }

        [Test]
        public void Bottle3DView_DoesNotCreateExplicitFillLineRings()
        {
            string bottleView = File.ReadAllText(Bottle3DViewPath);

            Assert.That(bottleView, Does.Not.Contain("FillLineMin"));
            Assert.That(bottleView, Does.Not.Contain("FillLineMax"));
            Assert.That(bottleView, Does.Not.Contain("CreateFillLineGO"));
        }

        [Test]
        public void BottleGlassShader_DoesNotUseShaderNeckOrFillBoundaryBands()
        {
            string shader = File.ReadAllText(BottleGlassShaderPath);

            Assert.That(shader, Does.Not.Contain("junctionUV"));
            Assert.That(shader, Does.Not.Contain("neckMask"));
        }

        [Test]
        public void Bottle3DView_UsesGeometryBackedNeckAndBoundaryDetails()
        {
            string bottleView = File.ReadAllText(Bottle3DViewPath);

            Assert.That(bottleView, Does.Contain("GenerateNeckOverlayMesh"));
            Assert.That(bottleView, Does.Contain("GenerateBoundaryCollarMesh"));
        }

        [Test]
        public void GameController_StartsPourAudioInsideAnimationLoop_AndStopsItAfterwards()
        {
            string controller = File.ReadAllText(GameControllerPath);

            Assert.That(controller, Does.Contain("if (!pourSfxStarted && t > 0f)"));
            Assert.That(controller, Does.Contain("PlayPourSfx(previousFillRatio, newFillRatio);"));
            Assert.That(controller, Does.Contain("StopPourSfx();"));
        }
    }
}