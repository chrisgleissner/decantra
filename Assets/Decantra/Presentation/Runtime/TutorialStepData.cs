/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System;

namespace Decantra.Presentation
{
    [Serializable]
    public sealed class TutorialStepData
    {
        public string Id;
        public string TargetObjectName;
        public string Instruction;
        public bool Optional;

        public TutorialStepData(string id, string targetObjectName, string instruction, bool optional = false)
        {
            Id = id;
            TargetObjectName = targetObjectName;
            Instruction = instruction;
            Optional = optional;
        }
    }
}
