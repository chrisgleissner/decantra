using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Decantra.Tests.EditMode.Visual
{
    public sealed class LiquidShaderInvariantTests
    {
        private const string LiquidShaderPath = "Assets/Decantra/Presentation/View3D/Shaders/Liquid3D.shader";
        private const string BottleGlassShaderPath = "Assets/Decantra/Presentation/View3D/Shaders/BottleGlass.shader";
        private const string BottleViewPath = "Assets/Decantra/Presentation/View/BottleView.cs";
        private const string Bottle3DViewPath = "Assets/Decantra/Presentation/View3D/Bottle3DView.cs";
        private const string LiquidColorTuningPath = "Assets/Decantra/Presentation/View3D/LiquidColorTuning.cs";
        private const string GameControllerPath = "Assets/Decantra/Presentation/Controller/GameController.cs";
        private const string LiquidColorTuningTypeName = "Decantra.Presentation.View3D.LiquidColorTuning, Decantra.Presentation.View3D";

        private static System.Type ResolveLiquidColorTuningType()
        {
            var type = System.Type.GetType(LiquidColorTuningTypeName);
            Assert.NotNull(type, "LiquidColorTuning type could not be resolved.");
            return type;
        }

        private static float GetTuningFloat(string fieldName)
        {
            var field = ResolveLiquidColorTuningType().GetField(fieldName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            Assert.NotNull(field, $"LiquidColorTuning field '{fieldName}' was not found.");
            return (float)field.GetValue(null);
        }

        private static Color InvokeApplyGameplayVibrancy(Color source)
        {
            var method = ResolveLiquidColorTuningType().GetMethod("ApplyGameplayVibrancy", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            Assert.NotNull(method, "LiquidColorTuning.ApplyGameplayVibrancy was not found.");
            return (Color)method.Invoke(null, new object[] { source });
        }

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
        public void LiquidShader_Defaults_PreservePronouncedSideFalloff()
        {
            string shader = File.ReadAllText(LiquidShaderPath);

            Assert.That(shader, Does.Contain("_CylPower (\"Cyl Power\", Range(0.5, 4.0)) = 1.9"));
            Assert.That(shader, Does.Contain("_CylEdgeBrightness (\"Cyl Edge Brightness\", Range(0.5, 1.0)) = 0.72"));
            Assert.That(shader, Does.Contain("_CylCenterBrightness (\"Cyl Center Brightness\", Range(1.0, 1.25)) = 1.18"));
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
        public void Bottle3DView_AppliesSharedLiquidVibrancyTuning_ToStaticAndReceiveLayers()
        {
            string bottleView = File.ReadAllText(Bottle3DViewPath);
            string tuning = File.ReadAllText(LiquidColorTuningPath);

            Assert.That(tuning, Does.Contain("SaturationTarget = 1f"));
            Assert.That(tuning, Does.Contain("MinimumValue = 0.97f"));
            Assert.That(tuning, Does.Contain("ValueBoost = 1.45f"));
            Assert.That(tuning, Does.Contain("Color.RGBToHSV"));
            Assert.That(tuning, Does.Contain("Color.HSVToRGB"));
            Assert.That(bottleView, Does.Contain("_receiveColor = LiquidColorTuning.ApplyGameplayVibrancy(new Color(r, g, b, 1f));"));
            Assert.That(bottleView, Does.Contain("block.SetColor(PropLayerColor[0], LiquidColorTuning.ApplyGameplayVibrancy(new Color(layer.R, layer.G, layer.B, 1f)));"));
        }

        [Test]
        public void BottleView_AppliesMatchingHuePreservingVibrancyBoost_ToUiLiquidLayers()
        {
            string bottleView = File.ReadAllText(BottleViewPath);

            Assert.That(bottleView, Does.Contain("private static Color ApplyLiquidVibrancy(Color baseColor, float alpha)"));
            Assert.That(bottleView, Does.Contain("LiquidColorTuning.ApplyGameplayVibrancy(baseColor)"));
            Assert.That(bottleView, Does.Contain("image.color = ApplyLiquidVibrancy(palette.GetColor(color), 0.6f);"));
            Assert.That(bottleView, Does.Contain("image.color = ApplyLiquidVibrancy(palette.GetColor(color), 0.75f);"));
            Assert.That(bottleView, Does.Contain("image.color = ApplyLiquidVibrancy(palette.GetColor(color.Value), 1f);"));
            Assert.That(bottleView, Does.Contain("liquidSurface.color = ApplyLiquidVibrancy(palette.GetColor(topColor.Value), 0.55f);"));
        }

        [Test]
        public void LiquidColorTuning_DrivesLiquidsToFullSaturation_WhilePreservingHue()
        {
            Color source = new Color(0.42f, 0.18f, 0.76f, 1f);
            Color tuned = InvokeApplyGameplayVibrancy(source);

            Color.RGBToHSV(source, out float sourceHue, out _, out float sourceValue);
            Color.RGBToHSV(tuned, out float tunedHue, out float tunedSaturation, out float tunedValue);

            Assert.AreEqual(sourceHue, tunedHue, 0.001f);
            Assert.AreEqual(1f, tunedSaturation, 0.001f);
            Assert.GreaterOrEqual(tunedValue, Mathf.Max(sourceValue, GetTuningFloat("MinimumValue")) - 0.001f);
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