using System.Collections.Generic;

namespace EverendForge.Unity
{
    public interface IEverendNarrativeGateway
    {
        EverendRuntimePackage Package { get; }
        bool TryGetSequence(string id, out EverendSequence sequence);
        bool TryGetBranch(string id, out EverendBranch branch);
        bool TryGetEvent(string id, out EverendEvent narrativeEvent);
        IReadOnlyList<EverendTransition> GetOutgoingTransitions(string nodeId);
        IReadOnlyList<EverendDecision> GetDecisions(string eventId);
        bool TryResolveText(string textKey, out string text);
        bool TryGetScriptBlock(string scriptId, string blockId, out EverendScriptBlock block);
        bool TryResolveBeatLine(EverendBeat beat, out string speakerRef, out string text);
        bool TryGetNativeArtifact(string profileId, string everendId, out EverendNativeArtifact artifact);
    }
}
