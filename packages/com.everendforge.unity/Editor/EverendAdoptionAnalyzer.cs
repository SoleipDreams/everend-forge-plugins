using System;
using System.Collections.Generic;
using UnityEditor;

namespace EverendForge.Unity.Editor
{
    public sealed class EverendMappingSuggestion
    {
        public string EverendId;
        public string Kind;
        public string AssetPath;
        public string Guid;
        public string Reason;
        public int Score;
        public string Mode;
    }

    public static class EverendAdoptionAnalyzer
    {
        public static List<EverendMappingSuggestion> Suggest(EverendNarrativeCatalog catalog, EverendProjectionProfile profile)
        {
            var result = new List<EverendMappingSuggestion>();
            if (catalog == null) return result;
            var manifest = FindManifest(profile);
            foreach (var narrativeEvent in catalog.Package.Story.Events)
            {
                EverendNativeArtifact artifact;
                if (manifest != null && manifest.TryGet(narrativeEvent.Id, out artifact) && artifact.NativeAsset != null)
                {
                    result.Add(new EverendMappingSuggestion { EverendId = narrativeEvent.Id, Kind = "event", AssetPath = artifact.AssetPath, Guid = AssetDatabase.AssetPathToGUID(artifact.AssetPath), Reason = "Existing projection manifest", Score = 100, Mode = "generated" });
                    continue;
                }
                var matches = AssetDatabase.FindAssets("Event_" + EverendRuntimePackageImporter.Sanitize(narrativeEvent.Id));
                if (matches.Length == 1)
                {
                    var path = AssetDatabase.GUIDToAssetPath(matches[0]);
                    result.Add(new EverendMappingSuggestion { EverendId = narrativeEvent.Id, Kind = "event", AssetPath = path, Guid = matches[0], Reason = "Unique GUID/name match", Score = 80, Mode = "linked" });
                }
                else result.Add(new EverendMappingSuggestion { EverendId = narrativeEvent.Id, Kind = "event", Reason = matches.Length == 0 ? "No existing asset found; create generated asset" : "Ambiguous name match requires confirmation", Score = matches.Length == 0 ? 20 : 40, Mode = matches.Length == 0 ? "generated" : "linked" });
            }
            return result;
        }

        private static EverendProjectionManifest FindManifest(EverendProjectionProfile profile)
        {
            if (profile == null) return null;
            foreach (var guid in AssetDatabase.FindAssets("t:EverendProjectionManifest"))
            {
                var manifest = AssetDatabase.LoadAssetAtPath<EverendProjectionManifest>(AssetDatabase.GUIDToAssetPath(guid));
                if (manifest != null && string.Equals(manifest.ProfileId, profile.ProfileId, StringComparison.Ordinal)) return manifest;
            }
            return null;
        }
    }
}
