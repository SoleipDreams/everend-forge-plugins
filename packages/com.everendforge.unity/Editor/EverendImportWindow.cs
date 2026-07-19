using System;
using UnityEditor;
using UnityEngine;

namespace EverendForge.Unity.Editor
{
    public sealed class EverendImportWindow : EditorWindow
    {
        private TextAsset runtimePackage;
        private EverendProjectionProfile profile;
        private EverendImportAnalysis analysis;
        private Vector2 scroll;

        [MenuItem("Everend Forge/Import RuntimePackage...")]
        public static void Open() { GetWindow<EverendImportWindow>("Everend Import"); }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("RuntimePackage import", EditorStyles.boldLabel);
            runtimePackage = (TextAsset)EditorGUILayout.ObjectField("runtime-package.json", runtimePackage, typeof(TextAsset), false);
            profile = (EverendProjectionProfile)EditorGUILayout.ObjectField("Projection profile", profile, typeof(EverendProjectionProfile), false);
            using (new EditorGUI.DisabledScope(runtimePackage == null))
                if (GUILayout.Button("Analyze import")) analysis = EverendImportAnalyzer.Analyze(runtimePackage.text);

            if (analysis == null) return;
            EditorGUILayout.Space(); EditorGUILayout.HelpBox(analysis.Summary, analysis.IsValid ? MessageType.Info : MessageType.Error);
            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.MinHeight(130));
            foreach (var error in analysis.Errors) EditorGUILayout.HelpBox(error, MessageType.Error);
            foreach (var warning in analysis.Warnings) EditorGUILayout.HelpBox(warning, MessageType.Warning);
            EditorGUILayout.EndScrollView();

            using (new EditorGUI.DisabledScope(!analysis.IsValid))
            {
                if (GUILayout.Button("Import catalog")) ImportCatalog();
                using (new EditorGUI.DisabledScope(profile == null))
                    if (GUILayout.Button("Dry-run SINPO projection")) RunSinpo(true);
                using (new EditorGUI.DisabledScope(profile == null))
                    if (GUILayout.Button("Apply SINPO projection")) RunSinpo(false);
            }
        }

        private void ImportCatalog()
        {
            try
            {
                var root = profile == null ? "Assets/EverendForge/Generated" : profile.ManagedOutputRoot;
                var catalog = EverendRuntimePackageImporter.Import(runtimePackage.text, AssetDatabase.GetAssetPath(runtimePackage), root);
                Selection.activeObject = catalog; EditorGUIUtility.PingObject(catalog); ShowNotification(new GUIContent("Catalog imported."));
            }
            catch (Exception exception) { Debug.LogException(exception); }
        }
        private void RunSinpo(bool dryRun)
        {
            try
            {
                var catalog = EverendRuntimePackageImporter.Import(runtimePackage.text, AssetDatabase.GetAssetPath(runtimePackage), profile.ManagedOutputRoot);
                var report = EverendSinpoProjection.Apply(catalog, profile, dryRun);
                Debug.Log(report.ToString());
                ShowNotification(new GUIContent(dryRun ? "Dry-run logged to Console." : "SINPO projection applied."));
            }
            catch (Exception exception) { Debug.LogException(exception); }
        }
    }
}
