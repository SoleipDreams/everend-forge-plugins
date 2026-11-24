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
        public bool TryGetScriptBlock(string scriptId, string blockId, out EverendScriptBlock block) => catalog.TryGetBlock(scriptId, blockId, out block);
        public bool TryResolveBeatLine(EverendBeat beat, out string speakerRef, out string text)
        {
            speakerRef = null; text = null;
            if (beat == null) return false;
            EverendScriptBlock block;
            if (!catalog.TryGetBlock(beat.ScriptId, beat.BlockId, out block)) return false;
            speakerRef = string.IsNullOrWhiteSpace(block.CharacterRef) ? block.SpeakerRef : block.CharacterRef;
            var textKey = string.IsNullOrWhiteSpace(block.TextKey) ? "script." + beat.ScriptId + "." + beat.BlockId : block.TextKey;
            if (TryResolveText(textKey, out text)) return true;
            text = block.Content;
            return !string.IsNullOrEmpty(text);
        }
        public bool TryGetNativeArtifact(string profileId, string everendId, out EverendNativeArtifact artifact)
        {
            foreach (var manifest in manifests)
                if (manifest != null && manifest.ProfileId == profileId && manifest.TryGet(everendId, out artifact)) return true;
            artifact = null; return false;
        }
    }
}
