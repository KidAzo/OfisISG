using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace NGS.SLO.Shared
{
    public static class PathUtil
    {
        public static bool TryGetProjectRelative(string input, out string projectRelative)
        {
            projectRelative = null;

            if (string.IsNullOrWhiteSpace(input))
                return false;

            if (input.StartsWith("/"))
                input = input.Remove(0, 1);

            if (!input.EndsWith("/"))
                input = input + "/";

            string dataPathFull = Application.dataPath.Replace('\\', '/');

            if (!Path.IsPathRooted(input) &&
                input.Replace('\\', '/').StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                projectRelative = input.Replace('\\', '/');
                return true;
            }

            if (Path.IsPathRooted(input))
            {
                string full = Path.GetFullPath(input).Replace('\\', '/');

                if (full.StartsWith(dataPathFull, StringComparison.OrdinalIgnoreCase))
                {
                    projectRelative = "Assets" + full.Substring(dataPathFull.Length);
                    return true;
                }

                return false;        
            }

            string combined = Path.GetFullPath(Path.Combine(dataPathFull, input)).Replace('\\', '/');

            if (combined.StartsWith(dataPathFull, StringComparison.OrdinalIgnoreCase))
            {
                projectRelative = "Assets" + combined.Substring(dataPathFull.Length);
                return true;
            }

            return false;            
        }
    }
}
