using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace EverendForge.Unity
{
    public struct EverendRuntimePackageEnvelope
    {
        public string PackageId;
        public string SpecificationVersion;
    }

    /// <summary>Parses only the portable RuntimePackage contract; Unity-owned data never enters this layer.</summary>
    public static class EverendRuntimePackageParser
    {
        public static EverendRuntimePackageEnvelope ReadEnvelope(string json)
        {
            var root = RequireObject(EverendJson.Parse(json), "RuntimePackage root");
            var packageId = String(root, "packageId");
            var spec = String(root, "specVersion");
            if (string.IsNullOrWhiteSpace(packageId)) throw new FormatException("RuntimePackage.packageId is required.");
            if (string.IsNullOrWhiteSpace(spec)) throw new FormatException("RuntimePackage.specVersion is required.");
            return new EverendRuntimePackageEnvelope { PackageId = packageId, SpecificationVersion = spec };
        }

        public static EverendRuntimePackage Parse(string json)
        {
            var root = RequireObject(EverendJson.Parse(json), "RuntimePackage root");
            var envelope = ReadEnvelope(json);
            var pathBranching = Object(root, "pathBranching");
            if (pathBranching == null) throw new FormatException("RuntimePackage.pathBranching is required by this importer.");

            return new EverendRuntimePackage
            {
                PackageId = envelope.PackageId,
                SpecificationVersion = envelope.SpecificationVersion,
                EntryNodeId = String(root, "entryNodeId"),
                PrimaryLocale = String(root, "primaryLocale"),
                Variables = Object(root, "variables") ?? EverendCollection.EmptyObject,
                Localizations = StringMap(Object(root, "localization") ?? Object(root, "localizations")),
                Story = new EverendStory
                {
                    ProjectId = String(pathBranching, "projectId"),
                    EntrySequenceId = String(pathBranching, "entrySequenceId"),
                    Sequences = Sequences(Array(pathBranching, "sequences")),
                    Branches = Branches(Array(pathBranching, "branches")),
                    Events = Events(Array(pathBranching, "events")),
                    ScriptDocuments = Documents(Array(pathBranching, "scriptDocuments")),
                    ExternalFunctions = ExternalFunctions(Array(pathBranching, "externalFunctions"))
                }
            };
        }

        private static IReadOnlyList<EverendSequence> Sequences(List<object> values)
        {
            if (values == null) return EverendCollection.EmptySequences;
            var result = new List<EverendSequence>();
            foreach (var value in values)
            {
                var item = RequireObject(value, "sequence");
                result.Add(new EverendSequence
                {
                    Id = RequiredString(item, "id", "sequence"), Name = String(item, "name"),
                    CharacterRef = String(item, "characterRef"), EntryEventId = String(item, "entryEventId"),
                    EventIds = Strings(Array(item, "eventIds")), BranchIds = Strings(Array(item, "branchIds"))
                });
            }
            return result;
        }

        private static IReadOnlyList<EverendBranch> Branches(List<object> values)
        {
            if (values == null) return EverendCollection.EmptyBranches;
            var result = new List<EverendBranch>();
            foreach (var value in values)
            {
                var item = RequireObject(value, "branch");
                result.Add(new EverendBranch
                {
                    Id = RequiredString(item, "id", "branch"), Title = String(item, "title"),
                    Description = String(item, "description"), SequenceId = String(item, "sequenceId"),
                    EventIds = Strings(Array(item, "eventIds"))
                });
            }
            return result;
        }

        private static IReadOnlyList<EverendEvent> Events(List<object> values)
        {
            if (values == null) return EverendCollection.EmptyEvents;
            var result = new List<EverendEvent>();
            foreach (var value in values)
            {
                var item = RequireObject(value, "event");
                var text = Object(item, "text");
                result.Add(new EverendEvent
                {
                    Id = RequiredString(item, "id", "event"), Name = String(item, "name"), Type = String(item, "type"),
                    SequenceId = String(item, "sequenceId"), BranchId = FirstString(item, "branchId", "branchRef"), Raw = item,
                    Text = text == null ? null : new EverendText { Format = String(text, "format"), Content = String(text, "content") },
                    Decisions = Decisions(Array(item, "decisions")), Transitions = Transitions(Array(item, "transitions")),
                    DialogueBeats = Beats(Array(item, "dialogueBeats"))
                });
            }
            return result;
        }

        private static IReadOnlyList<EverendDecision> Decisions(List<object> values)
        {
            if (values == null) return EverendCollection.EmptyDecisions;
            var result = new List<EverendDecision>();
            foreach (var value in values)
            {
                var item = RequireObject(value, "decision");
                var outcomes = new List<EverendOutcome>();
                foreach (var outcomeValue in Array(item, "outcomes") ?? new List<object>())
                {
                    var outcome = RequireObject(outcomeValue, "decision outcome");
                    outcomes.Add(new EverendOutcome
                    {
                        Id = RequiredString(outcome, "id", "decision outcome"), Name = String(outcome, "name"),
                        VisibleText = String(outcome, "visibleText"), TargetNodeId = String(outcome, "targetNodeId"),
                        Availability = Condition(outcome, "availability", "conditions") ?? EverendCollection.EmptyObject,
                        Consequences = ObjectList(Array(outcome, "consequences"))
                    });
                }
                result.Add(new EverendDecision { Id = RequiredString(item, "id", "decision"), Name = String(item, "name"), Description = String(item, "description"), Type = String(item, "type"), Outcomes = outcomes });
            }
            return result;
        }

        private static IReadOnlyList<EverendTransition> Transitions(List<object> values)
        {
            if (values == null) return EverendCollection.EmptyTransitions;
            var result = new List<EverendTransition>();
            foreach (var value in values)
            {
                var item = RequireObject(value, "transition");
                result.Add(new EverendTransition
                {
                    Id = RequiredString(item, "id", "transition"), From = String(item, "from"), To = String(item, "to"),
                    Label = String(item, "label"), Mode = String(item, "mode"), Order = Int(item, "order"),
                    Conditions = Condition(item, "conditions", "condition") ?? EverendCollection.EmptyObject,
                    Consequences = ObjectList(Array(item, "consequences"))
                });
            }
            return result;
        }

        private static IReadOnlyList<EverendBeat> Beats(List<object> values)
        {
            if (values == null) return EverendCollection.EmptyBeats;
            var result = new List<EverendBeat>();
            foreach (var value in values)
            {
                var item = RequireObject(value, "dialogue beat");
                var reference = Object(item, "blockRef");
                result.Add(new EverendBeat
                {
                    Id = RequiredString(item, "id", "dialogue beat"), Kind = String(item, "kind"),
                    ScriptId = String(reference, "scriptId"), BlockId = String(reference, "blockId"),
                    DisplayCondition = Condition(item, "displayCondition") ?? EverendCollection.EmptyObject
                });
            }
            return result;
        }

        private static IReadOnlyList<EverendScriptDocument> Documents(List<object> values)
        {
            if (values == null) return EverendCollection.EmptyDocuments;
            var result = new List<EverendScriptDocument>();
            foreach (var value in values)
            {
                var item = RequireObject(value, "script document");
                var blocks = new List<EverendScriptBlock>();
                foreach (var blockValue in Array(item, "blocks") ?? new List<object>())
                {
                    var block = RequireObject(blockValue, "script block");
                    blocks.Add(new EverendScriptBlock
                    {
                        Id = RequiredString(block, "id", "script block"), Kind = String(block, "kind"), Content = String(block, "content"),
                        SpeakerRef = String(block, "speakerRef"), CharacterRef = String(block, "characterRef"), TextKey = String(block, "textKey")
                    });
                }
                result.Add(new EverendScriptDocument { Id = RequiredString(item, "id", "script document"), Name = String(item, "name"), Format = String(item, "format"), Blocks = blocks });
            }
            return result;
        }

        private static IReadOnlyList<IReadOnlyDictionary<string, object>> ObjectList(List<object> values)
        {
            if (values == null) return EverendCollection.EmptyConsequences;
            var result = new List<IReadOnlyDictionary<string, object>>();
            foreach (var value in values) result.Add(RequireObject(value, "object list item"));
            return result;
        }

        private static IReadOnlyList<string> Strings(List<object> values)
        {
            if (values == null) return EverendCollection.EmptyStrings;
            var result = new List<string>();
            foreach (var value in values) if (value != null) result.Add(Convert.ToString(value, CultureInfo.InvariantCulture));
            return result;
        }
        private static IReadOnlyList<string> ExternalFunctions(List<object> values)
        {
            if (values == null) return EverendCollection.EmptyStrings;
            var result = new List<string>();
            foreach (var value in values)
            {
                var map = value as Dictionary<string, object>;
                var name = map == null ? Convert.ToString(value, CultureInfo.InvariantCulture) : String(map, "name");
                if (!string.IsNullOrWhiteSpace(name)) result.Add(name);
            }
            return result;
        }

        private static IReadOnlyDictionary<string, string> StringMap(Dictionary<string, object> source)
        {
            if (source == null) return EverendCollection.EmptyStringsByKey;
            var result = new Dictionary<string, string>();
            foreach (var pair in source) result[pair.Key] = pair.Value == null ? string.Empty : Convert.ToString(pair.Value, CultureInfo.InvariantCulture);
            return result;
        }

        private static Dictionary<string, object> RequireObject(object value, string name)
        {
            var result = value as Dictionary<string, object>;
            if (result == null) throw new FormatException(name + " must be an object.");
            return result;
        }
        private static Dictionary<string, object> Object(Dictionary<string, object> source, string key)
        {
            object value; return source != null && source.TryGetValue(key, out value) ? value as Dictionary<string, object> : null;
        }
        private static Dictionary<string, object> Condition(Dictionary<string, object> source, params string[] keys)
        {
            foreach (var key in keys)
            {
                object value;
                if (source != null && source.TryGetValue(key, out value))
                {
                    var map = value as Dictionary<string, object>;
                    if (map != null) return map;
                    var list = value as List<object>;
                    if (list != null) return new Dictionary<string, object> { { "all", list } };
                }
            }
            return null;
        }
        private static List<object> Array(Dictionary<string, object> source, string key)
        {
            object value; return source != null && source.TryGetValue(key, out value) ? value as List<object> : null;
        }
        private static string String(Dictionary<string, object> source, string key)
        {
            object value; return source != null && source.TryGetValue(key, out value) && value != null ? Convert.ToString(value, CultureInfo.InvariantCulture) : string.Empty;
        }
        private static string RequiredString(Dictionary<string, object> source, string key, string name)
        {
            var value = String(source, key); if (string.IsNullOrWhiteSpace(value)) throw new FormatException(name + "." + key + " is required."); return value;
        }
        private static string FirstString(Dictionary<string, object> source, params string[] keys)
        {
            foreach (var key in keys)
            {
                var value = String(source, key);
                if (!string.IsNullOrWhiteSpace(value)) return value;
            }
            return string.Empty;
        }
        private static int Int(Dictionary<string, object> source, string key)
        {
            object value; if (source == null || !source.TryGetValue(key, out value) || value == null) return 0;
            if (value is long) return (int)(long)value; if (value is double) return (int)(double)value;
            int parsed; return int.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out parsed) ? parsed : 0;
        }
    }

    // Small dependency-free JSON reader. It preserves unknown condition/consequence objects for profiles.
    internal static class EverendJson
    {
        public static object Parse(string json)
        {
            if (json == null) throw new ArgumentNullException(nameof(json));
            var parser = new Parser(json); var value = parser.ReadValue(); parser.SkipWhitespace();
            if (!parser.End) throw new FormatException("Unexpected JSON content after root value.");
            return value;
        }

        private sealed class Parser
        {
            private readonly string text; private int index;
            public Parser(string value) { text = value; }
            public bool End => index >= text.Length;
            public void SkipWhitespace() { while (!End && char.IsWhiteSpace(text[index])) index++; }
            public object ReadValue()
            {
                SkipWhitespace(); if (End) throw new FormatException("Unexpected end of JSON.");
                switch (text[index]) { case '{': return ReadObject(); case '[': return ReadArray(); case '"': return ReadString(); case 't': Expect("true"); return true; case 'f': Expect("false"); return false; case 'n': Expect("null"); return null; default: return ReadNumber(); }
            }
            private Dictionary<string, object> ReadObject()
            {
                var result = new Dictionary<string, object>(); index++; SkipWhitespace(); if (Try('}')) return result;
                while (true) { SkipWhitespace(); if (End || text[index] != '"') throw new FormatException("Expected object key."); var key = ReadString(); SkipWhitespace(); Require(':'); result[key] = ReadValue(); SkipWhitespace(); if (Try('}')) return result; Require(','); }
            }
            private List<object> ReadArray()
            {
                var result = new List<object>(); index++; SkipWhitespace(); if (Try(']')) return result;
                while (true) { result.Add(ReadValue()); SkipWhitespace(); if (Try(']')) return result; Require(','); }
            }
            private string ReadString()
            {
                Require('"'); var value = new StringBuilder();
                while (!End) { var c = text[index++]; if (c == '"') return value.ToString(); if (c != '\\') { value.Append(c); continue; } if (End) break; c = text[index++]; switch (c) { case '"': value.Append('"'); break; case '\\': value.Append('\\'); break; case '/': value.Append('/'); break; case 'b': value.Append('\b'); break; case 'f': value.Append('\f'); break; case 'n': value.Append('\n'); break; case 'r': value.Append('\r'); break; case 't': value.Append('\t'); break; case 'u': value.Append((char)Convert.ToInt32(ReadHex(4), 16)); break; default: throw new FormatException("Invalid JSON string escape."); } }
                throw new FormatException("Unterminated JSON string.");
            }
            private string ReadHex(int count) { if (index + count > text.Length) throw new FormatException("Invalid unicode escape."); var result = text.Substring(index, count); index += count; return result; }
            private object ReadNumber()
            {
                var start = index; if (Try('-')) { } while (!End && char.IsDigit(text[index])) index++; if (Try('.')) while (!End && char.IsDigit(text[index])) index++; if (!End && (text[index] == 'e' || text[index] == 'E')) { index++; if (!End && (text[index] == '+' || text[index] == '-')) index++; while (!End && char.IsDigit(text[index])) index++; }
                var token = text.Substring(start, index - start); if (token.Length == 0) throw new FormatException("Invalid JSON value."); long integral; return long.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out integral) ? (object)integral : double.Parse(token, CultureInfo.InvariantCulture);
            }
            private bool Try(char expected) { if (!End && text[index] == expected) { index++; return true; } return false; }
            private void Require(char expected) { SkipWhitespace(); if (!Try(expected)) throw new FormatException("Expected '" + expected + "'."); }
            private void Expect(string expected) { if (index + expected.Length > text.Length || text.Substring(index, expected.Length) != expected) throw new FormatException("Invalid JSON literal."); index += expected.Length; }
        }
    }
}
