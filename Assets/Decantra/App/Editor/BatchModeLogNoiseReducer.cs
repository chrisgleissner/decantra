/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System;
using UnityEditor;
using UnityEngine;

namespace Decantra.App.Editor
{
    /// <summary>
    /// Keeps batchmode output readable by removing stack traces from normal log/warning lines.
    /// Errors and exceptions keep their stack traces.
    /// Set DECANTRA_VERBOSE_LOGS=1 to opt out.
    /// </summary>
    [InitializeOnLoad]
    internal static class BatchModeLogNoiseReducer
    {
        private const string VerboseLogsEnvVar = "DECANTRA_VERBOSE_LOGS";

        static BatchModeLogNoiseReducer()
        {
            if (!Application.isBatchMode)
            {
                return;
            }

            if (IsVerboseLogsEnabled())
            {
                return;
            }

            Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
            Application.SetStackTraceLogType(LogType.Warning, StackTraceLogType.None);
        }

        private static bool IsVerboseLogsEnabled()
        {
            var value = Environment.GetEnvironmentVariable(VerboseLogsEnvVar);
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            return value.Equals("1", StringComparison.OrdinalIgnoreCase)
                || value.Equals("true", StringComparison.OrdinalIgnoreCase)
                || value.Equals("yes", StringComparison.OrdinalIgnoreCase);
        }
    }
}
