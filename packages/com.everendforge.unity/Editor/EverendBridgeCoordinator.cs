using System;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace EverendForge.Unity.Editor
{
    public sealed class EverendSyncCoordinator : IEverendSyncCoordinator
    {
        private readonly EverendBridgeSettings settings;

        public EverendSyncCoordinator(EverendBridgeSettings settings) { this.settings = settings ?? EverendBridgeSettings.Load(); }

        public EverendImportPlan Analyze(EverendSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            var plan = new EverendImportPlan
            {
                RuntimePackageJson = snapshot.RuntimePackageJson,
                SourceDescription = snapshot.SourceDescription,
                Revision = snapshot.Revision,
                IsLive = snapshot.IsLive,
                Analysis = EverendImportAnalyzer.Analyze(snapshot.RuntimePackageJson)
            };
            plan.Package = plan.Analysis.Package;
            return plan;
        }

        public EverendImportPlan AnalyzeBundle(string bundlePath)
        {
            if (string.IsNullOrWhiteSpace(bundlePath) || !Directory.Exists(bundlePath)) throw new DirectoryNotFoundException("Offline bundle folder does not exist: " + bundlePath);
            var packagePath = Path.Combine(bundlePath, "runtime-package.json");
            if (!File.Exists(packagePath)) throw new FileNotFoundException("Offline bundle is missing runtime-package.json.", packagePath);
            var plan = Analyze(new EverendSnapshot { RuntimePackageJson = File.ReadAllText(packagePath), SourceDescription = bundlePath, IsLive = false });
            var validationPath = Path.Combine(bundlePath, "validation-report.json");
            if (File.Exists(validationPath)) plan.Report.Warnings.Add("Bundle validation-report.json was loaded; contract-level validation is delegated to the source report in v0.1.");
            else plan.Report.Warnings.Add("Offline bundle has no validation-report.json; the Unity contract analyzer will be the blocking validation source.");
            if (!File.Exists(Path.Combine(bundlePath, "sinpo-projection.json"))) plan.Report.Warnings.Add("Bundle has no sinpo-projection.json; the selected Unity profile will be used.");
            if (!File.Exists(Path.Combine(bundlePath, "adapter-manifest.json"))) plan.Report.Warnings.Add("Bundle has no adapter-manifest.json; existing Unity manifest mappings will be used.");
            return plan;
        }

        public EverendApplyResult Apply(EverendImportPlan plan) { return ApplyInternal(plan, false); }

        public EverendApplyResult DryRun(EverendImportPlan plan) { return ApplyInternal(plan, true); }

        public Task<EverendApplyResult> ApplySnapshotAsync(EverendSnapshot snapshot)
        {
            return Task.FromResult(Apply(Analyze(snapshot)));
        }

        private EverendApplyResult ApplyInternal(EverendImportPlan plan, bool dryRun)
        {
            var result = new EverendApplyResult { DryRun = dryRun, Revision = plan == null ? 0 : plan.Revision, Report = plan == null ? new EverendBridgeReport() : plan.Report };
            if (plan == null || !plan.IsValid)
            {
                result.Report.Errors.Add("Import plan is invalid and cannot be applied.");
                result.Message = "Blocked by contract or projection validation.";
                return result;
            }
            var profile = settings.Profile;
            if (profile == null)
            {
                result.Report.Errors.Add("Select an EverendProjectionProfile before applying a projection.");
                result.Message = "No projection profile selected.";
                return result;
            }
            try
            {
                EverendNarrativeCatalog catalog;
                if (dryRun) catalog = BuildInMemoryCatalog(plan.RuntimePackageJson);
                else catalog = EverendRuntimePackageImporter.Import(plan.RuntimePackageJson, plan.SourceDescription, string.IsNullOrWhiteSpace(settings.outputRoot) ? profile.ManagedOutputRoot : settings.outputRoot);
                var projection = EverendSinpoProjection.Apply(catalog, profile, dryRun);
                result.Catalog = catalog;
                result.Report.Creates.AddRange(projection.Creates);
                result.Report.Updates.AddRange(projection.Updates);
                result.Report.Warnings.AddRange(projection.Warnings);
                result.Report.Errors.AddRange(projection.Errors);
                result.Success = projection.Success;
                result.Message = projection.ToString();
                if (result.Success && !dryRun)
                {
                    UpdateManifestMetadata(catalog, profile, plan);
                    settings.lastRevision = plan.Revision;
                    settings.Save();
                }
            }
            catch (Exception exception)
            {
                result.Report.Errors.Add(exception.Message);
                result.Success = false;
                result.Message = exception.ToString();
            }
            return result;
        }

        private static EverendNarrativeCatalog BuildInMemoryCatalog(string json)
        {
            var package = ScriptableObject.CreateInstance<EverendRuntimePackageAsset>();
            package.Replace(json, "dry-run");
            var catalog = ScriptableObject.CreateInstance<EverendNarrativeCatalog>();
            catalog.Rebuild(package);
            return catalog;
        }

        private void UpdateManifestMetadata(EverendNarrativeCatalog catalog, EverendProjectionProfile profile, EverendImportPlan plan)
        {
            var root = EverendRuntimePackageImporter.NormalizeAssetPath(profile.ManagedOutputRoot) + "/" + EverendRuntimePackageImporter.Sanitize(catalog.Package.PackageId) + "/" + profile.ProfileId;
            var manifestPath = root + "/" + EverendRuntimePackageImporter.Sanitize(catalog.Package.PackageId) + "." + profile.ProfileId + ".Manifest.asset";
            var manifest = AssetDatabase.LoadAssetAtPath<EverendProjectionManifest>(manifestPath);
            if (manifest == null) return;
            manifest.UpdateSyncMetadata(settings.projectId, settings.storyId, plan.Revision, "generated");
            settings.manifestPath = manifestPath;
            EditorUtility.SetDirty(manifest);
            AssetDatabase.SaveAssets();
        }
    }
}
