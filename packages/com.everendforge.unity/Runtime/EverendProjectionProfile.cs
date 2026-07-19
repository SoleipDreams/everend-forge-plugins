using System;
using System.Collections.Generic;
using UnityEngine;

namespace EverendForge.Unity
{
    [CreateAssetMenu(menuName = "Everend Forge/Narrative Projection Profile", fileName = "EverendProjectionProfile")]
    public sealed class EverendProjectionProfile : ScriptableObject
    {
        [SerializeField] private string profileId = "sinpo-v0.1";
        [SerializeField] private string profileVersion = "0.1.0";
        [SerializeField] private string managedOutputRoot = "Assets/EverendForge/Generated";
        [SerializeField] private List<EverendCharacterMapping> characterMappings = new List<EverendCharacterMapping>();
        [SerializeField] private EverendSinpoSettings sinpo = new EverendSinpoSettings();

        public string ProfileId => profileId;
        public string ProfileVersion => profileVersion;
        public string ManagedOutputRoot => managedOutputRoot;
        public IReadOnlyList<EverendCharacterMapping> CharacterMappings => characterMappings;
        public EverendSinpoSettings Sinpo => sinpo;

        public bool TryGetCharacter(string everendCharacterRef, out EverendCharacterMapping mapping)
        {
            mapping = characterMappings.Find(x => x.EverendCharacterRef == everendCharacterRef);
            return mapping != null;
        }
    }

    [Serializable]
    public sealed class EverendCharacterMapping
    {
        public string EverendCharacterRef;
        public string UnityEnumValue;
        public string AddressablesLabel;
    }

    [Serializable]
    public sealed class EverendSinpoSettings
    {
        public bool Enabled = true;
        public string GlobalInkIncludePath = "Assets/GameData/Stories/Globals.ink";
        public string SequenceTypeName = "SequenceData";
        public string BranchTypeName = "BranchData";
        public string EventTypeName = "EventsData";
        [Header("Unity field mapping")]
        public string SequenceCharacterField = "characterRef";
        public string SequenceStartingEventField = "startingEvent";
        public string SequenceEventsField = "events";
        public string SequenceBranchesField = "branches";
        public string BranchIdField = "branchID";
        public string BranchTitleField = "title";
        public string BranchDescriptionField = "description";
        public string BranchEventsField = "events";
        public string EventIdField = "eventID";
        public string EventNameField = "EventName";
        public string EventBranchField = "BranchRef";
        public string EventTypeField = "EventType";
        public string EventInkJsonField = "InkJSON";
        public string EventDescriptionField = "Description";
        public string EventNextEventsField = "nextEvents";
        public List<string> AllowedExternalFunctions = new List<string> { "SetNextEvent" };
        public bool ConfigureAddressables = true;
    }
}
