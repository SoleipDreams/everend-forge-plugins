using System;
using System.Collections.Generic;
using System.Linq;

namespace EverendForge.Unity
{
    public sealed class EverendRuntimePackage
    {
        public string SpecificationVersion { get; internal set; }
        public string PackageId { get; internal set; }
        public string EntryNodeId { get; internal set; }
        public string PrimaryLocale { get; internal set; }
        public IReadOnlyDictionary<string, object> Variables { get; internal set; }
        public IReadOnlyDictionary<string, string> Localizations { get; internal set; }
        public EverendStory Story { get; internal set; }
    }

    public sealed class EverendStory
    {
        public string ProjectId { get; internal set; }
        public string EntrySequenceId { get; internal set; }
        public IReadOnlyList<EverendSequence> Sequences { get; internal set; }
        public IReadOnlyList<EverendBranch> Branches { get; internal set; }
        public IReadOnlyList<EverendEvent> Events { get; internal set; }
        public IReadOnlyList<EverendScriptDocument> ScriptDocuments { get; internal set; }
        public IReadOnlyList<string> ExternalFunctions { get; internal set; }
    }

    public sealed class EverendSequence
    {
        public string Id { get; internal set; }
        public string Name { get; internal set; }
        public string CharacterRef { get; internal set; }
        public string EntryEventId { get; internal set; }
        public IReadOnlyList<string> EventIds { get; internal set; }
        public IReadOnlyList<string> BranchIds { get; internal set; }
    }

    public sealed class EverendBranch
    {
        public string Id { get; internal set; }
        public string Title { get; internal set; }
        public string Description { get; internal set; }
        public string SequenceId { get; internal set; }
        public IReadOnlyList<string> EventIds { get; internal set; }
    }

    public sealed class EverendEvent
    {
        public string Id { get; internal set; }
        public string Name { get; internal set; }
        public string Type { get; internal set; }
        public string SequenceId { get; internal set; }
        public string BranchId { get; internal set; }
        public EverendText Text { get; internal set; }
        public IReadOnlyList<EverendDecision> Decisions { get; internal set; }
        public IReadOnlyList<EverendTransition> Transitions { get; internal set; }
        public IReadOnlyList<EverendBeat> DialogueBeats { get; internal set; }
        public IReadOnlyDictionary<string, object> Raw { get; internal set; }
    }

    public sealed class EverendText
    {
        public string Format { get; internal set; }
        public string Content { get; internal set; }
    }

    public sealed class EverendDecision
    {
        public string Id { get; internal set; }
        public string Name { get; internal set; }
        public string Description { get; internal set; }
        public string Type { get; internal set; }
        public IReadOnlyList<EverendOutcome> Outcomes { get; internal set; }
    }

    public sealed class EverendOutcome
    {
        public string Id { get; internal set; }
        public string Name { get; internal set; }
        public string VisibleText { get; internal set; }
        public IReadOnlyDictionary<string, object> Availability { get; internal set; }
        public IReadOnlyList<IReadOnlyDictionary<string, object>> Consequences { get; internal set; }
        public string TargetNodeId { get; internal set; }
    }

    public sealed class EverendTransition
    {
        public string Id { get; internal set; }
        public string From { get; internal set; }
        public string To { get; internal set; }
        public string Label { get; internal set; }
        public int Order { get; internal set; }
        public string Mode { get; internal set; }
        public IReadOnlyDictionary<string, object> Conditions { get; internal set; }
        public IReadOnlyList<IReadOnlyDictionary<string, object>> Consequences { get; internal set; }
    }

    public sealed class EverendBeat
    {
        public string Id { get; internal set; }
        public string Kind { get; internal set; }
        public string ScriptId { get; internal set; }
        public string BlockId { get; internal set; }
        public IReadOnlyDictionary<string, object> DisplayCondition { get; internal set; }
    }

    public sealed class EverendScriptDocument
    {
        public string Id { get; internal set; }
        public string Name { get; internal set; }
        public string Format { get; internal set; }
        public IReadOnlyList<EverendScriptBlock> Blocks { get; internal set; }
    }

    public sealed class EverendScriptBlock
    {
        public string Id { get; internal set; }
        public string Kind { get; internal set; }
        public string Content { get; internal set; }
        public string SpeakerRef { get; internal set; }
        public string CharacterRef { get; internal set; }
        public string TextKey { get; internal set; }
    }

    internal static class EverendCollection
    {
        public static readonly IReadOnlyList<string> EmptyStrings = Array.Empty<string>();
        public static readonly IReadOnlyList<EverendSequence> EmptySequences = Array.Empty<EverendSequence>();
        public static readonly IReadOnlyList<EverendBranch> EmptyBranches = Array.Empty<EverendBranch>();
        public static readonly IReadOnlyList<EverendEvent> EmptyEvents = Array.Empty<EverendEvent>();
        public static readonly IReadOnlyList<EverendDecision> EmptyDecisions = Array.Empty<EverendDecision>();
        public static readonly IReadOnlyList<EverendOutcome> EmptyOutcomes = Array.Empty<EverendOutcome>();
        public static readonly IReadOnlyList<EverendTransition> EmptyTransitions = Array.Empty<EverendTransition>();
        public static readonly IReadOnlyList<EverendBeat> EmptyBeats = Array.Empty<EverendBeat>();
        public static readonly IReadOnlyList<EverendScriptDocument> EmptyDocuments = Array.Empty<EverendScriptDocument>();
        public static readonly IReadOnlyList<EverendScriptBlock> EmptyBlocks = Array.Empty<EverendScriptBlock>();
        public static readonly IReadOnlyList<IReadOnlyDictionary<string, object>> EmptyConsequences = Array.Empty<IReadOnlyDictionary<string, object>>();
        public static readonly IReadOnlyDictionary<string, object> EmptyObject = new Dictionary<string, object>();
        public static readonly IReadOnlyDictionary<string, string> EmptyStringsByKey = new Dictionary<string, string>();
    }
}
