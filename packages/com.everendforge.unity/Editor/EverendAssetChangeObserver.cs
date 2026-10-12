using System;
using System.Collections.Generic;
using UnityEditor;

namespace EverendForge.Unity.Editor
{
    /// <summary>Classifies Unity asset changes without mutating narrative-owned fields.</summary>
    public sealed class EverendAssetChangeObserver : AssetPostprocessor
    {
        public static event Action<string[]> AssetsChanged;

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            var paths = new List<string>();
            if (imported != null) paths.AddRange(imported);
            if (deleted != null) paths.AddRange(deleted);
            if (moved != null) paths.AddRange(moved);
            if (movedFrom != null) paths.AddRange(movedFrom);
            if (paths.Count > 0) AssetsChanged?.Invoke(paths.ToArray());
        }

        public static EverendChangeSet BuildUnityOwnedProposal(IEnumerable<string> paths)
        {
            var result = new EverendChangeSet { Description = "Unity asset changes proposed for PathBranching review." };
            if (paths == null) return result;
            foreach (var path in paths)
            {
                if (string.IsNullOrWhiteSpace(path) || path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;
                var generated = path.IndexOf("/EverendForge/Generated/", StringComparison.OrdinalIgnoreCase) >= 0;
                result.Operations.Add(new EverendChangeOperation
                {
                    Operation = generated ? "propose" : "set",
                    EntityType = "unityAsset",
                    EntityId = AssetDatabase.AssetPathToGUID(path),
                    PropertyPath = "path",
                    Ownership = generated ? "adapter" : "unity",
                    ValueJson = "\"" + path.Replace("\\", "/").Replace("\"", "\\\"") + "\"",
                    Reason = generated ? "Generated assets are adapter-owned and cannot be applied as narrative edits." : "Unity-owned asset path/reference changed."
                });
            }
            return result;
        }
    }
}
