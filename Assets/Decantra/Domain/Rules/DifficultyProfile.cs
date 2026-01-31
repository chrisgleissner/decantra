/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System;

namespace Decantra.Domain.Rules
{
    public sealed class DifficultyProfile
    {
        public DifficultyProfile(int levelIndex,
            LevelBand band,
            int bottleCount,
            int colorCount,
            int emptyBottleCount,
            int reverseMoves,
            BackgroundThemeId themeId,
            int difficultyRating)
        {
            if (levelIndex <= 0) throw new ArgumentOutOfRangeException(nameof(levelIndex));
            if (bottleCount <= 0) throw new ArgumentOutOfRangeException(nameof(bottleCount));
            if (colorCount <= 0) throw new ArgumentOutOfRangeException(nameof(colorCount));
            if (emptyBottleCount < 0) throw new ArgumentOutOfRangeException(nameof(emptyBottleCount));
            if (reverseMoves <= 0) throw new ArgumentOutOfRangeException(nameof(reverseMoves));
            if (difficultyRating < 0) throw new ArgumentOutOfRangeException(nameof(difficultyRating));

            LevelIndex = levelIndex;
            Band = band;
            BottleCount = bottleCount;
            ColorCount = colorCount;
            EmptyBottleCount = emptyBottleCount;
            ReverseMoves = reverseMoves;
            ThemeId = themeId;
            DifficultyRating = difficultyRating;
        }

        public int LevelIndex { get; }
        public LevelBand Band { get; }
        public int BottleCount { get; }
        public int ColorCount { get; }
        public int EmptyBottleCount { get; }
        public int ReverseMoves { get; }
        public BackgroundThemeId ThemeId { get; }
        public int DifficultyRating { get; }
    }
}
