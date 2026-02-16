/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Decantra.Domain.Model;
using Decantra.Presentation.View;
using NUnit.Framework;
using UnityEngine;

namespace Decantra.Tests.EditMode
{
    public class ColorPaletteTests
    {
        [Test]
        public void SetAccessibleColorsEnabled_SwitchesPaletteValues()
        {
            var palette = BuildPalette();

            palette.SetAccessibleColorsEnabled(false);
            Color defaultRed = palette.GetColor(ColorId.Red);
            Assert.AreEqual(ToColor32(ParseHex("#EB4038")), ToColor32(defaultRed));

            palette.SetAccessibleColorsEnabled(true);
            Color accessibleRed = palette.GetColor(ColorId.Red);
            Assert.AreEqual(ToColor32(ParseHex("#0072B2")), ToColor32(accessibleRed));
        }

        [Test]
        public void AccessibleAndDefaultPalettes_HaveDistinctLuminanceOrder()
        {
            var palette = BuildPalette();

            palette.SetAccessibleColorsEnabled(false);
            var defaultOrder = OrderedByLuminance(palette);

            palette.SetAccessibleColorsEnabled(true);
            var accessibleOrder = OrderedByLuminance(palette);

            CollectionAssert.AreNotEqual(defaultOrder, accessibleOrder);
        }

        private static IReadOnlyList<ColorId> OrderedByLuminance(ColorPalette palette)
        {
            var ids = new[]
            {
                ColorId.Red, ColorId.Blue, ColorId.Green, ColorId.Yellow,
                ColorId.Purple, ColorId.Orange, ColorId.Cyan, ColorId.Magenta
            };

            return ids
                .Select(id => new { Id = id, Luminance = Luminance(palette.GetColor(id)) })
                .OrderBy(pair => pair.Luminance)
                .Select(pair => pair.Id)
                .ToArray();
        }

        private static float Luminance(Color color)
        {
            return 0.2126f * color.r + 0.7152f * color.g + 0.0722f * color.b;
        }

        private static ColorPalette BuildPalette()
        {
            var palette = ScriptableObject.CreateInstance<ColorPalette>();
            var defaultEntries = new List<ColorPalette.Entry>
            {
                Entry(ColorId.Red, "#EB4038"),
                Entry(ColorId.Blue, "#4080F2"),
                Entry(ColorId.Green, "#33C763"),
                Entry(ColorId.Yellow, "#FAE033"),
                Entry(ColorId.Purple, "#A661D9"),
                Entry(ColorId.Orange, "#F78C33"),
                Entry(ColorId.Cyan, "#33D9E6"),
                Entry(ColorId.Magenta, "#EB5CC7")
            };
            var accessibleEntries = new List<ColorPalette.Entry>
            {
                Entry(ColorId.Red, "#0072B2"),
                Entry(ColorId.Blue, "#E69F00"),
                Entry(ColorId.Green, "#56B4E9"),
                Entry(ColorId.Yellow, "#009E73"),
                Entry(ColorId.Purple, "#F0E442"),
                Entry(ColorId.Orange, "#D55E00"),
                Entry(ColorId.Cyan, "#CC79A7"),
                Entry(ColorId.Magenta, "#1B2A41")
            };

            SetPrivateField(palette, "entries", defaultEntries);
            SetPrivateField(palette, "colorBlindEntries", accessibleEntries);
            return palette;
        }

        private static ColorPalette.Entry Entry(ColorId colorId, string hex)
        {
            return new ColorPalette.Entry
            {
                ColorId = colorId,
                Color = ParseHex(hex)
            };
        }

        private static Color ParseHex(string hex)
        {
            Assert.IsTrue(ColorUtility.TryParseHtmlString(hex, out Color color), $"Invalid color literal: {hex}");
            return color;
        }

        private static Color32 ToColor32(Color color)
        {
            return (Color32)color;
        }

        private static void SetPrivateField(object instance, string name, object value)
        {
            var field = instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field {name} not found");
            field.SetValue(instance, value);
        }
    }
}
