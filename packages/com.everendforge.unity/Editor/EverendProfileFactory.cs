using UnityEditor;
using UnityEngine;

namespace EverendForge.Unity.Editor
{
    internal static class EverendProfileFactory
    {
        [MenuItem("Everend Forge/Create SINPO v0.1 Profile")]
        private static void CreateSinpoProfile()
        {
            var path = EditorUtility.SaveFilePanelInProject("Create SINPO profile", "SINPO-v0.1", "asset", "Choose the profile location.");
            if (string.IsNullOrWhiteSpace(path)) return;
            var profile = ScriptableObject.CreateInstance<EverendProjectionProfile>();
            AssetDatabase.CreateAsset(profile, path); AssetDatabase.SaveAssets(); Selection.activeObject = profile;
        }
    }
}
