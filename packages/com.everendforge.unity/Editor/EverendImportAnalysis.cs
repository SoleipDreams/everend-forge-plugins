using System;
using System.Collections.Generic;
using System.Linq;

namespace EverendForge.Unity.Editor
{
    public sealed class EverendImportAnalysis
    {
        public EverendRuntimePackage Package { get; internal set; }
        public List<string> Errors { get; } = new List<string>();
        public List<string> Warnings { get; } = new List<string>();
        public bool IsValid => Errors.Count == 0;
        public string Summary => Package == null ? "No package loaded." : string.Format("{0}: {1} sequences, {2} branches, {3} events, {4} scripts.", Package.PackageId, Package.Story.Sequences.Count, Package.Story.Branches.Count, Package.Story.Events.Count, Package.Story.ScriptDocuments.Count);
    }

    public static class EverendImportAnalyzer
    {
        public static EverendImportAnalysis Analyze(string json)
        {
            var result = new EverendImportAnalysis();
            try { result.Package = EverendRuntimePackageParser.Parse(json); }
            catch (Exception exception) { result.Errors.Add(exception.Message); return result; }

            var story = result.Package.Story;
            ValidateUnique(story.Sequences.Select(x => x.Id), "sequence", result);
            ValidateUnique(story.Branches.Select(x => x.Id), "branch", result);
            ValidateUnique(story.Events.Select(x => x.Id), "event", result);
            foreach (var sequence in story.Sequences)
            {
                if (!string.IsNullOrWhiteSpace(sequence.EntryEventId) && !story.Events.Any(x => x.Id == sequence.EntryEventId)) result.Errors.Add("Sequence '" + sequence.Id + "' points to missing entry event '" + sequence.EntryEventId + "'.");
                foreach (var eventId in sequence.EventIds.Where(id => !story.Events.Any(x => x.Id == id))) result.Errors.Add("Sequence '" + sequence.Id + "' references missing event '" + eventId + "'.");
            }
            foreach (var transition in story.Events.SelectMany(x => x.Transitions))
            {
                if (string.IsNullOrWhiteSpace(transition.To)) result.Errors.Add("Transition '" + transition.Id + "' has no target.");
                else if (!story.Events.Any(x => x.Id == transition.To) && !story.Events.SelectMany(x => x.DialogueBeats).Any(x => x.Id == transition.To)) result.Warnings.Add("Transition '" + transition.Id + "' targets '" + transition.To + "', which is not an exported event or beat.");
                if (string.Equals(transition.Role, "flow", StringComparison.OrdinalIgnoreCase) && (transition.Conditions.Count > 0 || transition.Consequences.Count > 0 || string.Equals(transition.Mode, "fallback", StringComparison.OrdinalIgnoreCase))) result.Errors.Add("Flow transition '" + transition.Id + "' contains route logic. Export it as role 'route'.");
            }
            return result;
        }

        private static void ValidateUnique(IEnumerable<string> ids, string kind, EverendImportAnalysis result)
        {
            foreach (var id in ids.GroupBy(x => x).Where(group => group.Count() > 1)) result.Errors.Add("Duplicate " + kind + " id '" + id.Key + "'.");
        }
    }
}
