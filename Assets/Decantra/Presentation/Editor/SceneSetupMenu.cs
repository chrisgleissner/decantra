/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System.Collections.Generic;
using Decantra.Presentation.Controller;
using Decantra.Presentation.View;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Decantra.Presentation.Editor
{
    public static class SceneSetupMenu
    {
        [MenuItem("Decantra/Setup Scene")]
        public static void SetupScene()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();

            var canvasGo = GetOrCreate("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);

            GetOrCreate("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            var hudRoot = GetOrCreateChild(canvasGo.transform, "HUD");
            var hud = GetOrCreateChild(hudRoot.transform, "HudView");
            var hudView = hud.GetComponent<HudView>() ?? hud.AddComponent<HudView>();
            var levelText = GetOrCreateText(hud.transform, "LevelText", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -40));
            var movesText = GetOrCreateText(hud.transform, "MovesText", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20, -80));
            var optimalText = GetOrCreateText(hud.transform, "OptimalText", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20, -80));
            var scoreText = GetOrCreateText(hud.transform, "ScoreText", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -120));

            AssignPrivateField(hudView, "levelText", levelText);
            AssignPrivateField(hudView, "movesText", movesText);
            AssignPrivateField(hudView, "optimalText", optimalText);
            AssignPrivateField(hudView, "scoreText", scoreText);

            var gridRoot = GetOrCreateChild(canvasGo.transform, "BottleGrid");
            var grid = gridRoot.GetComponent<GridLayoutGroup>() ?? gridRoot.AddComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;
            grid.cellSize = new Vector2(120, 300);
            grid.spacing = new Vector2(20, 20);

            var palette = GetOrCreatePaletteAsset();

            var bottleViews = new List<BottleView>();
            for (int i = 0; i < 9; i++)
            {
                var bottleGo = GetOrCreateChild(gridRoot.transform, $"Bottle_{i + 1}");
                var bottleView = bottleGo.GetComponent<BottleView>() ?? bottleGo.AddComponent<BottleView>();
                var input = bottleGo.GetComponent<BottleInput>() ?? bottleGo.AddComponent<BottleInput>();

                AssignPrivateField(bottleView, "palette", palette);

                var slotContainer = GetOrCreateChild(bottleGo.transform, "Slots");
                var slotList = new List<Image>();
                for (int s = 0; s < 4; s++)
                {
                    var slotGo = GetOrCreateChild(slotContainer.transform, $"Slot_{s + 1}");
                    var image = slotGo.GetComponent<Image>() ?? slotGo.AddComponent<Image>();
                    image.color = new Color(0, 0, 0, 0);
                    slotList.Add(image);
                }
                AssignPrivateField(bottleView, "slots", slotList);

                bottleViews.Add(bottleView);
            }

            var controllerGo = GetOrCreate("GameController", typeof(GameController));
            var controller = controllerGo.GetComponent<GameController>();
            AssignPrivateField(controller, "bottleViews", bottleViews);
            AssignPrivateField(controller, "hudView", hudView);

            foreach (var bottleView in bottleViews)
            {
                var input = bottleView.GetComponent<BottleInput>();
                AssignPrivateField(input, "bottleView", bottleView);
                AssignPrivateField(input, "controller", controller);
            }

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
        }

        private static GameObject GetOrCreate(string name, params System.Type[] components)
        {
            var existing = GameObject.Find(name);
            if (existing != null) return existing;
            var go = new GameObject(name, components);
            return go;
        }

        private static GameObject GetOrCreateChild(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child != null) return child.gameObject;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go;
        }

        private static Text GetOrCreateText(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition)
        {
            var go = GetOrCreateChild(parent, name);
            var text = go.GetComponent<Text>() ?? go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 28;
            text.alignment = TextAnchor.MiddleCenter;
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(400, 40);
            return text;
        }

        private static ColorPalette GetOrCreatePaletteAsset()
        {
            const string path = "Assets/Decantra/Presentation/ColorPalette.asset";
            var palette = AssetDatabase.LoadAssetAtPath<ColorPalette>(path);
            if (palette != null) return palette;

            palette = ScriptableObject.CreateInstance<ColorPalette>();
            AssetDatabase.CreateAsset(palette, path);
            AssetDatabase.SaveAssets();
            return palette;
        }

        private static void AssignPrivateField(Object target, string fieldName, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(fieldName);
            if (prop != null)
            {
                prop.objectReferenceValue = value;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void AssignPrivateField(Object target, string fieldName, IList<Image> list)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(fieldName);
            if (prop == null) return;
            prop.ClearArray();
            for (int i = 0; i < list.Count; i++)
            {
                prop.InsertArrayElementAtIndex(i);
                prop.GetArrayElementAtIndex(i).objectReferenceValue = list[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignPrivateField(Object target, string fieldName, IList<BottleView> list)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(fieldName);
            if (prop == null) return;
            prop.ClearArray();
            for (int i = 0; i < list.Count; i++)
            {
                prop.InsertArrayElementAtIndex(i);
                prop.GetArrayElementAtIndex(i).objectReferenceValue = list[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
