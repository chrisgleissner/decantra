/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using Decantra.Domain.Rules;
using NUnit.Framework;
using System.Collections.Generic;

namespace Decantra.Tests.EditMode
{
    public sealed class BackgroundRulesTests
    {
        [Test]
        public void ZoneIndex_ReturnsCorrectBoundaries()
        {
            Assert.AreEqual(0, BackgroundRules.GetZoneIndex(1));
            Assert.AreEqual(0, BackgroundRules.GetZoneIndex(9));
            Assert.AreEqual(1, BackgroundRules.GetZoneIndex(10));
            Assert.AreEqual(1, BackgroundRules.GetZoneIndex(19));
            Assert.AreEqual(2, BackgroundRules.GetZoneIndex(20));
        }

        [Test]
        public void ZoneIndex_ConsecutiveGroupsMatchSpecification()
        {
            for (int zone = 1; zone <= 100; zone++)
            {
                int firstLevel = 10 + (zone - 1) * 10;
                int lastLevel = firstLevel + 9;

                for (int level = firstLevel; level <= lastLevel; level++)
                {
                    Assert.AreEqual(zone, BackgroundRules.GetZoneIndex(level),
                        $"Level {level} should be in Zone {zone}");
                }
            }
        }

        [Test]
        public void ZoneTheme_IsDeterministicForSameZoneAndSeed()
        {
            int seed = 12345;
            for (int zoneIndex = 0; zoneIndex < 20; zoneIndex++)
            {
                var first = BackgroundRules.GetZoneTheme(zoneIndex, seed);
                var second = BackgroundRules.GetZoneTheme(zoneIndex, seed);
                Assert.AreEqual(first.LayerCount, second.LayerCount, $"Zone {zoneIndex} layerCount not deterministic");
                Assert.AreEqual(first.GeometryVocabulary, second.GeometryVocabulary, $"Zone {zoneIndex} geometry not deterministic");
                Assert.AreEqual(first.PrimaryGeneratorFamily, second.PrimaryGeneratorFamily, $"Zone {zoneIndex} generator not deterministic");
                Assert.AreEqual(first.MacroCount, second.MacroCount, $"Zone {zoneIndex} macro count not deterministic");
                Assert.AreEqual(first.MicroCount, second.MicroCount, $"Zone {zoneIndex} micro count not deterministic");
            }
        }

        [Test]
        public void ZoneTheme_AdjacentZonesAreStructurallyDistinct()
        {
            int seed = 9876;
            for (int zoneIndex = 1; zoneIndex < 25; zoneIndex++)
            {
                var current = BackgroundRules.GetZoneTheme(zoneIndex, seed);
                var previous = BackgroundRules.GetZoneTheme(zoneIndex - 1, seed);

                Assert.AreNotEqual(previous.GeometryVocabulary, current.GeometryVocabulary,
                    $"Zone {zoneIndex} geometry vocabulary repeats adjacent zone");
                Assert.AreNotEqual(previous.PrimaryGeneratorFamily, current.PrimaryGeneratorFamily,
                    $"Zone {zoneIndex} primary generator repeats adjacent zone");
            }
        }

        [Test]
        public void ZoneTheme_LayerCountsAndScaleBandsMeetConstraints()
        {
            int seed = 1357;
            for (int zoneIndex = 0; zoneIndex < 30; zoneIndex++)
            {
                var theme = BackgroundRules.GetZoneTheme(zoneIndex, seed);
                Assert.GreaterOrEqual(theme.LayerCount, 4);
                Assert.LessOrEqual(theme.LayerCount, 20);
                Assert.AreEqual(theme.LayerCount, theme.Layers.Length);

                if (theme.LayerCount >= 8)
                {
                    Assert.GreaterOrEqual(theme.MacroCount, 2);
                    Assert.GreaterOrEqual(theme.MesoCount, 2);
                    Assert.GreaterOrEqual(theme.MicroCount, 1);
                    Assert.LessOrEqual(theme.MacroCount, 6);
                    Assert.LessOrEqual(theme.MesoCount, 10);
                    Assert.LessOrEqual(theme.MicroCount, 8);
                }
                else
                {
                    Assert.GreaterOrEqual(theme.MacroCount, 1);
                    Assert.GreaterOrEqual(theme.MesoCount, 1);
                    Assert.GreaterOrEqual(theme.MicroCount, 1);
                }

                int softLayers = 0;
                int crispLayers = 0;
                bool hasGradient = false;
                foreach (var layer in theme.Layers)
                {
                    if (layer.EdgeSoftness == Crispness.Soft) softLayers++;
                    if (layer.EdgeSoftness == Crispness.Crisp) crispLayers++;
                    if (layer.IsGradientOnly) hasGradient = true;
                }

                Assert.IsTrue(hasGradient, $"Zone {zoneIndex} missing gradient-only layer");
                if (theme.LayerCount >= 8)
                {
                    Assert.GreaterOrEqual(softLayers, 2, $"Zone {zoneIndex} missing soft layers");
                    Assert.GreaterOrEqual(crispLayers, 2, $"Zone {zoneIndex} missing crisp layers");
                }
            }
        }

        [Test]
        public void LevelVariant_IsDeterministicForSameLevel()
        {
            int seed = 2222;
            for (int level = 1; level <= 100; level++)
            {
                var first = BackgroundRules.GetLevelVariant(level, seed, 6);
                var second = BackgroundRules.GetLevelVariant(level, seed, 6);
                Assert.AreEqual(first.PaletteIndex, second.PaletteIndex, $"Level {level} palette not deterministic");
                Assert.AreEqual(first.HueShift, second.HueShift, $"Level {level} hueShift not deterministic");
                Assert.AreEqual(first.PhaseOffset, second.PhaseOffset, $"Level {level} phase not deterministic");
            }
        }

        [Test]
        public void LevelVariant_VariesWithinZone()
        {
            int seed = 3333;
            var paletteIndices = new HashSet<int>();
            for (int level = 10; level <= 19; level++)
            {
                var variant = BackgroundRules.GetLevelVariant(level, seed, 6);
                paletteIndices.Add(variant.PaletteIndex);
            }
            Assert.GreaterOrEqual(paletteIndices.Count, 2, "Level variants should vary within a Zone");
        }

        [Test]
        public void BackgroundSignature_NoDuplicatesForFirst2000Levels()
        {
            int seed = 4444;
            var signatures = new HashSet<string>();
            for (int level = 1; level <= 2000; level++)
            {
                string signature = BackgroundRules.GetBackgroundSignature(level, seed, 6);
                Assert.IsTrue(signatures.Add(signature), $"Level {level} duplicate signature {signature}");
            }
            Assert.AreEqual(2000, signatures.Count);
        }

        [Test]
        public void ZoneTheme_GrayscaleRecognisable()
        {
            int seed = 7777;
            for (int zoneIndex = 1; zoneIndex < 25; zoneIndex++)
            {
                var theme = BackgroundRules.GetZoneTheme(zoneIndex, seed);
                Assert.IsTrue(BackgroundRules.IsGrayscaleRecognisable(theme),
                    $"Zone {zoneIndex} should be recognisable in grayscale");
            }
        }

        [Test]
        public void ZoneTheme_AntiRepetitionAgainstRecentZones()
        {
            int seed = 8888;
            var recent = new Queue<ZoneThemeFingerprint>();
            for (int zoneIndex = 0; zoneIndex < 30; zoneIndex++)
            {
                var theme = BackgroundRules.GetZoneTheme(zoneIndex, seed);
                if (recent.Count >= 3)
                {
                    recent.Dequeue();
                }

                foreach (var prev in recent)
                {
                    int matches = 0;
                    if (theme.Fingerprint.GeometryVocabulary == prev.GeometryVocabulary) matches++;
                    if (theme.Fingerprint.PrimaryGeneratorFamily == prev.PrimaryGeneratorFamily) matches++;
                    if (theme.Fingerprint.SymmetryClass == prev.SymmetryClass) matches++;
                    if (theme.Fingerprint.LayerCount == prev.LayerCount) matches++;
                    if (theme.Fingerprint.MotionPresence == prev.MotionPresence) matches++;
                    if (theme.Fingerprint.MacroCount == prev.MacroCount && theme.Fingerprint.MesoCount == prev.MesoCount && theme.Fingerprint.MicroCount == prev.MicroCount) matches++;
                    if (theme.Fingerprint.CompositingSignature == prev.CompositingSignature) matches++;

                    Assert.Less(matches, 4, $"Zone {zoneIndex} too similar to recent zone");
                }

                recent.Enqueue(theme.Fingerprint);
            }
        }

        [Test]
        public void PerformanceEstimates_AreWithinBudget()
        {
            int estimatedMax = BackgroundRules.EstimateZoneThemeWorkUnits(20);
            Assert.LessOrEqual(estimatedMax, 24000, "Zone Theme generation estimate too high");
            Assert.LessOrEqual(BackgroundRules.EstimateLevelVariantWorkUnits(), 5000, "Level Variant generation estimate too high");
        }
    }
}
