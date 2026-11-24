using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace EverendForge.Unity.Editor
{
    public sealed class EverendInventoryItem
    {
        public string Kind;
        public string Path;
        public string Guid;
        public string EverendId;
        public string Mode;
    }

    public sealed class EverendInventory
    {
        public List<EverendInventoryItem> Items = new List<EverendInventoryItem>();
        public string GeneratedRoot;
        public string ScannedAtUtc;
    }

    public static class EverendInventoryScanner
    {
        public static EverendInventory Scan(EverendProjectionProfile profile)
        {
            var inventory = new EverendInventory { GeneratedRoot = profile == null ? string.Empty : profile.ManagedOutputRoot, ScannedAtUtc = DateTime.UtcNow.ToString("O") };
            var roots = new[] { "Assets" };
            foreach (var root in roots)
            {
                foreach (var path in AssetDatabase.FindAssets("t:ScriptableObject", new[] { root }))
                {
                    var assetPath = AssetDatabase.GUIDToAssetPath(path);
                    var type = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
                    if (type == null) continue;
                    inventory.Items.Add(new EverendInventoryItem { Kind = type.Name, Path = assetPath, Guid = path, Mode = IsGenerated(assetPath, inventory.GeneratedRoot) ? "generated" : "linked" });
                }
                foreach (var path in AssetDatabase.FindAssets("t:TextAsset", new[] { root }))
                {
                    var assetPath = AssetDatabase.GUIDToAssetPath(path);
                    if (!assetPath.EndsWith(".ink", StringComparison.OrdinalIgnoreCase) && !assetPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) continue;
                    inventory.Items.Add(new EverendInventoryItem { Kind = assetPath.EndsWith(".ink", StringComparison.OrdinalIgnoreCase) ? "Ink" : "TextAsset", Path = assetPath, Guid = path, Mode = IsGenerated(assetPath, inventory.GeneratedRoot) ? "generated" : "linked" });
                }
            }
            return inventory;
        }

        public static string ToJson(EverendInventory inventory)
        {
            var values = new List<string>();
            foreach (var item in inventory.Items) values.Add("{\"kind\":\"" + Escape(item.Kind) + "\",\"path\":\"" + Escape(item.Path) + "\",\"guid\":\"" + Escape(item.Guid) + "\",\"everendId\":\"" + Escape(item.EverendId) + "\",\"mode\":\"" + Escape(item.Mode) + "\"}");
            return "{\"generatedRoot\":\"" + Escape(inventory.GeneratedRoot) + "\",\"scannedAtUtc\":\"" + Escape(inventory.ScannedAtUtc) + "\",\"items\":[" + string.Join(",", values.ToArray()) + "]}";
        }

        private static bool IsGenerated(string path, string root) { return !string.IsNullOrWhiteSpace(root) && path.StartsWith(root.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase); }
        private static string Escape(string value) { return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\""); }
    }
}
