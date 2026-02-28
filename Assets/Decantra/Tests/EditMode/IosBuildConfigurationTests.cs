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
    public sealed class IosBuildConfigurationTests
    {
        private static string ProjectRoot => FindProjectRoot();

        [Test]
        public void IosBuild_UsesDecantraProductName()
        {
            string iosBuildPath = Path.Combine(ProjectRoot, "Assets", "Decantra", "App", "Editor", "IosBuild.cs");
            Assert.That(File.Exists(iosBuildPath), $"Missing file: {iosBuildPath}");

            string source = File.ReadAllText(iosBuildPath);
            StringAssert.Contains("private const string IosProductName = \"Decantra\";", source);
            StringAssert.DoesNotContain("PlayerSettings.productName = \"Cantra\";", source);
        }

        [Test]
        public void IosBuild_EnforcesDisplayNameInInfoPlist()
        {
            string iosBuildPath = Path.Combine(ProjectRoot, "Assets", "Decantra", "App", "Editor", "IosBuild.cs");
            Assert.That(File.Exists(iosBuildPath), $"Missing file: {iosBuildPath}");

            string source = File.ReadAllText(iosBuildPath);
            StringAssert.Contains("SetPlistString(dict, \"CFBundleDisplayName\", displayName);", source);
            StringAssert.Contains("SetPlistString(dict, \"CFBundleName\", displayName);", source);
        }

        [Test]
        public void ProjectSettings_ProductName_IsDecantra()
        {
            string projectSettingsPath = Path.Combine(ProjectRoot, "ProjectSettings", "ProjectSettings.asset");
            Assert.That(File.Exists(projectSettingsPath), $"Missing file: {projectSettingsPath}");

            string settings = File.ReadAllText(projectSettingsPath);
            StringAssert.Contains("productName: Decantra", settings);
            StringAssert.DoesNotContain("productName: Cantra", settings);
        }

        [Test]
        public void AudioManager_ContainsIosAudioSessionConfiguration()
        {
            string audioManagerPath = Path.Combine(ProjectRoot, "Assets", "Decantra", "Presentation", "Runtime", "AudioManager.cs");
            Assert.That(File.Exists(audioManagerPath), $"Missing file: {audioManagerPath}");

            string source = File.ReadAllText(audioManagerPath);
            StringAssert.Contains("[DllImport(\"__Internal\")]", source);
            StringAssert.Contains("private static extern bool DecantraConfigureAudioSession(bool forcePlaybackCategory);", source);
            StringAssert.Contains("private static void ConfigureIosAudioSessionIfNeeded()", source);
            StringAssert.Contains("ConfigureIosAudioSessionIfNeeded();", source);
        }

        [Test]
        public void IosNativeAudioPlugin_Exists()
        {
            string pluginPath = Path.Combine(ProjectRoot, "Assets", "Plugins", "iOS", "DecantraAudioSession.mm");
            Assert.That(File.Exists(pluginPath), $"Missing file: {pluginPath}");

            string source = File.ReadAllText(pluginPath);
            StringAssert.Contains("DecantraConfigureAudioSession", source);
            StringAssert.Contains("AVAudioSession", source);
            StringAssert.Contains("AVAudioSessionCategoryPlayback", source);
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
