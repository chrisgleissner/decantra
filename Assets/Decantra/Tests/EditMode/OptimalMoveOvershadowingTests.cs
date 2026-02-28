/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System;
using Decantra.Domain.Generation;
using Decantra.Domain.Rules;
using Decantra.Domain.Scoring;
using NUnit.Framework;

namespace Decantra.Tests.EditMode
{
    public sealed class SlackFloorTests
    {
        [Test]
        public void SlackFactor_Level500_AtLeastMinimum()
        {
            float slack = MoveAllowanceCalculator.ComputeSlackFactor(500);
            Assert.GreaterOrEqual(slack, MoveAllowanceCalculator.MinimumSlackFactor);
        }

        [Test]
        public void SlackFactor_Level1000_AtLeastMinimum()
        {
            float slack = MoveAllowanceCalculator.ComputeSlackFactor(1000);
            Assert.GreaterOrEqual(slack, MoveAllowanceCalculator.MinimumSlackFactor);
        }

        [Test]
        public void MovesAllowed_10Optimal_AtLeastCeil10Times115()
        {
            var profile = LevelDifficultyEngine.GetProfile(500);
            int allowed = MoveAllowanceCalculator.ComputeMovesAllowed(profile, 10);
            int expected = (int)Math.Ceiling(10 * MoveAllowanceCalculator.MinimumSlackFactor);
            Assert.GreaterOrEqual(allowed, expected);
        }

        [Test]
        public void SlackFactor_MonotonicNonIncreasing()
        {
            float previous = float.MaxValue;
            for (int level = 1; level <= 600; level += 10)
            {
                float slack = MoveAllowanceCalculator.ComputeSlackFactor(level);
                Assert.LessOrEqual(slack, previous + 0.001f,
                    $"Slack increased at level {level}: {slack} > {previous}");
                previous = slack;
            }
        }

        [Test]
        public void SlackFactor_Level1_Is2()
        {
            Assert.AreEqual(2.0f, MoveAllowanceCalculator.ComputeSlackFactor(1));
        }

        [Test]
        public void SlackFactor_Level500_IsMinimumSlackFactor()
        {
            Assert.AreEqual(MoveAllowanceCalculator.MinimumSlackFactor, MoveAllowanceCalculator.ComputeSlackFactor(500));
        }
    }

    public sealed class ParCalculatorTests
    {
        [Test]
        public void TightSlack_AllowsBuffer2()
        {
            // optimal=10, allowed=12 → slack=2 → buffer=2 → par=12
            int par = ParCalculator.ComputePar(10, 12);
            Assert.AreEqual(12, par);
        }

        [Test]
        public void MediumSlack_AllowsBuffer1()
        {
            // optimal=10, allowed=14 → slack=4 → buffer=1 → par=11
            int par = ParCalculator.ComputePar(10, 14);
            Assert.AreEqual(11, par);
        }

        [Test]
        public void LargeSlack_NoBuffer()
        {
            // optimal=10, allowed=20 → slack=10 → buffer=0 → par=10
            int par = ParCalculator.ComputePar(10, 20);
            Assert.AreEqual(10, par);
        }

        [Test]
        public void Slack5_Buffer1()
        {
            // optimal=10, allowed=15 → slack=5 → buffer=1 → par=11
            int par = ParCalculator.ComputePar(10, 15);
            Assert.AreEqual(11, par);
        }

        [Test]
        public void Slack6_NoBuffer()
        {
            // optimal=10, allowed=16 → slack=6 → buffer=0 → par=10
            int par = ParCalculator.ComputePar(10, 16);
            Assert.AreEqual(10, par);
        }

        [Test]
        public void TightSlack_ParNeverExceedsMovesAllowed()
        {
            int par = ParCalculator.ComputePar(10, 11);
            Assert.AreEqual(11, par);
        }

        [Test]
        public void ComputePar_ThrowsWhenMovesAllowedBelowOptimal()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ParCalculator.ComputePar(10, 9));
        }
    }

    public sealed class StarDistributionTightSlackTests
    {
        [Test]
        public void TightSlack_OptimalPlus2_Gets5Stars()
        {
            // optimal=10, allowed=12 → par=12 (buffer=2)
            // movesUsed=12 → delta to par=0 → 5 stars
            int stars = ScoreCalculator.CalculateStars(10, 12, 12);
            Assert.AreEqual(5, stars);
        }

        [Test]
        public void TightSlack_OptimalExact_Gets5Stars()
        {
            // optimal=10, allowed=12 → par=12 (buffer=2)
            // movesUsed=10 → delta to par=-2 → 5 stars
            int stars = ScoreCalculator.CalculateStars(10, 10, 12);
            Assert.AreEqual(5, stars);
        }

        [Test]
        public void LargeSlack_Unchanged()
        {
            // optimal=10, allowed=20 → par=10 (buffer=0) → same as before
            Assert.AreEqual(5, ScoreCalculator.CalculateStars(10, 10, 20));
            Assert.AreEqual(4, ScoreCalculator.CalculateStars(10, 12, 20));
            Assert.AreEqual(3, ScoreCalculator.CalculateStars(10, 14, 20));
            Assert.AreEqual(2, ScoreCalculator.CalculateStars(10, 16, 20));
            Assert.AreEqual(1, ScoreCalculator.CalculateStars(10, 18, 20));
            Assert.AreEqual(0, ScoreCalculator.CalculateStars(10, 19, 20));
        }

        [Test]
        public void Optimal_AlwaysYields5Stars()
        {
            // Regardless of slack, optimal or better always gives 5 stars
            Assert.AreEqual(5, ScoreCalculator.CalculateStars(10, 10, 20));
            Assert.AreEqual(5, ScoreCalculator.CalculateStars(10, 10, 12));
            Assert.AreEqual(5, ScoreCalculator.CalculateStars(10, 9, 12));
            Assert.AreEqual(5, ScoreCalculator.CalculateStars(10, 10, 11));
        }

        [Test]
        public void GrosslyInefficient_Stars0()
        {
            // movesUsed=25, allowed=20 → way over budget
            Assert.AreEqual(0, ScoreCalculator.CalculateStars(10, 25, 20));
        }

        [Test]
        public void OverBudget_NeverGets5Stars()
        {
            // Prior to clamping/guarding this could award 5★ in slack=1 scenarios.
            Assert.AreEqual(0, ScoreCalculator.CalculateStars(10, 12, 11));
        }
    }

    public sealed class ScoreMultiplierCurveTests
    {
        [Test]
        public void X1_Yields2()
        {
            // x=1.0 (optimal) → perfMult = 0.20 + 1.80 * 1.0 = 2.00
            // difficulty 100 → d=1.0 → base = 60+60=120 → levelScore = 120*2.0 = 240 + clean 25 = 265
            int score = ScoreCalculator.CalculateLevelScore(10, 10, 20, 100, true);
            // The key assertion: optimal play gives perfMult=2.0
            // base = 60+60*1^0.7=120, perfMult=2.0, 120*2=240+25=265
            Assert.AreEqual(265, score);
        }

        [Test]
        public void X08_HigherThanOldCurve()
        {
            // x=0.8 → new: 0.20 + 1.80*0.64 = 1.352
            //          old: 0.10 + 1.90*0.4096 ≈ 0.878
            // Verify the new value is meaningfully higher at a reference difficulty
            // difficulty=100, base=120
            // new: 120 * 1.352 = 162.24 → round = 162
            // old: 120 * 0.878 = 105.36 → round = 105
            int score = ScoreCalculator.CalculateLevelScore(10, 12, 20, 100, false);
            // x = 1 - 2/10 = 0.8
            Assert.Greater(score, 105, "New curve at x=0.8 should produce higher score than old curve");
        }

        [Test]
        public void Monotonicity_InX()
        {
            int previousScore = 0;
            for (int movesUsed = 20; movesUsed >= 10; movesUsed--)
            {
                int score = ScoreCalculator.CalculateLevelScore(10, movesUsed, 20, 80, false);
                Assert.GreaterOrEqual(score, previousScore,
                    $"Score should increase as moves decrease (movesUsed={movesUsed})");
                previousScore = score;
            }
        }

        [Test]
        public void NoNegativeScores()
        {
            for (int movesUsed = 10; movesUsed <= 30; movesUsed++)
            {
                int score = ScoreCalculator.CalculateLevelScore(10, movesUsed, 20, 80, false);
                Assert.GreaterOrEqual(score, 0, $"Score negative at movesUsed={movesUsed}");
            }
        }

        [Test]
        public void MultiplierCap_NeverExceeds2x()
        {
            // difficulty=100 => base score = 120, max multiplier cap = 2.0 => 240
            const int maxNonCleanScore = 240;
            for (int movesUsed = 0; movesUsed <= 30; movesUsed++)
            {
                int score = ScoreCalculator.CalculateLevelScore(10, movesUsed, 20, 100, false);
                Assert.LessOrEqual(score, maxNonCleanScore, $"Score exceeded 2x cap at movesUsed={movesUsed}");
            }
        }

        [Test]
        public void WorstCaseMultiplier_AtLeast020()
        {
            // x=0 → perfMult = 0.20 + 1.80*0 = 0.20
            // difficulty=100, base=120 → 120*0.20 = 24
            int score = ScoreCalculator.CalculateLevelScore(10, 20, 20, 100, false);
            Assert.AreEqual(24, score);
        }
    }

    public sealed class MultiSolutionPreferenceTests
    {
        [Test]
        public void HighDifficulty_PrefersMultiSolution_WhenEquallyClose()
        {
            var candidateSingle = new MonotonicLevelSelector.CandidateResult
            {
                CandidateIndex = 0,
                Seed = 100,
                IntrinsicDifficulty = 75,
                Metrics = new LevelMetrics(0.3f, 2.5f, 3, 0.2f, 0.5f, 1, 5, 4, 3),
                IsValid = true
            };

            var candidateMulti = new MonotonicLevelSelector.CandidateResult
            {
                CandidateIndex = 1,
                Seed = 200,
                IntrinsicDifficulty = 75,
                Metrics = new LevelMetrics(0.3f, 2.5f, 3, 0.2f, 0.5f, 2, 5, 4, 3),
                IsValid = true
            };

            int targetDiff = 75;
            int scoreSingle = MonotonicLevelSelector.ComputeSelectionScore(targetDiff, candidateSingle);
            int scoreMulti = MonotonicLevelSelector.ComputeSelectionScore(targetDiff, candidateMulti);

            Assert.Less(scoreMulti, scoreSingle,
                "Multi-solution candidate should be preferred at high difficulty");
        }

        [Test]
        public void LowDifficulty_NoPreference()
        {
            int targetDiff = 50;
            var candidateSingle = new MonotonicLevelSelector.CandidateResult
            {
                CandidateIndex = 0,
                Seed = 100,
                IntrinsicDifficulty = 50,
                Metrics = new LevelMetrics(0.3f, 2.5f, 3, 0.2f, 0.5f, 1, 5, 4, 3),
                IsValid = true
            };

            var candidateMulti = new MonotonicLevelSelector.CandidateResult
            {
                CandidateIndex = 1,
                Seed = 200,
                IntrinsicDifficulty = 50,
                Metrics = new LevelMetrics(0.3f, 2.5f, 3, 0.2f, 0.5f, 2, 5, 4, 3),
                IsValid = true
            };
            int scoreSingle = MonotonicLevelSelector.ComputeSelectionScore(targetDiff, candidateSingle);
            int scoreMulti = MonotonicLevelSelector.ComputeSelectionScore(targetDiff, candidateMulti);

            Assert.AreEqual(scoreSingle, scoreMulti,
                "No multi-solution preference at low difficulty");
        }

        [Test]
        public void ComputeSelectionScore_DeterministicForSameInput()
        {
            var candidate = new MonotonicLevelSelector.CandidateResult
            {
                CandidateIndex = 1,
                Seed = 200,
                IntrinsicDifficulty = 75,
                Metrics = new LevelMetrics(0.3f, 2.5f, 3, 0.2f, 0.5f, 2, 5, 4, 3),
                IsValid = true
            };

            int a = MonotonicLevelSelector.ComputeSelectionScore(75, candidate);
            int b = MonotonicLevelSelector.ComputeSelectionScore(75, candidate);
            Assert.AreEqual(a, b);
        }

        [Test]
        public void ComputeCandidateSeed_Deterministic_SameInputsSameResult()
        {
            int seed1A = MonotonicLevelSelector.ComputeCandidateSeed(100, 0);
            int seed1B = MonotonicLevelSelector.ComputeCandidateSeed(100, 0);
            Assert.AreEqual(seed1A, seed1B);

            int seed2A = MonotonicLevelSelector.ComputeCandidateSeed(100, 1);
            int seed2B = MonotonicLevelSelector.ComputeCandidateSeed(100, 1);
            Assert.AreEqual(seed2A, seed2B);
        }
    }
}
