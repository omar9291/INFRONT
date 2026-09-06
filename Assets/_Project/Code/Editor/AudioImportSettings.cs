using UnityEditor;
using UnityEngine;

namespace Infront.EditorTools
{
    /// <summary>Kurze Effekte sind bereit im Speicher, lange Musik wird gestreamt.</summary>
    public sealed class AudioImportSettings : AssetPostprocessor
    {
        // Änderungen am Import brauchen eine neue Version, sonst behält Unity
        // bereits importierte Clips mit den alten Einstellungen im Cache.
        public override uint GetVersion() => 2;

        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith("Assets/ThirdParty/Audio/")) return;
            var importer = (AudioImporter)assetImporter;
            bool music = System.IO.Path.GetFileName(assetPath).StartsWith("musik_");
            var settings = importer.defaultSampleSettings;
            settings.loadType = music ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            // ADPCM überhöht die scharfe Mechanik-Transientenspitze (.60 -> .884).
            // Die kurzen Mono-Dateien sind klein genug für unverändertes PCM.
            settings.compressionFormat = music ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.PCM;
            settings.quality = music ? .75f : 1f;
            settings.preloadAudioData = !music;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            importer.defaultSampleSettings = settings;
            // Die kurzen Quellen liegen bereits in Mono vor. Kein erneutes
            // Downmixing mit Unitys automatischer Pegelnormalisierung.
            importer.forceToMono = false;
            importer.loadInBackground = music;
        }
    }
}
