using System.Collections;
using Decantra.Presentation;
using Decantra.Presentation.Controller;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Decantra.PlayMode.Tests
{
    public class BackgroundOrderingTests
    {
        [UnityTest]
        public IEnumerator ValidateBackgroundLayerOrdering()
        {
            // 1. Load Main Scene and wait for Bootstrap
            SceneManager.LoadScene("Main");
            yield return null; // Wait for Awake/Start

            // Explicitly ensure scene wiring
            SceneBootstrap.EnsureScene();
            yield return null; // Wait for Bootstrap

            // 2. Identify Cameras
            var camBackground = GameObject.Find("Camera_Background")?.GetComponent<Camera>();
            var camGame = GameObject.Find("Camera_Game")?.GetComponent<Camera>();
            var camUI = GameObject.Find("Camera_UI")?.GetComponent<Camera>();

            Assert.IsNotNull(camBackground, "Camera_Background missing");
            Assert.IsNotNull(camGame, "Camera_Game missing");
            Assert.IsNotNull(camUI, "Camera_UI missing");

            // 3. Verify Depths
            Assert.Less(camBackground.depth, camGame.depth, "Background camera must have lower depth than Game camera");
            Assert.Less(camGame.depth, camUI.depth, "Game camera must have lower depth than UI camera");

            // 4. Verify Background Canvas
            var canvasBackgroundGo = GameObject.Find("Canvas_Background");
            // SceneBootstrap might assign backgroundImage parent to existing specific canvas
            // Logic in SceneBootstrap: "var backgroundCanvas = CreateCanvas("Canvas_Background", cameras.Background...)"

            if (canvasBackgroundGo == null)
            {
                // sometimes naming or creation order might differ, try to find by camera association
                // iterate root objects
                foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects())
                {
                    var c = go.GetComponent<Canvas>();
                    if (c != null && c.worldCamera == camBackground)
                    {
                        canvasBackgroundGo = go;
                        break;
                    }
                }
            }

            Assert.IsNotNull(canvasBackgroundGo, "Canvas_Background (or root of Background) missing");
            var canvasBg = canvasBackgroundGo.GetComponent<Canvas>();
            Assert.IsNotNull(canvasBg, "Background object is not a Canvas or inside one");

            Assert.AreEqual(RenderMode.ScreenSpaceCamera, canvasBg.renderMode, "Background Canvas must be ScreenSpaceCamera");
            Assert.AreEqual(camBackground, canvasBg.worldCamera, "Background Canvas must use Camera_Background");

            // 5. Verify Scaling Fix (Gate D Extension)
            // We expect the Background GameObject (holding the Image) to have increased scale if the fix is active.

            // Wait for GameController to apply visuals
            yield return new WaitForSeconds(0.5f);

            var backgroundObj = GameObject.Find("Background");
            // Name set in SceneBootstrap: CreateUiChild(parent, "Background")

            // Search inside canvas if Global Find failed
            if (backgroundObj == null)
            {
                var t = canvasBackgroundGo.transform.Find("Background");
                if (t != null) backgroundObj = t.gameObject;
            }

            Assert.IsNotNull(backgroundObj, "Background Image object missing");

            // Check Scale - expect 2.5
            var scale = backgroundObj.transform.localScale;
            Debug.Log($"Background Scale: {scale}");

            Assert.That(scale.x, Is.GreaterThanOrEqualTo(2.4f), "Background Image X scale should be fixed to ~2.5");
            Assert.That(scale.y, Is.GreaterThanOrEqualTo(2.4f), "Background Image Y scale should be fixed to ~2.5");
        }
    }
}
