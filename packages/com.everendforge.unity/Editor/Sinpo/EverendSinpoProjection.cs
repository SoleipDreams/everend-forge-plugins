using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace EverendForge.Unity.Editor
{
    public sealed class EverendProjectionReport
    {
        public bool DryRun { get; internal set; }
        public List<string> Creates { get; } = new List<string>();
        public List<string> Updates { get; } = new List<string>();
        public List<string> Warnings { get; } = new List<string>();
        public List<string> Errors { get; } = new List<string>();
        public bool Success => Errors.Count == 0;
        public override string ToString()
        {
            var title = "Everend SINPO projection " + (DryRun ? "dry-run" : "apply") + ": " + (Success ? "OK" : "FAILED");
            return title + "\nCreates:\n- " + string.Join("\n- ", Creates) + "\nUpdates:\n- " + string.Join("\n- ", Updates) + (Warnings.Count == 0 ? string.Empty : "\nWarnings:\n- " + string.Join("\n- ", Warnings)) + (Errors.Count == 0 ? string.Empty : "\nErrors:\n- " + string.Join("\n- ", Errors));
        }
    }

    /// <summary>SINPO adapter. It talks to game ScriptableObjects only through SerializedObject, never from Runtime.</summary>
    public static class EverendSinpoProjection
    {
        public static EverendProjectionReport Apply(EverendNarrativeCatalog catalog, EverendProjectionProfile profile, bool dryRun)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            var report = new EverendProjectionReport { DryRun = dryRun };
            if (!profile.Sinpo.Enabled) { report.Errors.Add("The selected profile does not enable the SINPO adapter."); return report; }
            if (!catalog.IsCurrent) report.Warnings.Add("Catalog source hash differs from its RuntimePackage. It will be rebuilt by the next import.");

            foreach (var sequence in catalog.Package.Story.Sequences)
                if (!profile.TryGetCharacter(sequence.CharacterRef, out _)) report.Errors.Add("Sequence '" + sequence.Id + "' has no character mapping for '" + sequence.CharacterRef + "'.");
            foreach (var external in catalog.Package.Story.ExternalFunctions)
                if (!profile.Sinpo.AllowedExternalFunctions.Contains(external)) report.Errors.Add("RuntimePackage requests external '" + external + "', which is not allowed by profile '" + profile.ProfileId + "'.");
            if (!profile.Sinpo.AllowedExternalFunctions.Contains("SetNextEvent")) report.Errors.Add("SINPO v0.1 requires SetNextEvent in the allowed external functions.");
            ValidateAcyclicEventGraph(catalog, report);
            foreach (var hook in EverendProjectionHookRegistry.Hooks) hook.Validate(catalog, profile, report.Errors, report.Warnings);
            if (report.Errors.Count > 0) return report;

            var root = EverendRuntimePackageImporter.NormalizeAssetPath(profile.ManagedOutputRoot) + "/" + EverendRuntimePackageImporter.Sanitize(catalog.Package.PackageId) + "/" + profile.ProfileId;
            var manifestPath = root + "/" + EverendRuntimePackageImporter.Sanitize(catalog.Package.PackageId) + "." + profile.ProfileId + ".Manifest.asset";
            var manifest = AssetDatabase.LoadAssetAtPath<EverendProjectionManifest>(manifestPath);
            var ids = NativeIds(catalog, manifest);
            var generator = new EverendSinpoInkGenerator(catalog, id => ids[id]);
            var ink = new Dictionary<string, string>();
            var globalInclude = RelativeInkInclude(root + "/Ink", profile.Sinpo.GlobalInkIncludePath, report);
            ink["EverendGlobals.ink"] = generator.GenerateGlobals(globalInclude);
            foreach (var narrativeEvent in catalog.Package.Story.Events) ink[EventInkFile(narrativeEvent.Id)] = generator.GenerateEvent(narrativeEvent);
            report.Errors.AddRange(generator.Errors.Distinct());
            if (report.Errors.Count > 0) return report;

            PlanFiles(root + "/Ink", ink, report);
            PlanAssets(catalog, profile, root, manifest, report);
            if (dryRun) return report;

            foreach (var hook in EverendProjectionHookRegistry.Hooks) hook.BeforeApply(catalog, profile, false);

            EverendRuntimePackageImporter.EnsureFolder(root);
            EverendRuntimePackageImporter.EnsureFolder(root + "/Ink");
            WriteInk(root + "/Ink", ink);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (var narrativeEvent in catalog.Package.Story.Events)
            {
                var jsonPath = root + "/Ink/" + Path.ChangeExtension(EventInkFile(narrativeEvent.Id), ".json");
                if (AssetDatabase.LoadAssetAtPath<TextAsset>(jsonPath) == null) report.Errors.Add("Ink compiler did not produce '" + jsonPath + "'. Install/configure the Ink Unity importer and re-run Apply.");
            }
            if (report.Errors.Count > 0) return report;

            var sequenceType = FindScriptableObjectType(profile.Sinpo.SequenceTypeName);
            var branchType = FindScriptableObjectType(profile.Sinpo.BranchTypeName);
            var eventType = FindScriptableObjectType(profile.Sinpo.EventTypeName);
            if (sequenceType == null || branchType == null || eventType == null)
            {
                report.Errors.Add("Could not resolve SINPO ScriptableObject types. Expected " + profile.Sinpo.SequenceTypeName + ", " + profile.Sinpo.BranchTypeName + " and " + profile.Sinpo.EventTypeName + "."); return report;
            }
            if (manifest == null) { manifest = ScriptableObject.CreateInstance<EverendProjectionManifest>(); AssetDatabase.CreateAsset(manifest, manifestPath); }
            manifest.BeginUpdate(profile, catalog.SourcePackage);

            var eventAssets = new Dictionary<string, UnityEngine.Object>();
            var branchAssets = new Dictionary<string, UnityEngine.Object>();
            var sequenceAssets = new Dictionary<string, UnityEngine.Object>();
            foreach (var item in catalog.Package.Story.Events) eventAssets[item.Id] = FindOrCreate(eventType, root + "/Events", "Event_" + EverendRuntimePackageImporter.Sanitize(item.Id) + ".asset", manifest, item.Id, "event", report);
            foreach (var item in catalog.Package.Story.Branches) branchAssets[item.Id] = FindOrCreate(branchType, root + "/Branches", "Branch_" + EverendRuntimePackageImporter.Sanitize(item.Id) + ".asset", manifest, item.Id, "branch", report);
            foreach (var item in catalog.Package.Story.Sequences) sequenceAssets[item.Id] = FindOrCreate(sequenceType, root + "/Sequences", "Sequence_" + EverendRuntimePackageImporter.Sanitize(item.Id) + ".asset", manifest, item.Id, "sequence", report);

            foreach (var item in catalog.Package.Story.Events) ConfigureEvent(item, eventAssets[item.Id], eventAssets, branchAssets, ids, root, profile, manifest, report);
            foreach (var item in catalog.Package.Story.Branches) ConfigureBranch(item, branchAssets[item.Id], eventAssets, profile, manifest, report);
            foreach (var item in catalog.Package.Story.Sequences) ConfigureSequence(item, sequenceAssets[item.Id], eventAssets, branchAssets, profile, manifest, report);
            if (profile.Sinpo.ConfigureAddressables) ConfigureAddressables(catalog, profile, eventAssets, branchAssets, sequenceAssets, report);
            EditorUtility.SetDirty(manifest); AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            foreach (var hook in EverendProjectionHookRegistry.Hooks) hook.AfterApply(catalog, profile, false);
            return report;
        }

        private static Dictionary<string, string> NativeIds(EverendNarrativeCatalog catalog, EverendProjectionManifest manifest)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var narrativeEvent in catalog.Package.Story.Events)
            {
                EverendNativeArtifact current;
                result[narrativeEvent.Id] = manifest != null && manifest.TryGet(narrativeEvent.Id, out current) && !string.IsNullOrWhiteSpace(current.NativeId) ? current.NativeId : "ev_" + StableHash(narrativeEvent.Id).Substring(0, 12);
            }
            return result;
        }
        private static void ValidateAcyclicEventGraph(EverendNarrativeCatalog catalog, EverendProjectionReport report)
        {
            var visiting = new HashSet<string>(StringComparer.Ordinal); var visited = new HashSet<string>(StringComparer.Ordinal);
            foreach (var narrativeEvent in catalog.Package.Story.Events) Visit(narrativeEvent.Id);
            void Visit(string id)
            {
                if (visited.Contains(id)) return;
                if (!visiting.Add(id)) { report.Errors.Add("SINPO v0.1 does not support cyclic event graphs (cycle at '" + id + "')."); return; }
                EverendEvent narrativeEvent;
                if (catalog.TryGetEvent(id, out narrativeEvent))
                    foreach (var transition in narrativeEvent.Transitions.Where(x => x.From == id)) if (!string.IsNullOrWhiteSpace(transition.To)) Visit(transition.To);
                visiting.Remove(id); visited.Add(id);
            }
        }
        private static void PlanFiles(string root, Dictionary<string, string> files, EverendProjectionReport report)
        {
            foreach (var file in files) (File.Exists(Absolute(filePath: root + "/" + file.Key)) ? report.Updates : report.Creates).Add("Ink " + root + "/" + file.Key);
        }
        private static void PlanAssets(EverendNarrativeCatalog catalog, EverendProjectionProfile profile, string root, EverendProjectionManifest manifest, EverendProjectionReport report)
        {
            foreach (var item in catalog.Package.Story.Events) PlanArtifact(manifest, item.Id, "Event", root + "/Events", report);
            foreach (var item in catalog.Package.Story.Branches) PlanArtifact(manifest, item.Id, "Branch", root + "/Branches", report);
            foreach (var item in catalog.Package.Story.Sequences) PlanArtifact(manifest, item.Id, "Sequence", root + "/Sequences", report);
            if (manifest == null) report.Creates.Add("Manifest " + root);
            else report.Updates.Add("Manifest " + AssetDatabase.GetAssetPath(manifest));
        }
        private static void PlanArtifact(EverendProjectionManifest manifest, string id, string kind, string root, EverendProjectionReport report)
        {
            EverendNativeArtifact artifact;
            if (manifest != null && manifest.TryGet(id, out artifact) && artifact.NativeAsset != null) report.Updates.Add(kind + " " + id + " -> " + artifact.AssetPath);
            else report.Creates.Add(kind + " " + id + " -> " + root);
        }
        private static void WriteInk(string root, Dictionary<string, string> files)
        {
            foreach (var file in files) File.WriteAllText(Absolute(root + "/" + file.Key), file.Value, new UTF8Encoding(false));
        }
        private static UnityEngine.Object FindOrCreate(Type type, string folder, string fileName, EverendProjectionManifest manifest, string id, string kind, EverendProjectionReport report)
        {
            EverendRuntimePackageImporter.EnsureFolder(folder);
            var assetPath = folder + "/" + fileName;
            EverendNativeArtifact artifact; UnityEngine.Object asset = manifest.TryGet(id, out artifact) ? artifact.NativeAsset : null;
            if (asset == null) asset = AssetDatabase.LoadAssetAtPath(assetPath, type);
            if (asset == null) { asset = ScriptableObject.CreateInstance(type); AssetDatabase.CreateAsset(asset, assetPath); report.Creates.Add(kind + " " + id + " -> " + assetPath); }
            else report.Updates.Add(kind + " " + id + " -> " + AssetDatabase.GetAssetPath(asset));
            artifact = manifest.GetOrCreate(id, kind); artifact.NativeAsset = asset; artifact.AssetPath = AssetDatabase.GetAssetPath(asset); return asset;
        }
        private static void ConfigureEvent(EverendEvent item, UnityEngine.Object asset, Dictionary<string, UnityEngine.Object> eventAssets, Dictionary<string, UnityEngine.Object> branchAssets, Dictionary<string, string> ids, string root, EverendProjectionProfile profile, EverendProjectionManifest manifest, EverendProjectionReport report)
        {
            var serialized = new SerializedObject(asset);
            var settings = profile.Sinpo;
            SetString(serialized, settings.EventIdField, ids[item.Id], report); SetString(serialized, settings.EventNameField, item.Name, report); SetString(serialized, settings.EventDescriptionField, item.Text?.Content ?? string.Empty, report);
            SetEnum(serialized, settings.EventTypeField, item.Type, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { { "normal", "Normal" }, { "exploration", "Exploration" }, { "final", "Final" } }, report);
            UnityEngine.Object branch; if (!string.IsNullOrWhiteSpace(item.BranchId) && branchAssets.TryGetValue(item.BranchId, out branch)) SetObject(serialized, settings.EventBranchField, branch, report);
            var inkJsonPath = root + "/Ink/" + Path.ChangeExtension(EventInkFile(item.Id), ".json"); var inkJson = AssetDatabase.LoadAssetAtPath<TextAsset>(inkJsonPath);
            if (inkJson == null) report.Errors.Add("Ink JSON was unexpectedly missing for '" + item.Id + "' at " + inkJsonPath + "."); else SetObject(serialized, settings.EventInkJsonField, inkJson, report);
            var next = catalogTransitions(item).Select(x => eventAssets.ContainsKey(x.To) ? eventAssets[x.To] : null).Where(x => x != null).ToList(); SetObjects(serialized, settings.EventNextEventsField, next, report);
            serialized.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(asset);
            var artifact = manifest.GetOrCreate(item.Id, "event"); artifact.NativeId = ids[item.Id]; artifact.ContentHash = StableHash(item.Id + (item.Text?.Content ?? string.Empty));
        }
        private static IEnumerable<EverendTransition> catalogTransitions(EverendEvent item) { return item.Transitions.Where(x => x.From == item.Id); }
        private static void ConfigureBranch(EverendBranch item, UnityEngine.Object asset, Dictionary<string, UnityEngine.Object> eventAssets, EverendProjectionProfile profile, EverendProjectionManifest manifest, EverendProjectionReport report)
        {
            var settings = profile.Sinpo; var serialized = new SerializedObject(asset); SetString(serialized, settings.BranchIdField, item.Id, report); SetString(serialized, settings.BranchTitleField, item.Title, report); SetString(serialized, settings.BranchDescriptionField, item.Description, report);
            SetObjects(serialized, settings.BranchEventsField, item.EventIds.Where(eventAssets.ContainsKey).Select(id => eventAssets[id]).ToList(), report); serialized.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(asset); manifest.GetOrCreate(item.Id, "branch").ContentHash = StableHash(item.Id + item.Title + item.Description);
        }
        private static void ConfigureSequence(EverendSequence item, UnityEngine.Object asset, Dictionary<string, UnityEngine.Object> eventAssets, Dictionary<string, UnityEngine.Object> branchAssets, EverendProjectionProfile profile, EverendProjectionManifest manifest, EverendProjectionReport report)
        {
            var settings = profile.Sinpo; var serialized = new SerializedObject(asset); EverendCharacterMapping character; profile.TryGetCharacter(item.CharacterRef, out character); SetEnum(serialized, settings.SequenceCharacterField, character.UnityEnumValue, null, report);
            UnityEngine.Object entry; if (eventAssets.TryGetValue(item.EntryEventId, out entry)) SetObject(serialized, settings.SequenceStartingEventField, entry, report);
            SetObjects(serialized, settings.SequenceEventsField, item.EventIds.Where(eventAssets.ContainsKey).Select(id => eventAssets[id]).ToList(), report); SetObjects(serialized, settings.SequenceBranchesField, item.BranchIds.Where(branchAssets.ContainsKey).Select(id => branchAssets[id]).ToList(), report);
            serialized.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(asset); manifest.GetOrCreate(item.Id, "sequence").ContentHash = StableHash(item.Id + item.Name + item.CharacterRef);
        }
        private static void SetString(SerializedObject source, string name, string value, EverendProjectionReport report) { var property = source.FindProperty(name); if (property == null) report.Warnings.Add("Missing Unity-owned field '" + name + "' on " + source.targetObject.GetType().Name + "."); else property.stringValue = value ?? string.Empty; }
        private static void SetObject(SerializedObject source, string name, UnityEngine.Object value, EverendProjectionReport report) { var property = source.FindProperty(name); if (property == null) report.Warnings.Add("Missing Unity-owned field '" + name + "' on " + source.targetObject.GetType().Name + "."); else property.objectReferenceValue = value; }
        private static void SetObjects(SerializedObject source, string name, List<UnityEngine.Object> values, EverendProjectionReport report) { var property = source.FindProperty(name); if (property == null || !property.isArray) { report.Warnings.Add("Missing list field '" + name + "' on " + source.targetObject.GetType().Name + "."); return; } property.arraySize = values.Count; for (var i = 0; i < values.Count; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i]; }
        private static void SetEnum(SerializedObject source, string name, string value, Dictionary<string, string> aliases, EverendProjectionReport report) { var property = source.FindProperty(name); if (property == null || property.propertyType != SerializedPropertyType.Enum) { report.Warnings.Add("Missing enum field '" + name + "' on " + source.targetObject.GetType().Name + "."); return; } string wanted; if (aliases != null && aliases.TryGetValue(value ?? string.Empty, out wanted)) value = wanted; var index = Array.FindIndex(property.enumNames, x => string.Equals(x, value, StringComparison.OrdinalIgnoreCase)); if (index < 0) report.Warnings.Add("Enum '" + name + "' has no value '" + value + "'."); else property.enumValueIndex = index; }
        private static Type FindScriptableObjectType(string simpleName) { return TypeCache.GetTypesDerivedFrom<ScriptableObject>().FirstOrDefault(type => type.Name == simpleName || type.FullName == simpleName); }
        private static void ConfigureAddressables(EverendNarrativeCatalog catalog, EverendProjectionProfile profile, Dictionary<string, UnityEngine.Object> events, Dictionary<string, UnityEngine.Object> branches, Dictionary<string, UnityEngine.Object> sequences, EverendProjectionReport report)
        {
            var settingType = AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType("UnityEditor.AddressableAssets.Settings.AddressableAssetSettingsDefaultObject")).FirstOrDefault(type => type != null);
            if (settingType == null) { report.Warnings.Add("Addressables is not installed; Addressables labels were not configured."); return; }
            var settings = settingType.GetProperty("Settings", BindingFlags.Public | BindingFlags.Static)?.GetValue(null, null); if (settings == null) { report.Warnings.Add("Addressables has no active settings asset."); return; }
            foreach (var sequence in catalog.Package.Story.Sequences)
            {
                EverendCharacterMapping mapping; if (!profile.TryGetCharacter(sequence.CharacterRef, out mapping) || string.IsNullOrWhiteSpace(mapping.AddressablesLabel)) continue;
                Label(events.Where(pair => sequence.EventIds.Contains(pair.Key)).Select(pair => pair.Value), mapping.AddressablesLabel, settings, report);
                Label(branches.Where(pair => sequence.BranchIds.Contains(pair.Key)).Select(pair => pair.Value), mapping.AddressablesLabel, settings, report); UnityEngine.Object asset; if (sequences.TryGetValue(sequence.Id, out asset)) Label(new[] { asset }, mapping.AddressablesLabel, settings, report);
            }
        }
        private static void Label(IEnumerable<UnityEngine.Object> assets, string label, object settings, EverendProjectionReport report)
        {
            var type = settings.GetType(); var find = type.GetMethods().FirstOrDefault(method => method.Name == "FindAssetEntry" && method.GetParameters().Length == 1); var create = type.GetMethods().FirstOrDefault(method => method.Name == "CreateOrMoveEntry" && method.GetParameters().Length >= 2);
            if (find == null || create == null) { report.Warnings.Add("Addressables API shape is unsupported; labels were skipped."); return; }
            var group = type.GetProperty("DefaultGroup")?.GetValue(settings, null);
            foreach (var asset in assets)
            {
                var guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset)); var entry = find.Invoke(settings, new object[] { guid });
                if (entry == null) { var parameters = create.GetParameters(); var values = new object[parameters.Length]; values[0] = guid; values[1] = group; for (var i = 2; i < values.Length; i++) values[i] = parameters[i].ParameterType == typeof(bool) ? (object)true : null; entry = create.Invoke(settings, values); }
                var setLabel = entry.GetType().GetMethods().FirstOrDefault(method => method.Name == "SetLabel" && method.GetParameters().Length >= 1); if (setLabel != null) { var parameters = setLabel.GetParameters(); var values = new object[parameters.Length]; values[0] = label; for (var i = 1; i < values.Length; i++) values[i] = parameters[i].ParameterType == typeof(bool) ? (object)true : null; setLabel.Invoke(entry, values); }
            }
        }
        private static string EventInkFile(string id) { return "Event_" + EverendRuntimePackageImporter.Sanitize(id) + ".ink"; }
        private static string RelativeInkInclude(string fromAssetFolder, string includeAssetPath, EverendProjectionReport report)
        {
            if (string.IsNullOrWhiteSpace(includeAssetPath)) return string.Empty; if (!includeAssetPath.StartsWith("Assets/", StringComparison.Ordinal)) { report.Errors.Add("Global Ink include must be an Assets/ path."); return string.Empty; }
            var project = Directory.GetParent(Application.dataPath).FullName; var from = new Uri(Path.GetFullPath(Path.Combine(project, fromAssetFolder)) + Path.DirectorySeparatorChar); var target = new Uri(Path.GetFullPath(Path.Combine(project, includeAssetPath))); return Uri.UnescapeDataString(from.MakeRelativeUri(target).ToString());
        }
        private static string Absolute(string filePath) { return Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath).FullName, filePath)); }
        private static string StableHash(string text) { using (var sha = SHA256.Create()) { return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text ?? string.Empty))).Replace("-", string.Empty).ToLowerInvariant(); } }
    }
}
