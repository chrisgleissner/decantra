/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System;
using Decantra.App;
using NUnit.Framework;

namespace Decantra.Tests.EditModeApp
{
    /// <summary>
    /// Guards the build-info pipeline:
    ///   BuildInfo.cs (generated, gitignored)
    ///   → BuildInfoReader (reflection so no compile-time const-inlining)
    ///   → GetRuntimeBuildUtcTimestamp() in SceneBootstrap
    ///
    /// History: 1.4.4-rc1 showed "Build UTC unknown" because Android High
    /// managed-code stripping removed BuildInfo (only referenced via reflection).
    /// Fix: link.xml preserves Decantra.App.BuildInfo; [Preserve] on the class
    /// and each member is defence-in-depth.
    ///
    /// Note: All assertions go through BuildInfoReader (reflection) rather than
    /// BuildInfo directly, to avoid a compile-time dependency on the gitignored
    /// generated file Assets/Decantra/App/Runtime/BuildInfo.cs.
    /// </summary>
    public class BuildInfoReaderTests
    {
        [Test]
        public void BuildInfoReader_Version_IsNotEmpty()
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(BuildInfoReader.Version),
                "BuildInfoReader.Version is empty — check that EnsureExists/GenerateAndImport ran.");
        }

        [Test]
        public void BuildInfoReader_BuildUtc_IsValidIso8601()
        {
            string raw = BuildInfoReader.BuildUtc;
            bool parsed = DateTime.TryParse(raw, null,
                System.Globalization.DateTimeStyles.AdjustToUniversal |
                System.Globalization.DateTimeStyles.AssumeUniversal,
                out _);
            Assert.IsTrue(parsed,
                $"BuildInfoReader.BuildUtc '{raw}' could not be parsed as a UTC date/time.");
        }

        [Test]
        public void BuildInfoReader_BuildUtc_IsNotEmpty()
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(BuildInfoReader.BuildUtc),
                "BuildInfoReader.BuildUtc is empty — About section will show 'Build UTC unknown'. " +
                "Root cause: IL2CPP stripping removed Decantra.App.BuildInfo (reflection-only access). " +
                "Fix: link.xml + [Preserve] attribute on BuildInfo class.");
        }

        [Test]
        public void BuildInfoReader_BuildUtc_ParsesAsUtcDateTime()
        {
            string raw = BuildInfoReader.BuildUtc;
            Assert.IsFalse(string.IsNullOrWhiteSpace(raw),
                "BuildInfoReader.BuildUtc is empty.");

            bool parsed = DateTime.TryParse(raw, null,
                System.Globalization.DateTimeStyles.AdjustToUniversal |
                System.Globalization.DateTimeStyles.AssumeUniversal,
                out DateTime dt);
            Assert.IsTrue(parsed, $"BuildInfoReader.BuildUtc '{raw}' is not a valid date/time.");
            Assert.Greater(dt.Year, 2024,
                $"BuildInfoReader.BuildUtc '{raw}' looks too old — GenerateAndImport() may not have run.");
        }
    }
}
