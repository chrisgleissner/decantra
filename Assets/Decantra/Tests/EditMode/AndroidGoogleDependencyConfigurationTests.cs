/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;

namespace Decantra.Tests.EditMode
{
    public sealed class AndroidGoogleDependencyConfigurationTests
    {
        private static string ProjectRoot => FindProjectRoot();

        [Test]
        public void ProjectSettings_KeepPublishedAndroidApplicationId()
        {
            string projectSettingsPath = Path.Combine(ProjectRoot, "ProjectSettings", "ProjectSettings.asset");
            Assert.That(File.Exists(projectSettingsPath), $"Missing file: {projectSettingsPath}");

            string settings = File.ReadAllText(projectSettingsPath);
            StringAssert.Contains("Android: uk.gleissner.decantra", settings);
        }

        [Test]
        public void AndroidBuild_UsesPublishedApplicationIdentifier()
        {
            string androidBuildPath = Path.Combine(ProjectRoot, "Assets", "Decantra", "App", "Editor", "AndroidBuild.cs");
            Assert.That(File.Exists(androidBuildPath), $"Missing file: {androidBuildPath}");

            string source = File.ReadAllText(androidBuildPath);
            StringAssert.Contains("PlayerSettings.applicationIdentifier = \"uk.gleissner.decantra\";", source);
        }

        [Test]
        public void AndroidManifest_RemovesGooglePermissionsAndBilling()
        {
            string manifestPath = Path.Combine(ProjectRoot, "Assets", "Plugins", "Android", "AndroidManifest.xml");
            Assert.That(File.Exists(manifestPath), $"Missing file: {manifestPath}");

            string manifest = File.ReadAllText(manifestPath);
            var document = XDocument.Parse(manifest);
            XNamespace android = "http://schemas.android.com/apk/res/android";
            XNamespace tools = "http://schemas.android.com/tools";

            var usesPermissions = document.Root?.Elements("uses-permission").ToArray() ?? Array.Empty<XElement>();
            Assert.That(
                usesPermissions.Any(
                    element => (string)element.Attribute(android + "name") == "com.google.android.gms.permission.AD_ID"
                        && (string)element.Attribute(tools + "node") == "remove"),
                "Expected AD_ID permission to be present only as a removal directive.");
            Assert.That(
                usesPermissions.Any(
                    element => (string)element.Attribute(android + "name") == "com.android.vending.BILLING"
                        && (string)element.Attribute(tools + "node") == "remove"),
                "Expected BILLING permission to be present only as a removal directive.");
            Assert.That(
                usesPermissions.Where(
                        element => (string)element.Attribute(android + "name") is "com.google.android.gms.permission.AD_ID" or "com.android.vending.BILLING")
                    .All(element => (string)element.Attribute(tools + "node") == "remove"),
                "Google-related permissions must not be actively declared in the manifest.");

            XElement queries = document.Root?.Element("queries");
            Assert.That(queries, Is.Not.Null, "Expected a <queries> node so merged package queries can be removed.");
            Assert.That((string)queries.Attribute(tools + "node"), Is.EqualTo("remove"));
        }

        [Test]
        public void AndroidGradleInputs_DoNotDeclareGoogleRuntimeDependencies()
        {
            string gradlePath = Path.Combine(ProjectRoot, "Assets", "Plugins", "Android", "mainTemplate.gradle");
            string resolverPath = Path.Combine(ProjectRoot, "ProjectSettings", "AndroidResolverDependencies.xml");

            Assert.That(File.Exists(gradlePath), $"Missing file: {gradlePath}");
            Assert.That(File.Exists(resolverPath), $"Missing file: {resolverPath}");

            string gradle = File.ReadAllText(gradlePath);
            string resolver = File.ReadAllText(resolverPath);

            StringAssert.DoesNotContain("com.google.android", gradle);
            StringAssert.DoesNotContain("com.google.gms", gradle);
            StringAssert.DoesNotContain("play-services", gradle);
            StringAssert.DoesNotContain("firebase", gradle);
            StringAssert.DoesNotContain("billingclient", gradle);

            var document = XDocument.Parse(resolver);
            XElement packages = document.Root?.Element("packages");
            XElement files = document.Root?.Element("files");

            Assert.That(packages, Is.Not.Null, "Expected a <packages> element in Android resolver settings.");
            Assert.That(files, Is.Not.Null, "Expected a <files> element in Android resolver settings.");
            Assert.That(packages.HasElements, Is.False, "Expected no resolved Android packages.");
            Assert.That(files.HasElements, Is.False, "Expected no resolved Android files.");
            Assert.That(
                resolver.IndexOf("com.google.android", StringComparison.OrdinalIgnoreCase) < 0
                && resolver.IndexOf("com.google.gms", StringComparison.OrdinalIgnoreCase) < 0
                && resolver.IndexOf("play-services", StringComparison.OrdinalIgnoreCase) < 0
                && resolver.IndexOf("firebase", StringComparison.OrdinalIgnoreCase) < 0
                && resolver.IndexOf("billing", StringComparison.OrdinalIgnoreCase) < 0,
                "Resolver settings should not reference Google, Firebase, or billing packages.");
        }

        [Test]
        public void PurchasingMetadata_DoesNotForceGooglePlayStoreSelection()
        {
            string billingModePath = Path.Combine(ProjectRoot, "Assets", "Resources", "BillingMode.json");
            string unityConnectSettingsPath = Path.Combine(ProjectRoot, "ProjectSettings", "UnityConnectSettings.asset");

            Assert.False(File.Exists(billingModePath), $"Unexpected stale billing metadata: {billingModePath}");
            Assert.That(File.Exists(unityConnectSettingsPath), $"Missing file: {unityConnectSettingsPath}");

            string unityConnectSettings = File.ReadAllText(unityConnectSettingsPath);
            StringAssert.Contains("UnityPurchasingSettings:", unityConnectSettings);
            StringAssert.Contains("m_Enabled: 0", unityConnectSettings);
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
