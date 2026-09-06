using System;
using System.Collections.Generic;
using UnityEngine;

namespace Infront
{
    /// <summary>Für jede Klangkennung: Dateien, Herkunft oder eine bewusste
    /// Begründung für Synthese. Fehlende Aufnahmen werden durch Tests erkannt.</summary>
    public static class AudioCatalog
    {
        [Serializable]
        public sealed class Entry
        {
            public string id;
            public string kind;
            public string reason;
            public string credit;
            public string[] clips;
            public float gain = 1f;
        }

        [Serializable]
        sealed class Document { public Entry[] entries; }
        static Dictionary<SoundId, Entry> _entries;

        public static Entry For(SoundId id)
        {
            if (_entries == null)
            {
                _entries = new Dictionary<SoundId, Entry>();
                var json = Resources.Load<TextAsset>("audio-catalog");
                if (json == null) throw new InvalidOperationException("Missing audio catalog.");
                foreach (var entry in JsonUtility.FromJson<Document>(json.text).entries)
                {
                    if (!Enum.TryParse(entry.id, out SoundId key) || _entries.ContainsKey(key))
                        throw new FormatException("Unknown or duplicate audio ID: " + entry.id);
                    _entries.Add(key, entry);
                }
            }
            return _entries.TryGetValue(id, out var result) ? result : null;
        }

        /// <summary>Varianten wechseln ohne sofortige Wiederholung.</summary>
        public static int NextVariant(int count, int previous)
        {
            if (count <= 1) return 0;
            if (previous < 0 || previous >= count) return UnityEngine.Random.Range(0, count);
            int next = UnityEngine.Random.Range(0, count - 1);
            return next >= previous ? next + 1 : next;
        }
    }
}
