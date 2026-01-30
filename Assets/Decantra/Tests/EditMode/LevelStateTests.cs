using Decantra.Domain.Model;
using Decantra.Domain.Rules;
using NUnit.Framework;

namespace Decantra.Tests.EditMode
{
    public class LevelStateTests
    {
        [Test]
        public void TryApplyMove_ValidMoveIncrementsMoves()
        {
            var bottles = new[]
            {
                new Bottle(new ColorId?[] { ColorId.Red, ColorId.Red, null, null }),
                new Bottle(new ColorId?[] { null, null, null, null })
            };
            var state = new LevelState(bottles, 0, 10, 3, 1, 123);

            bool valid = MoveRules.IsValidMove(state, 0, 1);
            Assert.IsTrue(valid);

            int poured;
            bool applied = state.TryApplyMove(0, 1, out poured);
            Assert.IsTrue(applied);
            Assert.AreEqual(2, poured);
            Assert.AreEqual(1, state.MovesUsed);
        }

        [Test]
        public void TryApplyMove_InvalidMoveRejected()
        {
            var bottles = new[]
            {
                new Bottle(new ColorId?[] { null, null, null, null }),
                new Bottle(new ColorId?[] { null, null, null, null })
            };
            var state = new LevelState(bottles, 0, 10, 3, 1, 123);

            int poured;
            bool applied = state.TryApplyMove(0, 1, out poured);
            Assert.IsFalse(applied);
            Assert.AreEqual(0, poured);
            Assert.AreEqual(0, state.MovesUsed);
        }

        [Test]
        public void IsWin_RequiresSixSolvedAndThreeEmpty()
        {
            var bottles = new Bottle[9];
            for (int i = 0; i < 6; i++)
            {
                bottles[i] = new Bottle(new ColorId?[] { ColorId.Red, ColorId.Red, ColorId.Red, ColorId.Red });
            }
            for (int i = 6; i < 9; i++)
            {
                bottles[i] = new Bottle(new ColorId?[] { null, null, null, null });
            }
            var state = new LevelState(bottles, 0, 10, 3, 1, 123);
            Assert.IsTrue(state.IsWin());
        }

        [Test]
        public void IsWin_FailsWithPartiallyFilledUniformBottle()
        {
            var bottles = new Bottle[3];
            bottles[0] = new Bottle(new ColorId?[] { ColorId.Blue, ColorId.Blue, null, null });
            bottles[1] = new Bottle(new ColorId?[] { ColorId.Red, ColorId.Red, ColorId.Red, ColorId.Red });
            bottles[2] = new Bottle(new ColorId?[] { null, null, null, null });

            var state = new LevelState(bottles, 0, 10, 3, 1, 123);
            Assert.IsFalse(state.IsWin());
        }
    }
}
