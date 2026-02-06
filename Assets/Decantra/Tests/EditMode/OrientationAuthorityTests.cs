/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Decantra.Tests.EditMode
{
    public class OrientationAuthorityTests
    {
        private static string ProjectRoot => FindProjectRoot();

        [Test]
        public void PlayerSettings_LockPortraitOnly()
        {
            string settingsPath = Path.Combine(ProjectRoot, "ProjectSettings", "ProjectSettings.asset");
            string content = File.ReadAllText(settingsPath);

            AssertSetting(settingsPath, content, "defaultScreenOrientation", 0);
            AssertSetting(settingsPath, content, "allowedAutorotateToPortrait", 1);
            AssertSetting(settingsPath, content, "allowedAutorotateToPortraitUpsideDown", 0);
            AssertSetting(settingsPath, content, "allowedAutorotateToLandscapeRight", 0);
            AssertSetting(settingsPath, content, "allowedAutorotateToLandscapeLeft", 0);
            AssertSetting(settingsPath, content, "useOSAutorotation", 0);
        }

        [Test]
        public void PlayerSettings_AndroidAutoRotationBehavior_IsExplicit()
        {
            string settingsPath = Path.Combine(ProjectRoot, "ProjectSettings", "ProjectSettings.asset");
            string content = File.ReadAllText(settingsPath);

            AssertSetting(settingsPath, content, "androidAutoRotationBehavior", 1);
        }

        [Test]
        public void AndroidManifest_DoesNotOverrideScreenOrientation()
        {
            string manifestPath = Path.Combine(ProjectRoot, "Assets", "Plugins", "Android", "AndroidManifest.xml");
            if (!File.Exists(manifestPath))
            {
                Assert.Ignore("Custom AndroidManifest.xml not present.");
            }

            string content = File.ReadAllText(manifestPath);
            StringAssert.DoesNotContain("android:screenOrientation", content, "Custom AndroidManifest.xml must not set android:screenOrientation.");
        }

        [Test]
        public void Runtime_DoesNotForceScreenOrientation()
        {
            string bootstrapPath = Path.Combine(ProjectRoot, "Assets", "Decantra", "Presentation", "Runtime", "SceneBootstrap.cs");
            string content = File.ReadAllText(bootstrapPath);
            StringAssert.DoesNotContain("Screen.orientation", content, "Runtime code must not force Screen.orientation.");
        }

        private static void AssertSetting(string path, string content, string key, int expected)
        {
            var match = Regex.Match(content, "^\\s*" + Regex.Escape(key) + ":\\s*(\\S+)\\s*$", RegexOptions.Multiline);
            Assert.IsTrue(match.Success, $"Missing setting '{key}' in {path}.");
            Assert.AreEqual(expected.ToString(), match.Groups[1].Value, $"Setting '{key}' in {path} should be {expected}.");
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

            throw new InvalidOperationException("Unable to locate project root (missing ProjectSettings/ProjectSettings.asset).");
        }
    }
}
