/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System.Collections;
using System.Reflection;
using Decantra.Domain.Model;
using Decantra.Presentation.Controller;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Decantra.PlayMode.Tests
{
    /// <summary>
    /// PlayMode tests for the Options overlay and starfield configuration controls.
    /// </summary>
    public sealed class StarfieldOptionsTests
    {
        private GameController _controller;
        private GameObject _starsGo;
        private Material _starsMaterial;
        private GameObject _optionsOverlay;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            var controllerGo = new GameObject("TestController");
            _controller = controllerGo.AddComponent<GameController>();

            _starsGo = new GameObject("BackgroundStars");
            _starsGo.SetActive(true);

            _starsMaterial = new Material(Shader.Find("UI/Default") ?? Shader.Find("Sprites/Default"));
            // Set shader property IDs manually since test shader may not have them
            SetPrivateField(_controller, "backgroundStars", _starsGo);
            SetPrivateField(_controller, "_starfieldMaterial", _starsMaterial);
            SetPrivateField(_controller, "_starfieldConfig", StarfieldConfig.Default);

            // Create a minimal options overlay
            _optionsOverlay = new GameObject("OptionsOverlay");
            _optionsOverlay.SetActive(false);
            SetPrivateField(_controller, "_optionsOverlay", _optionsOverlay);

            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_controller != null) Object.Destroy(_controller.gameObject);
            if (_starsGo != null) Object.Destroy(_starsGo);
            if (_starsMaterial != null) Object.Destroy(_starsMaterial);
            if (_optionsOverlay != null) Object.Destroy(_optionsOverlay);
            yield return null;
        }

        // --- Overlay open/close ---

        [UnityTest]
        public IEnumerator ShowOptionsOverlay_ActivatesOverlay()
        {
            Assert.IsFalse(_controller.IsOptionsOverlayVisible);
            _controller.ShowOptionsOverlay();
            yield return null;
            Assert.IsTrue(_controller.IsOptionsOverlayVisible);
        }

        [UnityTest]
        public IEnumerator HideOptionsOverlay_DeactivatesOverlay()
        {
            _controller.ShowOptionsOverlay();
            yield return null;
            _controller.HideOptionsOverlay();
            yield return null;
            Assert.IsFalse(_controller.IsOptionsOverlayVisible);
        }

        [UnityTest]
        public IEnumerator OverlayOpenClose_DoesNotAffectGameState()
        {
            bool lockedBefore = _controller.IsInputLocked;
            bool sfxBefore = _controller.IsSfxEnabled;
            bool hasLevelBefore = _controller.HasActiveLevel;

            _controller.ShowOptionsOverlay();
            yield return null;
            _controller.HideOptionsOverlay();
            yield return null;

            Assert.AreEqual(lockedBefore, _controller.IsInputLocked, "InputLocked changed after overlay toggle");
            Assert.AreEqual(sfxBefore, _controller.IsSfxEnabled, "SfxEnabled changed after overlay toggle");
            Assert.AreEqual(hasLevelBefore, _controller.HasActiveLevel, "HasActiveLevel changed after overlay toggle");
        }

        // --- Starfield toggle ---

        [UnityTest]
        public IEnumerator SetStarfieldEnabled_False_DeactivatesStarsObject()
        {
            _controller.SetStarfieldEnabled(false);
            yield return null;
            Assert.IsFalse(_starsGo.activeSelf, "Stars should be inactive when disabled");
            Assert.IsFalse(_controller.StarfieldConfiguration.Enabled);
        }

        [UnityTest]
        public IEnumerator SetStarfieldEnabled_True_ActivatesStarsObject()
        {
            _controller.SetStarfieldEnabled(false);
            yield return null;
            _controller.SetStarfieldEnabled(true);
            yield return null;
            Assert.IsTrue(_starsGo.activeSelf, "Stars should be active when enabled");
            Assert.IsTrue(_controller.StarfieldConfiguration.Enabled);
        }

        // --- Slider changes ---

        [UnityTest]
        public IEnumerator SetStarfieldDensity_UpdatesConfig()
        {
            _controller.SetStarfieldDensity(0.80f);
            yield return null;
            Assert.AreEqual(0.80f, _controller.StarfieldConfiguration.Density, 0.001f);
        }

        [UnityTest]
        public IEnumerator SetStarfieldSpeed_UpdatesConfig()
        {
            _controller.SetStarfieldSpeed(0.15f);
            yield return null;
            Assert.AreEqual(0.15f, _controller.StarfieldConfiguration.Speed, 0.001f);
        }

        [UnityTest]
        public IEnumerator SetStarfieldBrightness_UpdatesConfig()
        {
            _controller.SetStarfieldBrightness(0.90f);
            yield return null;
            Assert.AreEqual(0.90f, _controller.StarfieldConfiguration.Brightness, 0.001f);
        }

        [UnityTest]
        public IEnumerator SetStarfieldValues_ClampsOutOfRange()
        {
            _controller.SetStarfieldDensity(-1f);
            _controller.SetStarfieldSpeed(99f);
            _controller.SetStarfieldBrightness(0f);
            yield return null;

            var config = _controller.StarfieldConfiguration;
            Assert.AreEqual(StarfieldConfig.DensityMin, config.Density, 0.001f);
            Assert.AreEqual(StarfieldConfig.SpeedMax, config.Speed, 0.001f);
            Assert.AreEqual(StarfieldConfig.BrightnessMin, config.Brightness, 0.001f);
        }

        // --- Full round-trip ---

        [UnityTest]
        public IEnumerator FullConfigChange_AllValuesApplied()
        {
            var custom = new StarfieldConfig(false, 0.20f, 0.75f, 0.30f);
            _controller.SetStarfieldConfig(custom);
            yield return null;

            Assert.IsFalse(_controller.StarfieldConfiguration.Enabled);
            Assert.AreEqual(0.20f, _controller.StarfieldConfiguration.Density, 0.001f);
            Assert.AreEqual(0.75f, _controller.StarfieldConfiguration.Speed, 0.001f);
            Assert.AreEqual(0.30f, _controller.StarfieldConfiguration.Brightness, 0.001f);
            Assert.IsFalse(_starsGo.activeSelf);
        }

        // --- Helpers ---

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field '{fieldName}' not found on {target.GetType().Name}");
            field.SetValue(target, value);
        }
    }
}
