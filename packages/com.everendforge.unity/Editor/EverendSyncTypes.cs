using System.Threading.Tasks;

namespace EverendForge.Unity.Editor
{
    public sealed class EverendImportPlan
    {
        public string SourceDescription;
        public string RuntimePackageJson;
        public EverendRuntimePackage Package;
        public EverendImportAnalysis Analysis;
        public int Revision;
        public bool IsLive;
        public EverendBridgeReport Report = new EverendBridgeReport();
        public bool IsValid { get { return Analysis != null && Analysis.IsValid && Report.Errors.Count == 0; } }
    }

    public sealed class EverendApplyResult
    {
        public bool Success;
        public bool DryRun;
        public int Revision;
        public string Message;
        public EverendBridgeReport Report = new EverendBridgeReport();
        public EverendNarrativeCatalog Catalog;
    }

    public interface IEverendSyncCoordinator
    {
        EverendImportPlan Analyze(EverendSnapshot snapshot);
        EverendImportPlan AnalyzeBundle(string bundlePath);
        EverendApplyResult Apply(EverendImportPlan plan);
        Task<EverendApplyResult> ApplySnapshotAsync(EverendSnapshot snapshot);
    }
}
