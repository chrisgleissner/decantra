/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System;
using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace Decantra.App.Editor
{
    public static class CommandLineTests
    {
        private static TestRunnerApi _api;
        private static string _resultsPath;
        private const string ResultsPathKey = "Decantra.CommandLineTests.ResultsPath";

        public static void RunEditMode()
        {
            RunTests(TestMode.EditMode, "Logs/TestResults.xml");
        }

        public static void RunPlayMode()
        {
            RunTests(TestMode.PlayMode, "Logs/PlayModeTestResults.xml");
        }

        private static void RunTests(TestMode mode, string defaultResultsPath)
        {
            _resultsPath = GetArg("-testResults") ?? defaultResultsPath;
            _resultsPath = Path.GetFullPath(_resultsPath);
            SessionState.SetString(ResultsPathKey, _resultsPath);
            EnsureDirectory(_resultsPath);

            _api = ScriptableObject.CreateInstance<TestRunnerApi>();
            _api.RegisterCallbacks(new Callback());

            var filter = new Filter { testMode = mode };
            var settings = new ExecutionSettings(filter);
            _api.Execute(settings);
        }

        private static void EnsureDirectory(string filePath)
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        private static string GetArg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == name)
                {
                    return args[i + 1];
                }
            }
            return null;
        }

        private static bool IsCoverageEnabled()
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-enableCodeCoverage")
                {
                    return true;
                }
            }
            return false;
        }

        private sealed class Callback : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun)
            {
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                if (string.IsNullOrEmpty(_resultsPath))
                {
                    _resultsPath = SessionState.GetString(ResultsPathKey, "Logs/TestResults.xml");
                    if (!string.IsNullOrEmpty(_resultsPath))
                    {
                        _resultsPath = Path.GetFullPath(_resultsPath);
                    }
                }
                TestRunnerApi.SaveResultToFile(result, _resultsPath);
                int exitCode = result.FailCount > 0 ? 1 : 0;
                if (EditorApplication.isPlaying)
                {
                    EditorApplication.isPlaying = false;
                }
                EditorApplication.delayCall += () => EditorApplication.Exit(exitCode);
            }

            public void TestStarted(ITestAdaptor test)
            {
            }

            public void TestFinished(ITestResultAdaptor result)
            {
            }
        }
    }
}
