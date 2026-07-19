using NUnit.Framework;
using UnityEngine;

namespace EverendForge.Unity.Tests
{
    public sealed class NarrativeGatewayTests
    {
        private static readonly string Fixture = "{\"specVersion\":\"0.1\",\"packageId\":\"ridina-mvp\",\"primaryLocale\":\"es\",\"variables\":{\"trust\":0},\"pathBranching\":{\"projectId\":\"sinpo\",\"entrySequenceId\":\"seq-ridina\",\"externalFunctions\":[\"SetNextEvent\"],\"sequences\":[{\"id\":\"seq-ridina\",\"name\":\"Ridina\",\"characterRef\":\"ridina\",\"entryEventId\":\"evt-start\",\"eventIds\":[\"evt-start\",\"evt-end\"],\"branchIds\":[\"branch-mvp\"]}],\"branches\":[{\"id\":\"branch-mvp\",\"title\":\"MVP\",\"description\":\"\",\"sequenceId\":\"seq-ridina\",\"eventIds\":[\"evt-start\",\"evt-end\"]}],\"scriptDocuments\":[{\"id\":\"script-1\",\"blocks\":[{\"id\":\"line-1\",\"kind\":\"dialogue\",\"content\":\"Hola\",\"speakerRef\":\"ridina\"}]}],\"events\":[{\"id\":\"evt-start\",\"name\":\"Inicio\",\"type\":\"normal\",\"sequenceId\":\"seq-ridina\",\"branchId\":\"branch-mvp\",\"dialogueBeats\":[{\"id\":\"beat-1\",\"blockRef\":{\"scriptId\":\"script-1\",\"blockId\":\"line-1\"}}],\"decisions\":[{\"id\":\"decision-1\",\"outcomes\":[{\"id\":\"yes\",\"visibleText\":\"Continuar\",\"consequences\":[{\"type\":\"setVariable\",\"variable\":\"trust\",\"value\":1}],\"targetNodeId\":\"evt-end\"}]}],\"transitions\":[{\"id\":\"go-end\",\"from\":\"evt-start\",\"to\":\"evt-end\",\"order\":0}]},{\"id\":\"evt-end\",\"name\":\"Fin\",\"type\":\"final\",\"sequenceId\":\"seq-ridina\",\"branchId\":\"branch-mvp\",\"decisions\":[],\"transitions\":[]}]}}";

        [Test]
        public void Gateway_IndexesEveryPortableObject()
        {
            var package = ScriptableObject.CreateInstance<EverendRuntimePackageAsset>(); package.Replace(Fixture, "test");
            var catalog = ScriptableObject.CreateInstance<EverendNarrativeCatalog>(); catalog.Rebuild(package);
            var gateway = new EverendNarrativeGateway(catalog);
            Assert.That(gateway.TryGetSequence("seq-ridina", out _), Is.True);
            Assert.That(gateway.TryGetBranch("branch-mvp", out _), Is.True);
            Assert.That(gateway.TryGetEvent("evt-start", out var narrativeEvent), Is.True);
            Assert.That(gateway.GetOutgoingTransitions(narrativeEvent.Id), Has.Count.EqualTo(1));
            Object.DestroyImmediate(catalog); Object.DestroyImmediate(package);
        }

        [Test]
        public void SinpoInk_GeneratesChoiceVariableTransitionAndFinal()
        {
            var package = ScriptableObject.CreateInstance<EverendRuntimePackageAsset>(); package.Replace(Fixture, "test");
            var catalog = ScriptableObject.CreateInstance<EverendNarrativeCatalog>(); catalog.Rebuild(package);
            var gateway = new EverendNarrativeGateway(catalog); gateway.TryGetEvent("evt-start", out var start); gateway.TryGetEvent("evt-end", out var end);
            var generator = new EverendForge.Unity.Editor.EverendSinpoInkGenerator(catalog, id => id == "evt-end" ? "ev_end" : "ev_start");
            var generated = generator.GenerateEvent(start); var final = generator.GenerateEvent(end);
            Assert.That(generator.Errors, Is.Empty);
            Assert.That(generated, Does.Contain("* [Continuar]").And.Contain("~ trust = 1").And.Contain("SetNextEvent(\"ev_end\")"));
            Assert.That(final, Does.Contain("-> END"));
            Object.DestroyImmediate(catalog); Object.DestroyImmediate(package);
        }

        [Test]
        public void Analyzer_RejectsInvalidPackage()
        {
            var analysis = EverendForge.Unity.Editor.EverendImportAnalyzer.Analyze("{\"packageId\":\"missing-spec\"}");
            Assert.That(analysis.IsValid, Is.False);
        }
    }
}
