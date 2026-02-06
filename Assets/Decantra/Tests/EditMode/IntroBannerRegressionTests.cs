/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System;
using System.IO;
using NUnit.Framework;

namespace Decantra.Tests.EditMode
{
    /// <summary>
    /// Regression guard: the IntroBanner's background must start transparent.
    /// If it starts opaque (alpha = 1), the full-screen black overlay permanently
    /// hides all gameplay content on Android because Play() is only called in
    /// screenshot-capture mode.
    /// </summary>
    public sealed class IntroBannerRegressionTests
    {
        private static string ProjectRoot => FindProjectRoot();

        [Test]
        public void IntroBanner_BackgroundImage_StartsTransparent()
        {
            string bootstrapPath = Path.Combine(ProjectRoot, "Assets", "Decantra",
                "Presentation", "Runtime", "SceneBootstrap.cs");
            string content = File.ReadAllText(bootstrapPath);

            // Locate the CreateIntroBanner method
            int methodStart = content.IndexOf("CreateIntroBanner(Transform parent)", StringComparison.Ordinal);
            Assert.IsTrue(methodStart >= 0, "CreateIntroBanner method not found in SceneBootstrap.cs");

            // Extract the method body (up to next private/static method boundary)
            string methodBody = content.Substring(methodStart, Math.Min(1500, content.Length - methodStart));

            // The background color assignment must have alpha = 0
            Assert.IsTrue(
                methodBody.Contains("new Color(0f, 0f, 0f, 0f)"),
                "IntroBanner background must be created with alpha 0 (transparent). " +
                "Creating it with alpha 1 causes a permanent black overlay on Android " +
                "because Play() is only called in screenshot-capture mode.");

            // Ensure it does NOT have alpha = 1
            Assert.IsFalse(
                methodBody.Contains("new Color(0f, 0f, 0f, 1f)"),
                "IntroBanner background must NOT start with alpha 1 (opaque black). " +
                "This causes the Android black screen regression.");
        }

        [Test]
        public void IntroBanner_PrepareForIntro_SetsBackgroundOpaque()
        {
            // Verify that PrepareForIntro (called by Play) still sets alpha to 1
            // so the screenshot capture flow works correctly.
            string bannerPath = Path.Combine(ProjectRoot, "Assets", "Decantra",
                "Presentation", "Runtime", "IntroBanner.cs");
            string content = File.ReadAllText(bannerPath);

            int prepareStart = content.IndexOf("PrepareForIntro()", StringComparison.Ordinal);
            Assert.IsTrue(prepareStart >= 0, "PrepareForIntro method not found in IntroBanner.cs");

            string afterPrepare = content.Substring(prepareStart, Math.Min(500, content.Length - prepareStart));
            Assert.IsTrue(
                afterPrepare.Contains("SetBackgroundAlpha(1f)"),
                "PrepareForIntro must set background alpha to 1 so the intro fade sequence works in screenshot mode.");
        }

        [Test]
        public void IntroBanner_Play_EndsTransparent()
        {
            // Verify that Play() ends by setting background alpha to 0
            string bannerPath = Path.Combine(ProjectRoot, "Assets", "Decantra",
                "Presentation", "Runtime", "IntroBanner.cs");
            string content = File.ReadAllText(bannerPath);

            int playStart = content.IndexOf("IEnumerator Play()", StringComparison.Ordinal);
            Assert.IsTrue(playStart >= 0, "Play method not found in IntroBanner.cs");

            string playBody = content.Substring(playStart, Math.Min(2000, content.Length - playStart));
            Assert.IsTrue(
                playBody.Contains("SetBackgroundAlpha(0f)"),
                "Play() must end by setting background alpha to 0 so the overlay is removed after the intro sequence.");
        }

        private static string FindProjectRoot()
        {
            var current = new DirectoryInfo(Directory.GetCurrentDirectory());
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

            throw new InvalidOperationException("Unable to locate project root.");
        }
    }
}
