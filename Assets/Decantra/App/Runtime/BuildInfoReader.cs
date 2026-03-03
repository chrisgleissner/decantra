/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System;
using System.Reflection;

namespace Decantra.App
{
    public static class BuildInfoReader
    {
        private static readonly Type BuildInfoType = ResolveBuildInfoType();

        public static string Version => ReadConstOrProperty("Version");
        public static string BuildUtc => ReadConstOrProperty("BuildUtc");
        public static string Revision => ReadConstOrProperty("Revision");

        private static Type ResolveBuildInfoType()
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType("Decantra.App.BuildInfo", false);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }

        private static string ReadConstOrProperty(string memberName)
        {
            if (BuildInfoType == null || string.IsNullOrWhiteSpace(memberName))
            {
                return string.Empty;
            }

            const BindingFlags Flags = BindingFlags.Public | BindingFlags.Static;

            FieldInfo field = BuildInfoType.GetField(memberName, Flags);
            if (field != null)
            {
                object value = field.GetValue(null);
                return value?.ToString() ?? string.Empty;
            }

            PropertyInfo property = BuildInfoType.GetProperty(memberName, Flags);
            if (property != null)
            {
                object value = property.GetValue(null, null);
                return value?.ToString() ?? string.Empty;
            }

            return string.Empty;
        }
    }
}