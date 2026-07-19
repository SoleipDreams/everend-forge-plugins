using System.Collections.Generic;

namespace EverendForge.Unity.Editor
{
    /// <summary>Extension seam for project adapters. Hooks are editor-only and never leak game types into Runtime.</summary>
    public interface IEverendProjectionHook
    {
        string Id { get; }
        void Validate(EverendNarrativeCatalog catalog, EverendProjectionProfile profile, ICollection<string> errors, ICollection<string> warnings);
        void BeforeApply(EverendNarrativeCatalog catalog, EverendProjectionProfile profile, bool dryRun);
        void AfterApply(EverendNarrativeCatalog catalog, EverendProjectionProfile profile, bool dryRun);
    }

    public static class EverendProjectionHookRegistry
    {
        private static readonly List<IEverendProjectionHook> hooks = new List<IEverendProjectionHook>();
        public static IReadOnlyList<IEverendProjectionHook> Hooks => hooks;
        public static void Register(IEverendProjectionHook hook) { if (hook != null && !hooks.Contains(hook)) hooks.Add(hook); }
        public static void Unregister(IEverendProjectionHook hook) { hooks.Remove(hook); }
    }
}
