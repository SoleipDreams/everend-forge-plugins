using System;
using System.Collections.Generic;
using UnityEngine;

namespace EverendForge.Unity
{
    public sealed class EverendProjectionManifest : ScriptableObject
    {
        [SerializeField] private string profileId;
        [SerializeField] private string profileVersion;
        [SerializeField] private string sourcePackageHash;
        [SerializeField] private string projectId;
        [SerializeField] private string storyId;
        [SerializeField] private int appliedRevision;
        [SerializeField] private string generationMode = "generated";
        [SerializeField] private string lastAppliedAtUtc;
        [SerializeField] private List<EverendNativeArtifact> artifacts = new List<EverendNativeArtifact>();

        public string ProfileId => profileId;
        public string ProfileVersion => profileVersion;
        public string SourcePackageHash => sourcePackageHash;
        public string ProjectId => projectId;
        public string StoryId => storyId;
        public int AppliedRevision => appliedRevision;
        public string GenerationMode => generationMode;
        public string LastAppliedAtUtc => lastAppliedAtUtc;
        public IReadOnlyList<EverendNativeArtifact> Artifacts => artifacts;

        public void BeginUpdate(EverendProjectionProfile profile, EverendRuntimePackageAsset source)
        {
            profileId = profile.ProfileId; profileVersion = profile.ProfileVersion; sourcePackageHash = source.SourceHash;
        }
        public void UpdateSyncMetadata(string project, string story, int revision, string mode)
        {
            projectId = project; storyId = story; appliedRevision = revision; generationMode = string.IsNullOrWhiteSpace(mode) ? "generated" : mode; lastAppliedAtUtc = DateTime.UtcNow.ToString("O");
        }
        public bool TryGet(string everendId, out EverendNativeArtifact artifact)
        {
            artifact = artifacts.Find(x => x.EverendId == everendId); return artifact != null;
        }
        public EverendNativeArtifact GetOrCreate(string everendId, string kind)
        {
            EverendNativeArtifact result;
            if (TryGet(everendId, out result)) return result;
            result = new EverendNativeArtifact { EverendId = everendId, Kind = kind };
            artifacts.Add(result); return result;
        }
    }

    [Serializable]
    public sealed class EverendNativeArtifact
    {
        public string EverendId;
        public string Kind;
        public string NativeId;
        public string AssetPath;
        public string ContentHash;
        public UnityEngine.Object NativeAsset;
    }
}
