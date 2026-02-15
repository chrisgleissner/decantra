/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System;

namespace Decantra.Presentation
{
    public static class BuildProvenance
    {
        public static string CommitSha => BuildProvenanceGenerated.CommitSha;
        public static string BuildTimestampUtc => BuildProvenanceGenerated.BuildTimestampUtc;
        public static string UnityVersion => BuildProvenanceGenerated.UnityVersion;
        public static string PipelineId => BuildProvenanceGenerated.PipelineId;
        public static string RefName => BuildProvenanceGenerated.RefName;
        public static string VersionName => BuildProvenanceGenerated.VersionName;
        public static string VersionCode => BuildProvenanceGenerated.VersionCode;

        public static string ShortCommitSha
        {
            get
            {
                string commit = CommitSha;
                if (string.IsNullOrWhiteSpace(commit))
                {
                    return "unknown";
                }

                return commit.Length <= 8 ? commit : commit.Substring(0, 8);
            }
        }

        public static string FooterLine
        {
            get
            {
                string versionCode = string.IsNullOrWhiteSpace(VersionCode) ? "?" : VersionCode;
                string pipeline = string.IsNullOrWhiteSpace(PipelineId) ? "local" : PipelineId;
                return $"Build {ShortCommitSha} · vc {versionCode} · {pipeline}";
            }
        }

        public static string ToDiagnosticString()
        {
            return
                $"commit={CommitSha}, ts={BuildTimestampUtc}, unity={UnityVersion}, pipeline={PipelineId}, ref={RefName}, version={VersionName} ({VersionCode})";
        }
    }
}
