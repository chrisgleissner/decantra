using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Decantra.Domain.Model;
using Decantra.Presentation;
using Decantra.Presentation.Controller;
using Decantra.Presentation.View;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Decantra.Tests.PlayMode
{
    public sealed class BottleReadabilityDensityPlayModeTests
    {
        private static readonly int PropSinkOnly = Shader.PropertyToID("_SinkOnly");
        private static readonly int PropLayer0Min = Shader.PropertyToID("_Layer0Min");
        private static readonly int PropLayer0Max = Shader.PropertyToID("_Layer0Max");
        private static readonly int PropTotalFill = Shader.PropertyToID("_TotalFill");

        [UnityTest]
        public IEnumerator BottleOverlap_NoneOnThreeRowBoard()
        {
            GameController controller = null;
            List<Component> views = null;
            yield return BootstrapThreeRowBoard((resolvedController, resolvedViews) =>
            {
                controller = resolvedController;
                views = resolvedViews;
            });

            var bounds = CollectGlassBounds(views);
            for (int i = 0; i < bounds.Count; i++)
            {
                for (int j = i + 1; j < bounds.Count; j++)
                {
                    Assert.IsFalse(bounds[i].Intersects(bounds[j]),
                        $"Bottle bounds intersect between indices {i} and {j}.");
                }
            }
        }

        [UnityTest]
        public IEnumerator HudClearance_TopRowRemainsBelowHud()
        {
            GameController controller = null;
            List<Component> views = null;
            yield return BootstrapThreeRowBoard((resolvedController, resolvedViews) =>
            {
                controller = resolvedController;
                views = resolvedViews;
            });

            var uiCamera = GameObject.Find("Camera_UI")?.GetComponent<Camera>()
                ?? Camera.main
                ?? UnityEngine.Object.FindFirstObjectByType<Camera>();
            var secondaryHud = GameObject.Find("SecondaryHud")?.GetComponent<RectTransform>();
            Assert.NotNull(secondaryHud, "SecondaryHud not found.");

            var bottleViews = GetPrivateField<List<BottleView>>(controller, "bottleViews") ?? new List<BottleView>();
            var rects = CollectBottleViewScreenRects(bottleViews, uiCamera);
            Assert.That(rects.Count, Is.GreaterThan(6), "Expected a three-row board for HUD clearance validation.");
            var hudRect = ResolveScreenRect(secondaryHud, uiCamera);

            var topRow = rects.OrderByDescending(rect => rect.yMax).Take(3).ToList();
            for (int i = 0; i < topRow.Count; i++)
            {
                float clearance = hudRect.yMin - topRow[i].yMax;
                Assert.GreaterOrEqual(clearance, -2f,
                    $"Top-row bottle {i} intrudes into the HUD. bottleTop={topRow[i].yMax:F2}, hudBottom={hudRect.yMin:F2}, clearance={clearance:F2}");
            }

            _ = views;
        }

        [UnityTest]
        public IEnumerator ThreeRowLayout_MaximizesBottleHeight_AndAvoidsCenteredOuterGaps()
        {
            GameController controller = null;
            yield return BootstrapThreeRowBoard((resolvedController, _) =>
            {
                controller = resolvedController;
            });

            var hudSafeLayout = UnityEngine.Object.FindFirstObjectByType<HudSafeLayout>();
            Assert.NotNull(hudSafeLayout, "HudSafeLayout not found.");

            var bottleGridLayout = GetPrivateField<GridLayoutGroup>(hudSafeLayout, "bottleGridLayout");
            var bottleGrid = GetPrivateField<RectTransform>(hudSafeLayout, "bottleGrid");
            var bottleViews = GetPrivateField<List<BottleView>>(controller, "bottleViews") ?? new List<BottleView>();
            Assert.NotNull(bottleGridLayout, "Bottle grid layout missing.");
            Assert.NotNull(bottleGrid, "Bottle grid missing.");

            var uiCamera = GameObject.Find("Camera_UI")?.GetComponent<Camera>()
                ?? Camera.main
                ?? UnityEngine.Object.FindFirstObjectByType<Camera>();
            var secondaryHud = GameObject.Find("SecondaryHud")?.GetComponent<RectTransform>();
            var bottomHud = GameObject.Find("BottomHud")?.GetComponent<RectTransform>();
            Assert.NotNull(secondaryHud, "SecondaryHud not found.");
            Assert.NotNull(bottomHud, "BottomHud not found.");

            var bottleRects = CollectBottleViewScreenRects(bottleViews, uiCamera);
            var hudRect = ResolveScreenRect(secondaryHud, uiCamera);
            var footerRect = ResolveScreenRect(bottomHud, uiCamera);
            float bottleTop = bottleRects.Max(rect => rect.yMax);
            float bottleBottom = bottleRects.Min(rect => rect.yMin);
            float topClearance = hudRect.yMin - bottleTop;
            float playableHeight = hudRect.yMin - footerRect.yMax;
            float occupiedHeight = bottleTop - bottleBottom;
            float bottomClearance = bottleBottom - footerRect.yMax;

            Assert.GreaterOrEqual(bottleGridLayout.spacing.y, 0f,
                $"3-row spacing became negative. actual={bottleGridLayout.spacing.y:F2}");
            Assert.GreaterOrEqual(topClearance, -2f,
                $"Top row overlaps the HUD. clearance={topClearance:F2}");
            Assert.LessOrEqual(topClearance, 48f,
                $"Top clearance is larger than necessary, which indicates wasted vertical space. clearance={topClearance:F2}");
            Assert.GreaterOrEqual(occupiedHeight / playableHeight, 0.78f,
                $"3-row bottles are not using enough vertical board space. occupied={occupiedHeight:F2}, playable={playableHeight:F2}, ratio={occupiedHeight / playableHeight:F3}");
            Assert.GreaterOrEqual(bottomClearance, -2f,
                $"Bottom row overlaps the footer or screen edge. clearance={bottomClearance:F2}");
            Assert.LessOrEqual(bottomClearance, 40f,
                $"Bottom clearance is too large, which indicates vertical centering waste. clearance={bottomClearance:F2}");
            Assert.GreaterOrEqual(bottleGrid.anchoredPosition.y, 0f,
                $"3-row bottle grid should remain top-anchored or flush with the safe area's top boundary. anchoredY={bottleGrid.anchoredPosition.y:F2}");

            _ = controller;
        }

        [UnityTest]
        public IEnumerator BottleCaps_ClearTheRowAbove()
        {
            List<Component> views = null;
            yield return BootstrapThreeRowBoard((_, resolvedViews) =>
            {
                views = resolvedViews;
            });

            var columns = GroupViewsByColumn(views);
            Assert.That(columns.Count, Is.EqualTo(3), "Expected exactly three bottle columns.");

            foreach (var column in columns)
            {
                var ordered = column.OrderByDescending(view => GetWorldRootTransform(view).position.y).ToList();
                for (int i = 0; i < ordered.Count - 1; i++)
                {
                    var upperBounds = ResolveGlassRenderer(ordered[i]).bounds;
                    var lowerBounds = ResolveGlassRenderer(ordered[i + 1]).bounds;
                    Assert.Less(lowerBounds.max.y, upperBounds.min.y,
                        $"Bottle cap clearance failed between rows in column. lowerTop={lowerBounds.max.y:F4}, upperBottom={upperBounds.min.y:F4}");
                }
            }
        }

        [UnityTest]
        public IEnumerator IndicatorCorrectness_RegularUsesFrostedMode_AndSinkUsesBlackMode()
        {
            SceneBootstrap.EnsureScene();
            yield return null;
            yield return null;

            var controller = ResolvePrimaryController();
            Assert.NotNull(controller, "GameController not found.");

            var bottleViews = Force3DPresentation(controller);
            Assert.That(bottleViews.Count, Is.GreaterThanOrEqualTo(2), "Expected at least two bottle views.");

            var regularView = bottleViews[0];
            var sinkView = bottleViews[1];
            regularView.SetPresentation3DEnabled(true);
            sinkView.SetPresentation3DEnabled(true);
            regularView.SetLevelMaxCapacity(4);
            sinkView.SetLevelMaxCapacity(4);

            var regularBottle = new Bottle(new ColorId?[4] { ColorId.Red, ColorId.Red, null, null });
            var sinkBottle = new Bottle(new ColorId?[4] { ColorId.Blue, ColorId.Blue, null, null }, isSink: true);
            regularView.Render(regularBottle);
            sinkView.Render(sinkBottle);
            yield return null;

            var regular3D = GetBottle3DViewComponent(regularView);
            var sink3D = GetBottle3DViewComponent(sinkView);
            Assert.NotNull(regular3D, "Regular Bottle3DView missing.");
            Assert.NotNull(sink3D, "Sink Bottle3DView missing.");

            InvokeBottle3DSetLevelMaxCapacity(regular3D, 4);
            InvokeBottle3DSetLevelMaxCapacity(sink3D, 4);
            InvokeBottle3DRender(regular3D, regularBottle);
            InvokeBottle3DRender(sink3D, sinkBottle);
            yield return null;

            var regularRenderer = ResolveGlassRenderer(regular3D);
            var sinkRenderer = ResolveGlassRenderer(sink3D);
            Assert.NotNull(regularRenderer, "Regular glass renderer missing.");
            Assert.NotNull(sinkRenderer, "Sink glass renderer missing.");
            Assert.AreEqual("Decantra/BottleGlass", regularRenderer.sharedMaterial.shader.name);
            Assert.AreEqual("Decantra/BottleGlass", sinkRenderer.sharedMaterial.shader.name);
            Assert.IsTrue(regularRenderer.sharedMaterial.HasProperty("_FrostAlpha"), "Shared glass shader lost frosted indicator support.");

            var regularBlock = new MaterialPropertyBlock();
            var sinkBlock = new MaterialPropertyBlock();
            regularRenderer.GetPropertyBlock(regularBlock);
            sinkRenderer.GetPropertyBlock(sinkBlock);

            Assert.AreEqual(0f, regularBlock.GetFloat(PropSinkOnly), 0.001f,
                "Regular bottles should stay on the frosted indicator path.");
            Assert.AreEqual(1f, sinkBlock.GetFloat(PropSinkOnly), 0.001f,
                "Sink bottles should switch the shared indicator path into black-cap mode.");
        }

        [UnityTest]
        public IEnumerator LiquidRenderingRegression_FillBoundsRemainMappedToBottleState()
        {
            SceneBootstrap.EnsureScene();
            yield return null;
            yield return null;

            var controller = ResolvePrimaryController();
            Assert.NotNull(controller, "GameController not found.");

            var bottleViews = Force3DPresentation(controller);
            Assert.That(bottleViews.Count, Is.GreaterThan(0), "Expected at least one bottle view.");

            var bottle = new Bottle(new ColorId?[4]
            {
                ColorId.Red,
                ColorId.Red,
                ColorId.Blue,
                ColorId.Blue
            });

            bottleViews[0].SetPresentation3DEnabled(true);
            bottleViews[0].SetLevelMaxCapacity(4);
            bottleViews[0].Render(bottle);
            yield return null;

            var view3D = GetBottle3DViewComponent(bottleViews[0]);
            Assert.NotNull(view3D, "Bottle3DView missing.");
            InvokeBottle3DSetLevelMaxCapacity(view3D, 4);
            InvokeBottle3DRender(view3D, bottle);
            yield return null;

            float[] expectedFillMins = { 0f, 0.5f };
            float[] expectedFillMaxs = { 0.5f, 1f };
            float expectedTopSurface = 1f;

            var liquidRoot = GetWorldRootTransform(view3D)?.Find("LiquidLayers");
            Assert.NotNull(liquidRoot, "LiquidLayers root missing.");
            Assert.AreEqual(2, liquidRoot.childCount, "Rendered liquid layer count changed unexpectedly.");

            for (int i = 0; i < liquidRoot.childCount; i++)
            {
                var renderer = liquidRoot.GetChild(i).GetComponent<MeshRenderer>();
                Assert.NotNull(renderer, $"Liquid layer renderer missing at index {i}.");

                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                Assert.AreEqual(expectedFillMins[i], block.GetFloat(PropLayer0Min), 0.0001f,
                    $"Layer {i} FillMin drifted.");
                Assert.AreEqual(expectedFillMaxs[i], block.GetFloat(PropLayer0Max), 0.0001f,
                    $"Layer {i} FillMax drifted.");
                Assert.AreEqual(expectedTopSurface, block.GetFloat(PropTotalFill), 0.0001f,
                    $"Layer {i} TotalFill drifted.");
            }
        }

        private static IEnumerator BootstrapThreeRowBoard(Action<GameController, List<Component>> onReady)
        {
            SceneBootstrap.EnsureScene();
            yield return null;
            yield return null;

            var controller = ResolvePrimaryController();
            Assert.NotNull(controller, "GameController not found.");

            Force3DPresentation(controller);
            controller.LoadLevel(36, 192731);
            yield return null;
            yield return null;
            yield return new WaitForSeconds(0.1f);

            var views = ResolveActiveBottle3DViews(controller);
            Assert.That(views.Count, Is.GreaterThan(6), "Expected a three-row board after bootstrap.");
            onReady?.Invoke(controller, views);
        }

        private static GameController ResolvePrimaryController()
        {
            var controllers = UnityEngine.Object.FindObjectsByType<GameController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            return controllers.Length > 0 ? controllers[0] : null;
        }

        private static List<BottleView> Force3DPresentation(GameController controller)
        {
            var bottleViews = GetPrivateField<List<BottleView>>(controller, "bottleViews") ?? new List<BottleView>();
            var bottle3DViewType = ResolveBottle3DViewType();
            Assert.NotNull(bottle3DViewType, "Bottle3DView type could not be resolved.");
            var palette = UnityEngine.Object.FindFirstObjectByType<ColorPalette>();
            if (palette != null)
            {
                SetPrivateField(controller, "_colorPalette", palette);
            }

            var bottle3DViews = CreateBottle3DViewList(bottle3DViewType);
            for (int i = 0; i < bottleViews.Count; i++)
            {
                var bottleView = bottleViews[i];
                if (bottleView == null)
                {
                    bottle3DViews.Add(null);
                    continue;
                }

                bottleView.SetPresentation3DEnabled(true);
                var bottle3DView = bottleView.GetComponent(bottle3DViewType);
                if (bottle3DView == null)
                {
                    bottle3DView = bottleView.gameObject.AddComponent(bottle3DViewType);
                }

                bottle3DViews.Add(bottle3DView);
            }

            SetPrivateField(controller, "_bottle3DViews", bottle3DViews);
            return bottleViews;
        }

        private static List<Component> ResolveActiveBottle3DViews(GameController controller)
        {
            var bottleViews = GetPrivateField<List<BottleView>>(controller, "bottleViews") ?? new List<BottleView>();
            var active = new List<Component>(bottleViews.Count);
            for (int i = 0; i < bottleViews.Count; i++)
            {
                var view = GetBottle3DViewComponent(bottleViews[i]);
                if (view is Behaviour behaviour && behaviour.isActiveAndEnabled && GetWorldRootTransform(view) != null)
                {
                    active.Add(view);
                }
            }

            return active;
        }

        private static List<Bounds> CollectGlassBounds(List<Component> views)
        {
            var bounds = new List<Bounds>(views.Count);
            for (int i = 0; i < views.Count; i++)
            {
                var renderer = ResolveGlassRenderer(views[i]);
                if (renderer != null)
                {
                    bounds.Add(renderer.bounds);
                }
            }

            return bounds;
        }

        private static MeshRenderer ResolveGlassRenderer(Component view)
        {
            var worldRoot = GetWorldRootTransform(view);
            if (view == null || worldRoot == null)
            {
                return null;
            }

            return worldRoot.Find("GlassBody")?.GetComponent<MeshRenderer>();
        }

        private static List<Rect> CollectBottleScreenRects(List<Component> views, Camera camera)
        {
            var rects = new List<Rect>(views.Count);
            for (int i = 0; i < views.Count; i++)
            {
                var renderer = ResolveGlassRenderer(views[i]);
                if (renderer == null)
                {
                    continue;
                }

                if (TryProjectBoundsToScreenRect(camera, renderer.bounds, out var rect))
                {
                    rects.Add(rect);
                }
            }

            return rects;
        }

        private static List<Rect> CollectBottleViewScreenRects(List<BottleView> views, Camera camera)
        {
            var rects = new List<Rect>(views.Count);
            for (int i = 0; i < views.Count; i++)
            {
                var rectTransform = views[i] != null ? views[i].GetComponent<RectTransform>() : null;
                if (rectTransform == null || !rectTransform.gameObject.activeInHierarchy)
                {
                    continue;
                }

                rects.Add(ResolveScreenRect(rectTransform, camera));
            }

            return rects;
        }

        private static bool TryProjectBoundsToScreenRect(Camera camera, Bounds bounds, out Rect rect)
        {
            rect = default;
            if (camera == null)
            {
                return false;
            }

            var corners = new[]
            {
                new Vector3(bounds.min.x, bounds.min.y, bounds.min.z),
                new Vector3(bounds.min.x, bounds.min.y, bounds.max.z),
                new Vector3(bounds.min.x, bounds.max.y, bounds.min.z),
                new Vector3(bounds.min.x, bounds.max.y, bounds.max.z),
                new Vector3(bounds.max.x, bounds.min.y, bounds.min.z),
                new Vector3(bounds.max.x, bounds.min.y, bounds.max.z),
                new Vector3(bounds.max.x, bounds.max.y, bounds.min.z),
                new Vector3(bounds.max.x, bounds.max.y, bounds.max.z)
            };

            float minX = float.MaxValue;
            float minY = float.MaxValue;
            float maxX = float.MinValue;
            float maxY = float.MinValue;
            bool any = false;

            for (int i = 0; i < corners.Length; i++)
            {
                Vector3 projected = camera.WorldToScreenPoint(corners[i]);
                if (projected.z <= 0f)
                {
                    continue;
                }

                any = true;
                minX = Mathf.Min(minX, projected.x);
                minY = Mathf.Min(minY, projected.y);
                maxX = Mathf.Max(maxX, projected.x);
                maxY = Mathf.Max(maxY, projected.y);
            }

            if (!any || maxX <= minX || maxY <= minY)
            {
                return false;
            }

            rect = Rect.MinMaxRect(minX, minY, maxX, maxY);
            return true;
        }

        private static Rect ResolveScreenRect(RectTransform rectTransform, Camera camera)
        {
            var corners = new Vector3[4];
            rectTransform.GetWorldCorners(corners);
            float minX = float.MaxValue;
            float minY = float.MaxValue;
            float maxX = float.MinValue;
            float maxY = float.MinValue;

            for (int i = 0; i < corners.Length; i++)
            {
                var screenPoint = RectTransformUtility.WorldToScreenPoint(camera, corners[i]);
                minX = Mathf.Min(minX, screenPoint.x);
                minY = Mathf.Min(minY, screenPoint.y);
                maxX = Mathf.Max(maxX, screenPoint.x);
                maxY = Mathf.Max(maxY, screenPoint.y);
            }

            return Rect.MinMaxRect(minX, minY, maxX, maxY);
        }

        private static List<List<Component>> GroupViewsByColumn(List<Component> views)
        {
            var ordered = views.OrderBy(view => GetWorldRootTransform(view).position.x).ToList();
            var columns = new List<List<Component>>
            {
                new List<Component>(),
                new List<Component>(),
                new List<Component>()
            };

            for (int i = 0; i < ordered.Count; i++)
            {
                columns[Mathf.Min(i / 3, 2)].Add(ordered[i]);
            }

            return columns;
        }

        private static System.Type ResolveBottle3DViewType()
        {
            return System.AppDomain.CurrentDomain
                .GetAssemblies()
                .Select(assembly => assembly.GetType("Decantra.Presentation.View3D.Bottle3DView", false))
                .FirstOrDefault(type => type != null);
        }

        private static IList CreateBottle3DViewList(System.Type bottle3DViewType)
        {
            var listType = typeof(List<>).MakeGenericType(bottle3DViewType);
            return (IList)System.Activator.CreateInstance(listType);
        }

        private static Component GetBottle3DViewComponent(BottleView bottleView)
        {
            if (bottleView == null)
            {
                return null;
            }

            var bottle3DViewType = ResolveBottle3DViewType();
            return bottle3DViewType != null ? bottleView.GetComponent(bottle3DViewType) : null;
        }

        private static Transform GetWorldRootTransform(Component bottle3DView)
        {
            if (bottle3DView == null)
            {
                return null;
            }

            var property = bottle3DView.GetType().GetProperty("WorldRootTransform", BindingFlags.Instance | BindingFlags.Public);
            return property?.GetValue(bottle3DView) as Transform;
        }

        private static void InvokeBottle3DSetLevelMaxCapacity(Component bottle3DView, int capacity)
        {
            if (bottle3DView == null)
            {
                return;
            }

            var method = bottle3DView.GetType().GetMethod("SetLevelMaxCapacity", BindingFlags.Instance | BindingFlags.Public);
            method?.Invoke(bottle3DView, new object[] { capacity });
        }

        private static void InvokeBottle3DRender(Component bottle3DView, Bottle bottle)
        {
            if (bottle3DView == null)
            {
                return;
            }

            var method = bottle3DView.GetType().GetMethod("Render", BindingFlags.Instance | BindingFlags.Public);
            method?.Invoke(bottle3DView, new object[] { bottle, null });
        }

        private static T GetPrivateField<T>(object target, string fieldName) where T : class
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            return field?.GetValue(target) as T;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            field?.SetValue(target, value);
        }
    }
}