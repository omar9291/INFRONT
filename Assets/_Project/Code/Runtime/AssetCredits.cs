using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace Infront
{
    /// <summary>Unveraenderliche Quellenangaben aus dem mitgelieferten Katalog.
    /// Keine Netzwerkanfrage: dieselben Angaben sind offline im Spiel verfuegbar.</summary>
    public static class AssetCredits
    {
        public const string ResourceName = "asset-credits";
        static IReadOnlyList<AssetCredit> _all;

        public static IReadOnlyList<AssetCredit> All
        {
            get
            {
                if (_all != null) return _all;
                var file = Resources.Load<TextAsset>(ResourceName);
                if (file == null)
                    throw new InvalidOperationException("Asset credits manifest is missing.");
                _all = Parse(file.text);
                return _all;
            }
        }

        public static IReadOnlyList<AssetCredit> Parse(string json)
        {
            var document = JsonUtility.FromJson<CreditDocument>(json);
            if (document == null || document.schemaVersion != 1 || document.entries == null)
                throw new FormatException("Unsupported or incomplete asset credits manifest.");
            var result = new List<AssetCredit>(document.entries.Length);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in document.entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.id) || !ids.Add(entry.id))
                    throw new FormatException("Asset credits require unique, nonempty IDs.");
                result.Add(new AssetCredit(entry));
            }
            return result.AsReadOnly();
        }

        [Serializable]
        internal sealed class CreditDocument
        {
            public int schemaVersion;
            public CreditData[] entries;
        }

        [Serializable]
        internal sealed class CreditData
        {
            public string id;
            public string name;
            public string author;
            public string license;
            public string sourceUrl;
            public string provenanceStatus;
            public string notes;
            public bool optional;
            public string[] paths;
            public EvidenceData[] evidence;
            public FingerprintData[] content;
        }

        [Serializable]
        internal sealed class EvidenceData
        {
            public string path;
            public string kind;
            public string sourceUrl;
            public string sha256;
        }

        [Serializable]
        internal sealed class FingerprintData
        {
            public string path;
            public string sha256;
        }
    }

    public sealed class AssetCredit
    {
        public string Id { get; }
        public string Name { get; }
        public string Author { get; }
        public string License { get; }
        public string SourceUrl { get; }
        public string ProvenanceStatus { get; }
        public string Notes { get; }
        public bool Optional { get; }
        public IReadOnlyList<string> Paths { get; }
        public IReadOnlyList<AssetCreditEvidence> Evidence { get; }
        public IReadOnlyList<AssetContentFingerprint> Content { get; }

        internal AssetCredit(AssetCredits.CreditData data)
        {
            Id = data.id;
            Name = data.name ?? "";
            Author = data.author ?? "";
            License = data.license ?? "";
            SourceUrl = data.sourceUrl ?? "";
            ProvenanceStatus = data.provenanceStatus ?? "";
            Notes = data.notes ?? "";
            Optional = data.optional;
            Paths = Array.AsReadOnly((string[])(data.paths ?? Array.Empty<string>()).Clone());
            var evidence = new List<AssetCreditEvidence>();
            foreach (var item in data.evidence ?? Array.Empty<AssetCredits.EvidenceData>())
                evidence.Add(new AssetCreditEvidence(item));
            Evidence = evidence.AsReadOnly();
            var content = new List<AssetContentFingerprint>();
            foreach (var item in data.content ?? Array.Empty<AssetCredits.FingerprintData>())
                content.Add(new AssetContentFingerprint(item));
            Content = content.AsReadOnly();
        }
    }

    public sealed class AssetCreditEvidence
    {
        public string Path { get; }
        public string Kind { get; }
        public string SourceUrl { get; }
        public string Sha256 { get; }

        internal AssetCreditEvidence(AssetCredits.EvidenceData data)
        {
            Path = data.path ?? "";
            Kind = data.kind ?? "";
            SourceUrl = data.sourceUrl ?? "";
            Sha256 = data.sha256 ?? "";
        }
    }

    public sealed class AssetContentFingerprint
    {
        public string Path { get; }
        public string Sha256 { get; }

        internal AssetContentFingerprint(AssetCredits.FingerprintData data)
        {
            Path = data.path ?? "";
            Sha256 = data.sha256 ?? "";
        }
    }
}
