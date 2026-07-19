using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace EverendForge.Unity
{
    /// <summary>Immutable source snapshot used by the catalog and all projections.</summary>
    public sealed class EverendRuntimePackageAsset : ScriptableObject
    {
        [SerializeField, TextArea(8, 40)] private string rawJson;
        [SerializeField] private string packageId;
        [SerializeField] private string specificationVersion;
        [SerializeField] private string sourceHash;
        [SerializeField] private string sourceDescription;
        [SerializeField] private string importedAtUtc;

        public string RawJson => rawJson;
        public string PackageId => packageId;
        public string SpecificationVersion => specificationVersion;
        public string SourceHash => sourceHash;
        public string SourceDescription => sourceDescription;
        public string ImportedAtUtc => importedAtUtc;

        public void Replace(string json, string source)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("RuntimePackage JSON cannot be empty.", nameof(json));

            var envelope = EverendRuntimePackageParser.ReadEnvelope(json);
            rawJson = json;
            packageId = envelope.PackageId;
            specificationVersion = envelope.SpecificationVersion;
            sourceDescription = source ?? string.Empty;
            sourceHash = ComputeSha256(json);
            importedAtUtc = DateTime.UtcNow.ToString("O");
        }

        private static string ComputeSha256(string value)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
                var builder = new StringBuilder(bytes.Length * 2);
                foreach (var b in bytes) builder.Append(b.ToString("x2"));
                return builder.ToString();
            }
        }
    }
}
