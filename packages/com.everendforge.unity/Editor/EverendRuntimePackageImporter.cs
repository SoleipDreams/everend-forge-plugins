using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace EverendForge.Unity.Editor
{
    public static class EverendRuntimePackageImporter
    {
        public static EverendNarrativeCatalog Import(string json, string sourceDescription, string outputRoot)
        {
            var analysis = EverendImportAnalyzer.Analyze(json);
            if (!analysis.IsValid) throw new InvalidOperationException(string.Join("\n", analysis.Errors));
            var packageId = Sanitize(analysis.Package.PackageId);
            var root = NormalizeAssetPath(outputRoot) + "/Catalog";
            EnsureFolder(root);
            var packagePath = root + "/" + packageId + ".RuntimePackage.asset";
            var catalogPath = root + "/" + packageId + ".NarrativeCatalog.asset";

            var package = AssetDatabase.LoadAssetAtPath<EverendRuntimePackageAsset>(packagePath);
            if (package == null) { package = ScriptableObject.CreateInstance<EverendRuntimePackageAsset>(); AssetDatabase.CreateAsset(package, packagePath); }
            package.Replace(json, sourceDescription);
            EditorUtility.SetDirty(package);

            var catalog = AssetDatabase.LoadAssetAtPath<EverendNarrativeCatalog>(catalogPath);
            if (catalog == null) { catalog = ScriptableObject.CreateInstance<EverendNarrativeCatalog>(); AssetDatabase.CreateAsset(catalog, catalogPath); }
            catalog.Rebuild(package);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            return catalog;
        }

        public static string NormalizeAssetPath(string root)
        {
            if (string.IsNullOrWhiteSpace(root)) return "Assets/EverendForge/Generated";
            root = root.Replace('\\', '/').TrimEnd('/');
            if (!root.StartsWith("Assets", StringComparison.Ordinal)) throw new ArgumentException("Managed output path must be inside Assets/.");
            return root;
        }
        public static void EnsureFolder(string assetFolder)
        {
            var parts = assetFolder.Split('/'); var path = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = path + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(path, parts[i]);
                path = next;
            }
        }
        public static string Sanitize(string value)
        {
            foreach (var character in Path.GetInvalidFileNameChars()) value = value.Replace(character, '_');
            return string.IsNullOrWhiteSpace(value) ? "runtime-package" : value;
        }
    }
}
