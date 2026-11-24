using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EverendForge.Unity
{
    /// <summary>Indexed, normalized view of a RuntimePackage. It is intentionally interpreter-agnostic.</summary>
    public sealed class EverendNarrativeCatalog : ScriptableObject
    {
        [SerializeField] private EverendRuntimePackageAsset sourcePackage;
        [SerializeField] private string sourceHash;
        [SerializeField] private string catalogVersion = "0.1";

        private EverendRuntimePackage package;
        private Dictionary<string, EverendSequence> sequences;
        private Dictionary<string, EverendBranch> branches;
        private Dictionary<string, EverendEvent> events;
        private Dictionary<string, EverendScriptBlock> blocks;
        private Dictionary<string, List<EverendTransition>> outgoingTransitions;

        public EverendRuntimePackageAsset SourcePackage => sourcePackage;
        public string SourceHash => sourceHash;
        public string CatalogVersion => catalogVersion;
        public EverendRuntimePackage Package { get { EnsureBuilt(); return package; } }

        public void Rebuild(EverendRuntimePackageAsset source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            sourcePackage = source;
            sourceHash = source.SourceHash;
            Build();
        }

        public bool IsCurrent => sourcePackage != null && sourceHash == sourcePackage.SourceHash;

        internal bool TryGetSequence(string id, out EverendSequence value) { EnsureBuilt(); return sequences.TryGetValue(id, out value); }
        internal bool TryGetBranch(string id, out EverendBranch value) { EnsureBuilt(); return branches.TryGetValue(id, out value); }
        internal bool TryGetEvent(string id, out EverendEvent value) { EnsureBuilt(); return events.TryGetValue(id, out value); }
        internal bool TryGetBlock(string scriptId, string blockId, out EverendScriptBlock value) { EnsureBuilt(); return blocks.TryGetValue(BlockKey(scriptId, blockId), out value); }
        internal IReadOnlyList<EverendTransition> GetOutgoingTransitions(string nodeId)
        {
            EnsureBuilt(); List<EverendTransition> result; return outgoingTransitions.TryGetValue(nodeId, out result) ? result : EverendCollection.EmptyTransitions;
        }

        private void OnEnable() { package = null; }
        private void EnsureBuilt()
        {
            if (package != null) return;
            if (sourcePackage == null || string.IsNullOrWhiteSpace(sourcePackage.RawJson)) throw new InvalidOperationException("Narrative catalog has no RuntimePackage source.");
            Build();
        }
        private void Build()
        {
            package = EverendRuntimePackageParser.Parse(sourcePackage.RawJson);
            sequences = package.Story.Sequences.ToDictionary(x => x.Id, StringComparer.Ordinal);
            branches = package.Story.Branches.ToDictionary(x => x.Id, StringComparer.Ordinal);
            events = package.Story.Events.ToDictionary(x => x.Id, StringComparer.Ordinal);
            foreach (var sequence in package.Story.Sequences)
            {
                foreach (var eventId in sequence.EventIds)
                {
                    EverendEvent narrativeEvent;
                    if (events.TryGetValue(eventId, out narrativeEvent) && string.IsNullOrWhiteSpace(narrativeEvent.SequenceId)) narrativeEvent.SequenceId = sequence.Id;
                }
                foreach (var branchId in sequence.BranchIds)
                {
                    EverendBranch branch;
                    if (branches.TryGetValue(branchId, out branch)) branch.SequenceId = sequence.Id;
                }
            }
            foreach (var branch in package.Story.Branches)
                foreach (var eventId in branch.EventIds)
                {
                    EverendEvent narrativeEvent;
                    if (events.TryGetValue(eventId, out narrativeEvent) && string.IsNullOrWhiteSpace(narrativeEvent.BranchId)) narrativeEvent.BranchId = branch.Id;
                }
            blocks = new Dictionary<string, EverendScriptBlock>(StringComparer.Ordinal);
            foreach (var document in package.Story.ScriptDocuments)
                foreach (var block in document.Blocks) blocks[BlockKey(document.Id, block.Id)] = block;
            outgoingTransitions = new Dictionary<string, List<EverendTransition>>(StringComparer.Ordinal);
            foreach (var transition in package.Story.Events.SelectMany(x => x.Transitions))
            {
                if (string.IsNullOrWhiteSpace(transition.From)) continue;
                List<EverendTransition> list;
                if (!outgoingTransitions.TryGetValue(transition.From, out list)) { list = new List<EverendTransition>(); outgoingTransitions.Add(transition.From, list); }
                list.Add(transition);
            }
            foreach (var list in outgoingTransitions.Values) list.Sort((a, b) => a.Order.CompareTo(b.Order));
        }
        private static string BlockKey(string scriptId, string blockId) { return (scriptId ?? string.Empty) + "::" + (blockId ?? string.Empty); }
    }
}
