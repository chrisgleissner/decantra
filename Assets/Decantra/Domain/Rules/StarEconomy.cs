/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System;

namespace Decantra.Domain.Rules
{
    /// <summary>
    /// Pure-function star economy rules: reset multiplier, auto-solve pricing,
    /// star spending and refunding, and assisted-level suppression.
    /// All methods are side-effect-free and deterministic.
    /// </summary>
    public static class StarEconomy
    {
        public const int ConvertSinksCost = 10;

        /// <summary>
        /// Returns the star multiplier for the given number of level resets.
        /// 0 resets → 1.0, 1 → 0.75, 2 → 0.5, 3+ → 0.25.
        /// </summary>
        public static float ResolveResetMultiplier(int resetCount)
        {
            if (resetCount <= 0) return 1f;
            if (resetCount == 1) return 0.75f;
            if (resetCount == 2) return 0.5f;
            return 0.25f;
        }

        /// <summary>
        /// Returns the difficulty tier (1, 2, or 3) for a given difficulty100 score.
        /// </summary>
        public static int ResolveDifficultyTier(int difficulty100)
        {
            if (difficulty100 <= 65) return 1;
            if (difficulty100 <= 85) return 2;
            return 3;
        }

        /// <summary>
        /// Returns the auto-solve cost in stars for a given difficulty100 score.
        /// Tier 1 → 15, Tier 2 → 25, Tier 3 → 35.
        /// </summary>
        public static int ResolveAutoSolveCost(int difficulty100)
        {
            int tier = ResolveDifficultyTier(difficulty100);
            if (tier <= 1) return 15;
            if (tier == 2) return 25;
            return 35;
        }

        /// <summary>
        /// Returns the number of stars awarded after applying the reset
        /// multiplier and assisted-level suppression.
        /// Assisted levels always return 0.
        /// </summary>
        public static int ResolveAwardedStars(int baseStars, int resetCount, bool isAssisted)
        {
            if (isAssisted) return 0;
            float multiplier = ResolveResetMultiplier(resetCount);
            return (int)Math.Floor(baseStars * multiplier);
        }

        /// <summary>
        /// Attempts to spend the given cost from the current balance.
        /// Returns true with newBalance set if successful, false otherwise.
        /// Never produces a negative balance.
        /// </summary>
        public static bool TrySpend(int currentBalance, int cost, out int newBalance)
        {
            newBalance = currentBalance;
            if (cost <= 0) return false;
            int safe = Math.Max(0, currentBalance);
            if (safe < cost) return false;
            newBalance = safe - cost;
            return true;
        }

        /// <summary>
        /// Refunds stars to the balance. Never produces a negative balance.
        /// </summary>
        public static int Refund(int currentBalance, int amount)
        {
            if (amount <= 0) return Math.Max(0, currentBalance);
            return Math.Max(0, currentBalance + amount);
        }
    }
}
