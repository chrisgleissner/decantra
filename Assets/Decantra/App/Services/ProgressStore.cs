/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System.Collections.Generic;
using System.IO;
using Decantra.Domain.Persistence;
using UnityEngine;

namespace Decantra.App.Services
{
    public sealed class ProgressStore
    {
        private const string FileName = "progress.json";
        private const string PublicDirName = "Decantra";
        private readonly string[] _overridePaths;

        public ProgressStore(IEnumerable<string> overridePaths = null)
        {
            _overridePaths = overridePaths == null ? null : new List<string>(overridePaths).ToArray();
        }

        public ProgressData Load()
        {
            foreach (string path in GetPaths())
            {
                if (!File.Exists(path)) continue;
                string json = File.ReadAllText(path);
                var data = JsonUtility.FromJson<ProgressData>(json);
                if (data != null)
                {
                    EnsureDefaults(data);
                    return data;
                }
            }

            return new ProgressData
            {
                HighestUnlockedLevel = 1,
                CurrentLevel = 1,
                CurrentSeed = 0,
                CurrentScore = 0,
                HighScore = 0,
                StarBalance = 0
            };
        }

        public void Save(ProgressData data)
        {
            EnsureDefaults(data);
            string json = JsonUtility.ToJson(data, true);
            foreach (string path in GetPaths())
            {
                try
                {
                    var dir = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }
                    File.WriteAllText(path, json);
                }
                catch
                {
                    // Ignore and keep trying other paths.
                }
            }
        }

        private IEnumerable<string> GetPaths()
        {
            if (_overridePaths != null && _overridePaths.Length > 0)
            {
                for (int i = 0; i < _overridePaths.Length; i++)
                {
                    var path = _overridePaths[i];
                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        yield return path;
                    }
                }
                yield break;
            }

            yield return Path.Combine(Application.persistentDataPath, FileName);
#if UNITY_EDITOR
            var publicRoot = "/storage/emulated/0";
            if (Directory.Exists(publicRoot))
            {
                yield return Path.Combine(publicRoot, PublicDirName, FileName);
            }
#endif
        }

        private static void EnsureDefaults(ProgressData data)
        {
            if (data.HighestUnlockedLevel <= 0) data.HighestUnlockedLevel = 1;
            if (data.CurrentLevel <= 0) data.CurrentLevel = data.HighestUnlockedLevel;
            if (data.CurrentSeed < 0) data.CurrentSeed = 0;
            if (data.CurrentScore < 0) data.CurrentScore = 0;
            if (data.HighScore < 0) data.HighScore = 0;
            if (data.StarBalance < 0) data.StarBalance = 0;
            if (data.CompletedLevels == null) data.CompletedLevels = new List<int>();
            if (data.BestPerformances == null) data.BestPerformances = new List<LevelPerformanceRecord>();
        }
    }
}
