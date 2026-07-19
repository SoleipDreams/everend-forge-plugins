using System;
using System.Collections.Generic;

namespace EverendForge.Unity
{
    public sealed class EverendNarrativeGateway : IEverendNarrativeGateway
    {
        private readonly EverendNarrativeCatalog catalog;
        private readonly IEnumerable<EverendProjectionManifest> manifests;

        public EverendNarrativeGateway(EverendNarrativeCatalog source, IEnumerable<EverendProjectionManifest> projectionManifests = null)
        {
            catalog = source ?? throw new ArgumentNullException(nameof(source));
            manifests = projectionManifests ?? Array.Empty<EverendProjectionManifest>();
        }

        public EverendRuntimePackage Package => catalog.Package;
        public bool TryGetSequence(string id, out EverendSequence sequence) => catalog.TryGetSequence(id, out sequence);
        public bool TryGetBranch(string id, out EverendBranch branch) => catalog.TryGetBranch(id, out branch);
        public bool TryGetEvent(string id, out EverendEvent narrativeEvent) => catalog.TryGetEvent(id, out narrativeEvent);
        public IReadOnlyList<EverendTransition> GetOutgoingTransitions(string nodeId) => catalog.GetOutgoingTransitions(nodeId);
        public IReadOnlyList<EverendDecision> GetDecisions(string eventId)
        {
            EverendEvent narrativeEvent; return catalog.TryGetEvent(eventId, out narrativeEvent) ? narrativeEvent.Decisions : EverendCollection.EmptyDecisions;
        }
        public bool TryResolveText(string textKey, out string text)
        {
            text = null;
            if (string.IsNullOrWhiteSpace(textKey)) return false;
            return Package.Localizations.TryGetValue(textKey, out text);
        }
        public bool TryGetNativeArtifact(string profileId, string everendId, out EverendNativeArtifact artifact)
        {
            foreach (var manifest in manifests)
                if (manifest != null && manifest.ProfileId == profileId && manifest.TryGet(everendId, out artifact)) return true;
            artifact = null; return false;
        }
    }
}
