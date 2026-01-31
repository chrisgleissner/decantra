/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System.Collections.Generic;
using Decantra.Domain.Export;
using Decantra.Domain.Model;
using NUnit.Framework;

namespace Decantra.Tests.EditMode
{
    public sealed class LevelLanguageTests
    {
        [Test]
        public void Serialize_ProducesCanonicalJson()
        {
            var bottles = new[]
            {
                new Bottle(new ColorId?[] { ColorId.Blue, ColorId.Blue, ColorId.Orange, null }),
                new Bottle(new ColorId?[] { null, null, null, null }, true)
            };
            var state = new LevelState(bottles, 0, 10, 3, 10, 999);

            var moves = new List<LevelLanguageMove>
            {
                new LevelLanguageMove(0, 0, 0, 1)
            };

            var document = LevelLanguage.FromLevelState(state, 10, 3, 3, moves, 1);
            string json = LevelLanguage.Serialize(document);

            string expected = "{\"lang\":\"decantra-level\",\"version\":1,\"level\":10,\"grid\":{\"rows\":3,\"cols\":3},\"initial\":{\"cells\":[[{\"capacity\":4,\"layers\":[[\"blue\",2],[\"orange\",1]]},{\"capacity\":4,\"layers\":[],\"flags\":[\"sink\"]},null],[null,null,null],[null,null,null]]},\"moves\":[{\"from\":[0,0],\"to\":[0,1]}]}";
            Assert.AreEqual(expected, json);
        }

        [Test]
        public void Parse_RoundTrips_ValidDocument()
        {
            const string json = "{\"lang\":\"decantra-level\",\"version\":1,\"level\":5,\"grid\":{\"rows\":3,\"cols\":3},\"initial\":{\"cells\":[[{\"capacity\":3,\"layers\":[[\"red\",1]]},null,null],[null,null,null],[null,null,null]]},\"moves\":[]}";

            Assert.IsTrue(LevelLanguage.TryParse(json, out var document, out var error), error);
            Assert.AreEqual(1, document.Version);
            Assert.AreEqual(5, document.Level);
            Assert.AreEqual(3, document.Grid.Rows);
            Assert.AreEqual(3, document.Grid.Cols);
            Assert.AreEqual(0, document.Moves.Count);
        }

        [Test]
        public void Parse_Rejects_InvalidDocuments()
        {
            var invalidDocs = new[]
            {
                "{\"lang\":\"wrong\",\"version\":1,\"level\":1,\"grid\":{\"rows\":1,\"cols\":1},\"initial\":{\"cells\":[[null]]},\"moves\":[]}",
                "{\"lang\":\"decantra-level\",\"version\":2,\"level\":1,\"grid\":{\"rows\":1,\"cols\":1},\"initial\":{\"cells\":[[null]]},\"moves\":[]}",
                "{\"lang\":\"decantra-level\",\"version\":1,\"level\":1,\"grid\":{\"rows\":2,\"cols\":1},\"initial\":{\"cells\":[[null]]},\"moves\":[]}",
                "{\"lang\":\"decantra-level\",\"version\":1,\"level\":1,\"grid\":{\"rows\":1,\"cols\":1},\"initial\":{\"cells\":[[{\"capacity\":4,\"layers\":[[\"red\",-1]]}]]},\"moves\":[]}",
                "{\"lang\":\"decantra-level\",\"version\":1,\"level\":1,\"grid\":{\"rows\":1,\"cols\":1},\"initial\":{\"cells\":[[{\"capacity\":5,\"layers\":[[\"red\",4],[\"blue\",4]]}]]},\"moves\":[]}",
                "{\"lang\":\"decantra-level\",\"version\":1,\"level\":1,\"grid\":{\"rows\":1,\"cols\":1},\"initial\":{\"cells\":[[{\"capacity\":4,\"layers\":[[\"red\",2]],\"flags\":[\"sink\"]}]]},\"moves\":[{\"from\":[0,0],\"to\":[1,0]}]}"
            };

            foreach (var doc in invalidDocs)
            {
                Assert.IsFalse(LevelLanguage.TryParse(doc, out _, out _));
            }
        }
    }
}
