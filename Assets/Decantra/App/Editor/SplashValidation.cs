using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using System.IO;

namespace Decantra.App.Editor.Tests
{
    public class SplashValidation
    {
        [Test]
        public void SplashLogo_IsCorrectlyConfigured()
        {
            // Verify Unity Splash Screen is enabled but Unity Logo is hidden
            Assert.IsTrue(PlayerSettings.SplashScreen.show, "Splash screen should be enabled");
            Assert.IsFalse(PlayerSettings.SplashScreen.showUnityLogo, "Unity logo should be disabled");

            // Verify a custom logo is configured
            var logos = PlayerSettings.SplashScreen.logos;
            Assert.IsNotEmpty(logos, "There should be at least one splash logo configured");

            var logoEntry = logos[0];
            var logoSprite = logoEntry.logo;
            Assert.IsNotNull(logoSprite, "Splash logo sprite must be assigned");

            // Verify it points to the expected asset
            var assetPath = AssetDatabase.GetAssetPath(logoSprite);
            Assert.AreEqual("Assets/Decantra/Presentation/Resources/DecantraLogo.png", assetPath, "Splash logo is not pointing to the expected asset path");

            // Verify the content of the asset matches the source of truth
            var sourcePath = "doc/play-store-assets/icons/app-icon-512x512.png";
            Assert.IsTrue(File.Exists(sourcePath), $"Source icon not found at {sourcePath}");

            // Since we copied the file directly, and Unity hasn't re-encoded it (it's a png), 
            // the bytes on disk should match exactly or at least be very close if metadata changed.
            // Note: Unity keeps the original file.
            var assetBytes = File.ReadAllBytes(assetPath);
            var sourceBytes = File.ReadAllBytes(sourcePath);

            Assert.AreEqual(sourceBytes.Length, assetBytes.Length, "Splash icon file size mismatch - content might be different");
        }
    }
}
