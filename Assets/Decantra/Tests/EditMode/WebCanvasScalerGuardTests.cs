/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Decantra.Tests.EditMode
{
    /// <summary>
    /// Static code analysis tests that prove the WebGL canvas-scaler fix is fully
    /// isolated behind <c>#if UNITY_WEBGL &amp;&amp; !UNITY_EDITOR</c> preprocessor guards.
    ///
    /// These tests verify that no WebGL-specific code path can ever execute on
    /// Android or iOS, providing hard evidence that the fix does not affect mobile layout.
    ///
    /// All assertions are textual / structural — they do not require the WebGL platform
    /// to be active and run successfully on all platforms including Editor.
    /// </summary>
    public sealed class WebCanvasScalerGuardTests
    {
        private static string ProjectRoot => OrientationAuthorityTests_FindProjectRoot();

        // ─── WebCanvasScalerController.cs structural guards ─────────────────────────

        [Test]
        public void WebCanvasScalerController_FileIsEntirelyInsideWebGLGuard()
        {
            string path = Path.Combine(ProjectRoot,
                "Assets", "Decantra", "Presentation", "View", "WebCanvasScalerController.cs");
            Assert.IsTrue(File.Exists(path), $"WebCanvasScalerController.cs not found at: {path}");

            string src = File.ReadAllText(path);

            // The code block (after license header) must open with the WebGL guard.
            // We accept optional leading blank lines and the license comment.
            var firstIfMatch = Regex.Match(src, @"#if\s+UNITY_WEBGL\s*&&\s*!UNITY_EDITOR");
            Assert.IsTrue(firstIfMatch.Success,
                "WebCanvasScalerController.cs must contain '#if UNITY_WEBGL && !UNITY_EDITOR'.");

            // The guard must be the first preprocessor directive in the file.
            var firstDirective = Regex.Match(src, @"^\s*#\S+", RegexOptions.Multiline);
            Assert.IsTrue(firstDirective.Success);
            StringAssert.Contains("UNITY_WEBGL", firstDirective.Value.Replace("\r", "").Replace("\n", "") + src.Substring(firstDirective.Index, 60),
                "The first preprocessor directive in WebCanvasScalerController.cs must reference UNITY_WEBGL.");

            // The file must end with a matching #endif.
            Assert.IsTrue(src.TrimEnd().EndsWith("#endif"),
                "WebCanvasScalerController.cs must end with '#endif' closing the UNITY_WEBGL guard.");
        }

        [Test]
        public void WebCanvasScalerController_ClassBody_OnlyExistsInsideGuard()
        {
            string path = Path.Combine(ProjectRoot,
                "Assets", "Decantra", "Presentation", "View", "WebCanvasScalerController.cs");
            string src = File.ReadAllText(path);

            // The class declaration must appear after the #if guard.
            int guardPos = src.IndexOf("#if UNITY_WEBGL");
            int classPos = src.IndexOf("class WebCanvasScalerController");
            Assert.Greater(guardPos, -1, "Guard not found.");
            Assert.Greater(classPos, -1, "Class declaration not found.");
            Assert.Greater(classPos, guardPos,
                "WebCanvasScalerController class must appear AFTER the #if UNITY_WEBGL guard.");
        }

        // ─── SceneBootstrap.cs isolation guards ──────────────────────────────────────

        [Test]
        public void SceneBootstrap_AllWebCanvasScalerControllerReferences_InsideWebGLGuard()
        {
            string path = Path.Combine(ProjectRoot,
                "Assets", "Decantra", "Presentation", "Runtime", "SceneBootstrap.cs");
            Assert.IsTrue(File.Exists(path), $"SceneBootstrap.cs not found at: {path}");

            string src = File.ReadAllText(path);

            // Find every occurrence of WebCanvasScalerController in SceneBootstrap.
            var matches = Regex.Matches(src, @"WebCanvasScalerController");
            Assert.Greater(matches.Count, 0, "SceneBootstrap.cs must reference WebCanvasScalerController.");

            foreach (Match match in matches)
            {
                // Walk backwards from the match position to find the nearest enclosing
                // #if / #elif / #endif block.
                string preceding = src.Substring(0, match.Index);
                bool insideGuard = IsInsideWebGlIfBlock(preceding);
                Assert.IsTrue(insideGuard,
                    $"Reference to WebCanvasScalerController at offset {match.Index} is not inside a " +
                    "'#if UNITY_WEBGL && !UNITY_EDITOR' block in SceneBootstrap.cs.");
            }
        }

        [Test]
        public void SceneBootstrap_EnsureWebCanvasControllers_InsideWebGLGuard()
        {
            string path = Path.Combine(ProjectRoot,
                "Assets", "Decantra", "Presentation", "Runtime", "SceneBootstrap.cs");
            string src = File.ReadAllText(path);

            int methodPos = src.IndexOf("EnsureWebCanvasControllers");
            Assert.Greater(methodPos, -1,
                "EnsureWebCanvasControllers method must exist in SceneBootstrap.cs.");

            string preceding = src.Substring(0, methodPos);
            Assert.IsTrue(IsInsideWebGlIfBlock(preceding),
                "EnsureWebCanvasControllers must be declared inside a '#if UNITY_WEBGL && !UNITY_EDITOR' block.");
        }

        [Test]
        public void SceneBootstrap_CreateCanvas_DoesNotCallAddComponentOutsideWebGLGuard()
        {
            string path = Path.Combine(ProjectRoot,
                "Assets", "Decantra", "Presentation", "Runtime", "SceneBootstrap.cs");
            string src = File.ReadAllText(path);

            // Find the CreateCanvas method body (ends before next "private static" at the same indent).
            int createCanvasStart = src.IndexOf("private static Canvas CreateCanvas(");
            Assert.Greater(createCanvasStart, -1);

            // Extract up to next private static method to isolate the body.
            int nextMethodPos = src.IndexOf("private static ", createCanvasStart + 10);
            string methodBody = nextMethodPos > createCanvasStart
                ? src.Substring(createCanvasStart, nextMethodPos - createCanvasStart)
                : src.Substring(createCanvasStart);

            // Any AddComponent call in CreateCanvas that touches WebCanvasScalerController
            // must be inside a #if block.
            var addCalls = Regex.Matches(methodBody, @"AddComponent<.*WebCanvasScalerController.*>");
            foreach (Match m in addCalls)
            {
                string precedingInMethod = methodBody.Substring(0, m.Index);
                Assert.IsTrue(IsInsideWebGlIfBlock(precedingInMethod),
                    $"AddComponent<WebCanvasScalerController> in CreateCanvas at local offset {m.Index} is not guarded by #if UNITY_WEBGL.");
            }
        }

        // ─── CanvasScaler defaults are unaffected ─────────────────────────────────────

        [Test]
        public void SceneBootstrap_CreateCanvas_SetsReferenceResolution_1080x1920()
        {
            string path = Path.Combine(ProjectRoot,
                "Assets", "Decantra", "Presentation", "Runtime", "SceneBootstrap.cs");
            string src = File.ReadAllText(path);

            // The canonical reference resolution assignment must exist.
            Assert.IsTrue(
                src.Contains("scaler.referenceResolution = new Vector2(1080, 1920)"),
                "SceneBootstrap.CreateCanvas must assign referenceResolution = new Vector2(1080, 1920).");
        }

        [Test]
        public void SceneBootstrap_CreateCanvas_DoesNotSetMatchWidthOrHeightOutsideWebGLGuard()
        {
            string path = Path.Combine(ProjectRoot,
                "Assets", "Decantra", "Presentation", "Runtime", "SceneBootstrap.cs");
            string src = File.ReadAllText(path);

            // Find all assignments to matchWidthOrHeight.
            var assignments = Regex.Matches(src, @"scaler\.matchWidthOrHeight\s*=");
            foreach (Match m in assignments)
            {
                string preceding = src.Substring(0, m.Index);
                if (IsInsideWebGlIfBlock(preceding))
                {
                    // This is a WebGL-only assignment — safe.
                    continue;
                }

                // Any assignment outside a WebGL guard would change Android/iOS behaviour.
                Assert.Fail(
                    $"scaler.matchWidthOrHeight assignment at offset {m.Index} is outside a " +
                    "'#if UNITY_WEBGL && !UNITY_EDITOR' block. This would affect Android/iOS.");
            }
        }

        // ─── Helper ──────────────────────────────────────────────────────────────────

        /// <summary>
        /// Determines whether the given source prefix (everything before a target position)
        /// is currently inside an open <c>#if UNITY_WEBGL &amp;&amp; !UNITY_EDITOR</c> block.
        /// Uses a simple counter: increment on matching #if, decrement on #endif.
        /// Note: this does not handle nested unrelated #if blocks, but is sufficient
        /// to verify top-level UNITY_WEBGL guards.
        /// </summary>
        private static bool IsInsideWebGlIfBlock(string preceding)
        {
            int depth = 0;
            foreach (Match m in Regex.Matches(preceding, @"#(if|elif|else|endif)\b[^\n]*", RegexOptions.Multiline))
            {
                string directive = m.Value.TrimStart();
                if (directive.StartsWith("#if") && directive.Contains("UNITY_WEBGL"))
                {
                    depth++;
                }
                else if (directive.StartsWith("#endif"))
                {
                    if (depth > 0) depth--;
                }
                else if ((directive.StartsWith("#else") || directive.StartsWith("#elif")) && depth > 0)
                {
                    // #else/#elif closes the positive branch — treat as exiting the guard.
                    depth--;
                }
            }

            return depth > 0;
        }

        private static string OrientationAuthorityTests_FindProjectRoot()
        {
            var current = new System.IO.DirectoryInfo(Directory.GetCurrentDirectory());
            while (current != null)
            {
                string settingsPath = Path.Combine(current.FullName, "ProjectSettings", "ProjectSettings.asset");
                string assetsPath = Path.Combine(current.FullName, "Assets");
                if (File.Exists(settingsPath) && Directory.Exists(assetsPath))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }

            return Directory.GetCurrentDirectory();
        }
    }
}
