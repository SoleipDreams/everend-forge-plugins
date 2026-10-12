using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace EverendForge.Unity.Editor
{
    [Serializable]
    public sealed class EverendBridgeSettings
    {
        public string endpoint = "http://127.0.0.1:47831";
        public string projectId = "";
        public string storyId = "";
        public string profilePath = "";
        public string outputRoot = "Assets/EverendForge/Generated";
        public string mode = "live";
        public bool autoSync;
        public bool showTerminal = true;
        public int lastRevision;
        public string manifestPath = "";
        public string daemonExecutable = "";
        public string locale = "und";
        public string manualPackagePath = "";
        public string manualBundlePath = "";

        private static string FilePath
        {
            get
            {
                var projectRoot = Directory.GetParent(Application.dataPath).FullName;
                return Path.Combine(projectRoot, "ProjectSettings", "EverendForgeSync.json");
            }
        }

        public bool IsLive { get { return string.Equals(mode, "live", StringComparison.OrdinalIgnoreCase); } }

        public EverendProjectionProfile Profile
        {
            get { return string.IsNullOrWhiteSpace(profilePath) ? null : AssetDatabase.LoadAssetAtPath<EverendProjectionProfile>(profilePath); }
        }

        public static EverendBridgeSettings Load()
        {
            EverendBridgeSettings settings;
            try { settings = File.Exists(FilePath) ? JsonUtility.FromJson<EverendBridgeSettings>(File.ReadAllText(FilePath)) : new EverendBridgeSettings(); }
            catch { settings = new EverendBridgeSettings(); }
            return settings ?? new EverendBridgeSettings();
        }

        public void Save()
        {
            var directory = Path.GetDirectoryName(FilePath);
            if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(FilePath, JsonUtility.ToJson(this, true));
            AssetDatabase.Refresh();
        }

        public string GetToken()
        {
            return EditorPrefs.GetString("EverendForge.Sync.Token." + (projectId ?? string.Empty), string.Empty);
        }

        public void SetToken(string value)
        {
            EditorPrefs.SetString("EverendForge.Sync.Token." + (projectId ?? string.Empty), value ?? string.Empty);
        }

        public string ResolveDaemonExecutable()
        {
            if (!string.IsNullOrWhiteSpace(daemonExecutable)) return daemonExecutable.Trim();
            return FindDaemonExecutable();
        }

        public static string FindDaemonExecutable()
        {
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            var candidates = new[]
            {
                Path.Combine(projectRoot, "everend-forge-bridge.cmd"),
                Path.Combine(projectRoot, "Tools", "everend-forge-bridge.cmd"),
                Path.Combine(projectRoot, "..", "everend-forge-bridge", "everend-forge-bridge.cmd"),
                Path.Combine(projectRoot, "..", "..", "..", "products", "everend", "forge", "repos", "everend-forge-bridge", "everend-forge-bridge.cmd"),
                Path.Combine(projectRoot, "Packages", "com.everendforge.unity", "..", "..", "everend-forge-bridge", "everend-forge-bridge.cmd"),
            };

            foreach (var candidate in candidates)
            {
                var fullPath = Path.GetFullPath(candidate);
                if (File.Exists(fullPath)) return fullPath;
            }

            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.everendforge.unity");
            if (packageInfo != null)
            {
                var packagedLauncher = Path.Combine(packageInfo.resolvedPath, "Editor", "BridgeRuntime", "everend-forge-bridge.cmd");
                if (File.Exists(packagedLauncher)) return packagedLauncher;
            }

            return string.Empty;
        }

        public void ApplyProfileDefaults(EverendProjectionProfile profile)
        {
            if (profile == null) return;
            profilePath = AssetDatabase.GetAssetPath(profile);
            outputRoot = profile.ManagedOutputRoot;
        }
    }
}
