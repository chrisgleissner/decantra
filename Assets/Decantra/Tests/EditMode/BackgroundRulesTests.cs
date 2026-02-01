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
        public void BackgroundPalette_IsDeterministicForSeedAndLevel()
        {
            var theme = BackgroundThemeId.PastelRainbow;
            int first = BackgroundRules.ComputePaletteIndex(3, 12345, theme, 6);
            int second = BackgroundRules.ComputePaletteIndex(3, 12345, theme, 6);
            Assert.AreEqual(first, second);
        }

        [Test]
        public void BackgroundPalette_ChangesWithSeedOrLevel()
        {
            var theme = BackgroundThemeId.Balloons;
            int a = BackgroundRules.ComputePaletteIndex(5, 222, theme, 6);
            int b = BackgroundRules.ComputePaletteIndex(11, 222, theme, 6);
            int c = BackgroundRules.ComputePaletteIndex(5, 333, theme, 6);
            Assert.AreNotEqual(a, b);
            Assert.AreEqual(a, c);
        }

        [Test]
        public void GetLanguageId_ReturnsCorrectGroupingForLevels()
        {
            // Levels 1-10 => languageId 0
            for (int level = 1; level <= 10; level++)
            {
                Assert.AreEqual(0, BackgroundRules.GetLanguageId(level), $"Level {level} should be languageId 0");
            }

            // Levels 11-20 => languageId 1
            for (int level = 11; level <= 20; level++)
            {
                Assert.AreEqual(1, BackgroundRules.GetLanguageId(level), $"Level {level} should be languageId 1");
            }

            // Levels 21-30 => languageId 2
            for (int level = 21; level <= 30; level++)
            {
                Assert.AreEqual(2, BackgroundRules.GetLanguageId(level), $"Level {level} should be languageId 2");
            }

            // Edge case: Level 100 => languageId 9
            Assert.AreEqual(9, BackgroundRules.GetLanguageId(100));

            // Level 1000 => languageId 99
            Assert.AreEqual(99, BackgroundRules.GetLanguageId(1000));

            // Level 1001 => languageId 100
            Assert.AreEqual(100, BackgroundRules.GetLanguageId(1001));
        }

        [Test]
        public void GetLanguageId_ConsecutiveLevelsGroupCorrectly()
        {
            // Verify that exactly 10 consecutive levels share the same languageId
            for (int group = 0; group < 100; group++)
            {
                int expectedLanguageId = group;
                int firstLevelInGroup = group * 10 + 1;
                int lastLevelInGroup = firstLevelInGroup + 9;

                for (int level = firstLevelInGroup; level <= lastLevelInGroup; level++)
                {
                    Assert.AreEqual(expectedLanguageId, BackgroundRules.GetLanguageId(level),
                        $"Level {level} should be in group {expectedLanguageId}");
                }
            }
        }

        [Test]
        public void GetDesignLanguage_IsDeterministicForSameLanguageId()
        {
            for (int languageId = 0; languageId < 100; languageId++)
            {
                var first = BackgroundRules.GetDesignLanguage(languageId);
                var second = BackgroundRules.GetDesignLanguage(languageId);

                Assert.AreEqual(first.BaseHue, second.BaseHue, $"LanguageId {languageId} BaseHue not deterministic");
                Assert.AreEqual(first.BaseSaturation, second.BaseSaturation, $"LanguageId {languageId} BaseSaturation not deterministic");
                Assert.AreEqual(first.BaseValue, second.BaseValue, $"LanguageId {languageId} BaseValue not deterministic");
                Assert.AreEqual(first.MotifFamily, second.MotifFamily, $"LanguageId {languageId} MotifFamily not deterministic");
                Assert.AreEqual(first.MotifDensity, second.MotifDensity, $"LanguageId {languageId} MotifDensity not deterministic");
                Assert.AreEqual(first.LayerCount, second.LayerCount, $"LanguageId {languageId} LayerCount not deterministic");
            }
        }

        [Test]
        public void GetDesignLanguage_AtLeast100DistinctLanguages()
        {
            var signatures = new HashSet<string>();

            for (int languageId = 0; languageId < 100; languageId++)
            {
                var language = BackgroundRules.GetDesignLanguage(languageId);
                string signature = $"{language.BaseHue:F4}|{language.BaseSaturation:F4}|{language.BaseValue:F4}|{language.MotifFamily}|{language.LayerCount}";
                signatures.Add(signature);
            }

            // All 100 languages must be distinct
            Assert.AreEqual(100, signatures.Count, "First 100 design languages must all be distinct");
        }

        [Test]
        public void GetLevelVariation_IsDeterministicForSameLevel()
        {
            for (int level = 1; level <= 100; level++)
            {
                var first = BackgroundRules.GetLevelVariation(level);
                var second = BackgroundRules.GetLevelVariation(level);

                Assert.AreEqual(first.HueJitter, second.HueJitter, $"Level {level} HueJitter not deterministic");
                Assert.AreEqual(first.SaturationJitter, second.SaturationJitter, $"Level {level} SaturationJitter not deterministic");
                Assert.AreEqual(first.DetailOffset.x, second.DetailOffset.x, $"Level {level} DetailOffset.x not deterministic");
                Assert.AreEqual(first.DetailOffset.y, second.DetailOffset.y, $"Level {level} DetailOffset.y not deterministic");
            }
        }

        [Test]
        public void GetBackgroundSignature_NoDuplicatesForFirst2000Levels()
        {
            var signatures = new HashSet<string>();

            for (int level = 1; level <= 2000; level++)
            {
                string signature = BackgroundRules.GetBackgroundSignature(level);

                Assert.IsTrue(signatures.Add(signature),
                    $"Level {level} has duplicate background signature: {signature}");
            }

            Assert.AreEqual(2000, signatures.Count, "All 2000 levels must have unique background signatures");
        }

        [Test]
        public void GetBackgroundSignature_IsDeterministicForSameLevel()
        {
            for (int level = 1; level <= 100; level++)
            {
                string first = BackgroundRules.GetBackgroundSignature(level);
                string second = BackgroundRules.GetBackgroundSignature(level);

                Assert.AreEqual(first, second, $"Level {level} signature not deterministic");
            }
        }

        [Test]
        public void NoLanguageRepetitionForFirst1000Levels()
        {
            var usedLanguageIds = new HashSet<int>();

            // For first 1000 levels, no languageId should repeat
            // Since there are exactly 100 groups of 10 levels each, and 100 unique languageIds
            for (int group = 0; group < 100; group++)
            {
                int levelInGroup = group * 10 + 1;
                int languageId = BackgroundRules.GetLanguageId(levelInGroup);

                Assert.IsTrue(usedLanguageIds.Add(languageId),
                    $"LanguageId {languageId} repeated at group {group} (level {levelInGroup})");
            }

            Assert.AreEqual(100, usedLanguageIds.Count, "First 1000 levels must use exactly 100 unique language IDs");
        }

        [Test]
        public void DesignLanguage_LayerCountIsAtLeast3()
        {
            for (int languageId = 0; languageId < 100; languageId++)
            {
                var language = BackgroundRules.GetDesignLanguage(languageId);
                Assert.GreaterOrEqual(language.LayerCount, 3,
                    $"LanguageId {languageId} must have at least 3 layers for multi-layer parallax");
            }
        }

        [Test]
        public void DesignLanguage_HasValidMotifFamilies()
        {
            var validMotifs = new HashSet<string>
            {
                "Bubbles", "Crystalline", "Leaves", "Mist", "Waves",
                "Particles", "Geometric", "Organic", "Celestial", "Abstract"
            };

            for (int languageId = 0; languageId < 100; languageId++)
            {
                var language = BackgroundRules.GetDesignLanguage(languageId);
                Assert.IsTrue(validMotifs.Contains(language.MotifFamily),
                    $"LanguageId {languageId} has invalid motif family: {language.MotifFamily}");
            }
        }

        [Test]
        public void DesignLanguage_HueCoversFullSpectrum()
        {
            var hueRanges = new bool[10]; // 10 buckets for 0.0-1.0 hue range

            for (int languageId = 0; languageId < 100; languageId++)
            {
                var language = BackgroundRules.GetDesignLanguage(languageId);
                int bucket = (int)(language.BaseHue * 10);
                if (bucket >= 10) bucket = 9;
                hueRanges[bucket] = true;
            }

            int coveredBuckets = 0;
            for (int i = 0; i < 10; i++)
            {
                if (hueRanges[i]) coveredBuckets++;
            }

            Assert.GreaterOrEqual(coveredBuckets, 8,
                "Design languages should cover at least 80% of the hue spectrum");
        }

        [Test]
        public void DesignLanguage_ClearlySeparatedEvery10Levels()
        {
            // Adjacent language groups (every 10 levels) should have perceptibly different hues
            for (int languageId = 0; languageId < 99; languageId++)
            {
                var current = BackgroundRules.GetDesignLanguage(languageId);
                var next = BackgroundRules.GetDesignLanguage(languageId + 1);

                // Either hue differs, OR motif family differs, OR saturation/value differs significantly
                float hueDiff = System.Math.Abs(current.BaseHue - next.BaseHue);
                // Handle wrap-around for hue
                hueDiff = System.Math.Min(hueDiff, 1f - hueDiff);

                float satDiff = System.Math.Abs(current.BaseSaturation - next.BaseSaturation);
                float valDiff = System.Math.Abs(current.BaseValue - next.BaseValue);
                bool motifDiff = current.MotifFamily != next.MotifFamily;

                bool perceptiblyDifferent = hueDiff > 0.05f || motifDiff || satDiff > 0.1f || valDiff > 0.1f;

                Assert.IsTrue(perceptiblyDifferent,
                    $"LanguageId {languageId} and {languageId + 1} are too similar");
            }
        }
    }
}
