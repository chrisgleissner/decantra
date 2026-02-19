/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using NUnit.Framework;
using Decantra.Domain.Rules;

namespace Decantra.Tests.EditMode
{
    [TestFixture]
    public sealed class StarEconomyTests
    {
        // ── Reset Multiplier ──────────────────────────────────────────

        [TestCase(0, 1.00f)]
        [TestCase(1, 0.80f)]
        [TestCase(2, 0.60f)]
        [TestCase(3, 0.40f)]
        [TestCase(4, 0.20f)]
        [TestCase(5, 0.10f)]
        [TestCase(10, 0.10f)]
        [TestCase(100, 0.10f)]
        public void ResetMultiplier_MatchesSpec(int resets, float expected)
        {
            Assert.AreEqual(expected, StarEconomy.ResolveResetMultiplier(resets), 0.001f);
        }

        [Test]
        public void ResetMultiplier_NegativeResets_TreatsAsZero()
        {
            Assert.AreEqual(1f, StarEconomy.ResolveResetMultiplier(-1), 0.001f);
        }

        // ── Auto-Solve Cost ───────────────────────────────────────────

        [TestCase(1, 15)]
        [TestCase(30, 15)]
        [TestCase(65, 15)]
        [TestCase(66, 25)]
        [TestCase(85, 25)]
        [TestCase(86, 35)]
        [TestCase(100, 35)]
        public void AutoSolveCost_MatchesDifficultyTier(int difficulty100, int expectedCost)
        {
            Assert.AreEqual(expectedCost, StarEconomy.ResolveAutoSolveCost(difficulty100));
        }

        [TestCase(1, 1)]
        [TestCase(65, 1)]
        [TestCase(66, 2)]
        [TestCase(85, 2)]
        [TestCase(86, 3)]
        [TestCase(100, 3)]
        public void DifficultyTier_MatchesBoundaries(int difficulty100, int expectedTier)
        {
            Assert.AreEqual(expectedTier, StarEconomy.ResolveDifficultyTier(difficulty100));
        }

        // ── Awarded Stars ─────────────────────────────────────────────

        [Test]
        public void AwardedStars_AssistedLevel_AlwaysZero()
        {
            Assert.AreEqual(0, StarEconomy.ResolveAwardedStars(5, 0, isAssisted: true));
            Assert.AreEqual(0, StarEconomy.ResolveAwardedStars(100, 0, isAssisted: true));
            Assert.AreEqual(0, StarEconomy.ResolveAwardedStars(5, 3, isAssisted: true));
        }

        [Test]
        public void AwardedStars_NoResets_FullValue()
        {
            Assert.AreEqual(5, StarEconomy.ResolveAwardedStars(5, 0, false));
        }

        [Test]
        public void AwardedStars_OneReset_LinearCapReduction()
        {
            Assert.AreEqual(4, StarEconomy.ResolveAwardedStars(5, 1, false));
        }

        [Test]
        public void AwardedStars_TwoResets_LinearCapReduction()
        {
            Assert.AreEqual(3, StarEconomy.ResolveAwardedStars(5, 2, false));
        }

        [Test]
        public void AwardedStars_ThreeResets_LinearCapReduction()
        {
            Assert.AreEqual(2, StarEconomy.ResolveAwardedStars(5, 3, false));
        }

        [Test]
        public void AwardedStars_ManyResets_FloorsAtOne()
        {
            Assert.AreEqual(1, StarEconomy.ResolveAwardedStars(5, 4, false));
            Assert.AreEqual(1, StarEconomy.ResolveAwardedStars(5, 20, false));
        }

        [Test]
        public void AwardedStars_SolvedFloor_NeverZeroForNonAssistedSolve()
        {
            Assert.AreEqual(1, StarEconomy.ResolveAwardedStars(0, 0, false));
            Assert.AreEqual(1, StarEconomy.ResolveAwardedStars(0, 50, false));
        }

        [Test]
        public void AwardedStars_AlwaysClampedToOneToFive_WhenNotAssisted()
        {
            Assert.AreEqual(5, StarEconomy.ResolveAwardedStars(99, 0, false));
            Assert.AreEqual(4, StarEconomy.ResolveAwardedStars(99, 1, false));
            Assert.AreEqual(1, StarEconomy.ResolveAwardedStars(-3, 0, false));
        }

        [Test]
        public void AwardedStars_MonotonicNonIncreasing_WithIncreasingResetCount()
        {
            int previous = int.MaxValue;
            for (int resets = 0; resets <= 20; resets++)
            {
                int stars = StarEconomy.ResolveAwardedStars(5, resets, false);
                TestContext.WriteLine($"resetCount={resets} computedStars={stars}");
                Assert.LessOrEqual(stars, previous);
                previous = stars;
            }
        }

        [Test]
        public void AwardedScore_NoReset_IsFull()
        {
            Assert.AreEqual(200, StarEconomy.ResolveAwardedScore(200, 0, false));
        }

        [Test]
        public void AwardedScore_OneReset_IsReduced()
        {
            int full = StarEconomy.ResolveAwardedScore(200, 0, false);
            int degraded = StarEconomy.ResolveAwardedScore(200, 1, false);
            Assert.Less(degraded, full);
        }

        [Test]
        public void AwardedScore_ManyAndExtremeResets_RemainsPositive()
        {
            int many = StarEconomy.ResolveAwardedScore(200, 20, false);
            int extreme = StarEconomy.ResolveAwardedScore(200, 1_000_000, false);
            TestContext.WriteLine($"resetCount=20 computedScore={many}");
            TestContext.WriteLine($"resetCount=1000000 computedScore={extreme}");
            Assert.GreaterOrEqual(many, 1);
            Assert.GreaterOrEqual(extreme, 1);
        }

        [Test]
        public void AwardedScore_MonotonicNonIncreasing_WithIncreasingResetCount()
        {
            int previous = int.MaxValue;
            for (int resets = 0; resets <= 20; resets++)
            {
                int score = StarEconomy.ResolveAwardedScore(200, resets, false);
                TestContext.WriteLine($"resetCount={resets} computedScore={score}");
                Assert.LessOrEqual(score, previous);
                previous = score;
            }
        }

        [Test]
        public void AwardedScore_AssistedLevel_AlwaysZero()
        {
            Assert.AreEqual(0, StarEconomy.ResolveAwardedScore(200, 0, true));
            Assert.AreEqual(0, StarEconomy.ResolveAwardedScore(200, 10, true));
        }

        // ── TrySpend ──────────────────────────────────────────────────

        [Test]
        public void TrySpend_SufficientBalance_Succeeds()
        {
            bool ok = StarEconomy.TrySpend(20, 10, out int balance);
            Assert.IsTrue(ok);
            Assert.AreEqual(10, balance);
        }

        [Test]
        public void TrySpend_ExactBalance_Succeeds()
        {
            bool ok = StarEconomy.TrySpend(10, 10, out int balance);
            Assert.IsTrue(ok);
            Assert.AreEqual(0, balance);
        }

        [Test]
        public void TrySpend_InsufficientBalance_Fails()
        {
            bool ok = StarEconomy.TrySpend(5, 10, out int balance);
            Assert.IsFalse(ok);
            Assert.AreEqual(5, balance);
        }

        [Test]
        public void TrySpend_ZeroCost_Fails()
        {
            bool ok = StarEconomy.TrySpend(100, 0, out int balance);
            Assert.IsFalse(ok);
            Assert.AreEqual(100, balance);
        }

        [Test]
        public void TrySpend_NegativeCost_Fails()
        {
            bool ok = StarEconomy.TrySpend(100, -5, out int balance);
            Assert.IsFalse(ok);
            Assert.AreEqual(100, balance);
        }

        [Test]
        public void TrySpend_NegativeBalance_ClampsToZero()
        {
            bool ok = StarEconomy.TrySpend(-5, 1, out int balance);
            Assert.IsFalse(ok);
            // Balance should be clamped to the original (no change)
            Assert.AreEqual(-5, balance);
        }

        [Test]
        public void TrySpend_NeverProducesNegativeBalance()
        {
            for (int balance = 0; balance <= 50; balance++)
            {
                for (int cost = 1; cost <= 50; cost++)
                {
                    StarEconomy.TrySpend(balance, cost, out int result);
                    Assert.GreaterOrEqual(result, 0,
                        $"Negative balance produced: balance={balance}, cost={cost}, result={result}");
                }
            }
        }

        // ── Refund ────────────────────────────────────────────────────

        [Test]
        public void Refund_AddsToBalance()
        {
            Assert.AreEqual(25, StarEconomy.Refund(15, 10));
        }

        [Test]
        public void Refund_ZeroAmount_NoChange()
        {
            Assert.AreEqual(15, StarEconomy.Refund(15, 0));
        }

        [Test]
        public void Refund_NegativeAmount_NoChange()
        {
            Assert.AreEqual(15, StarEconomy.Refund(15, -5));
        }

        [Test]
        public void Refund_NegativeBalance_ClampsToZero()
        {
            Assert.AreEqual(0, StarEconomy.Refund(-10, 0));
            Assert.AreEqual(5, StarEconomy.Refund(-10, 15));
        }

        [Test]
        public void Refund_NeverProducesNegativeBalance()
        {
            for (int balance = -10; balance <= 10; balance++)
            {
                for (int amount = -5; amount <= 20; amount++)
                {
                    int result = StarEconomy.Refund(balance, amount);
                    Assert.GreaterOrEqual(result, 0,
                        $"Negative balance produced: balance={balance}, amount={amount}, result={result}");
                }
            }
        }

        // ── Constants ─────────────────────────────────────────────────

        [Test]
        public void ConvertSinksCost_IsTen()
        {
            Assert.AreEqual(10, StarEconomy.ConvertSinksCost);
        }
    }
}
