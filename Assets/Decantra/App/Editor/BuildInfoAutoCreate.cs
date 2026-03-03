/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using UnityEditor;

namespace Decantra.App.Editor
{
    /// <summary>
    /// Ensures <c>Assets/Decantra/App/Runtime/BuildInfo.cs</c> exists with real build
    /// metadata whenever the Unity Editor starts.
    ///
    /// <para>
    /// <c>BuildInfo.cs</c> is generated at build time by <see cref="BuildInfoGenerator"/>
    /// and is therefore listed in <c>.gitignore</c>. On a fresh clone the file is absent,
    /// which would prevent Unity from compiling. This <c>[InitializeOnLoad]</c> script runs
    /// before any domain reload completes and generates the file if needed so that the About
    /// section shows a real timestamp even in editor Play-mode.
    /// </para>
    /// </summary>
    [InitializeOnLoad]
    internal static class BuildInfoAutoCreate
    {
        static BuildInfoAutoCreate()
        {
            BuildInfoGenerator.EnsureExists();
        }
    }
}
