using Decantra.Domain.Rules;
using NUnit.Framework;

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
            int b = BackgroundRules.ComputePaletteIndex(6, 222, theme, 6);
            int c = BackgroundRules.ComputePaletteIndex(5, 333, theme, 6);
            Assert.AreNotEqual(a, b);
            Assert.AreNotEqual(a, c);
        }
    }
}
