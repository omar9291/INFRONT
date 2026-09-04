using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Infront.EditorTools
{
    /// <summary>
    /// Vorbereitung fuer gebackenes Licht.
    ///
    /// Warum ueberhaupt: die Halle wird bisher nur von direkten Lichtern plus
    /// einem gleichmaessigen Umgebungswert beleuchtet. Es gibt keinen
    /// indirekten Anteil - kein Licht, das von einer Wand auf die naechste
    /// faellt, keine weichen Uebergaenge, keine Verdunkelung in Ecken ausser
    /// dem Bildschirmeffekt SSAO. Deshalb wirkt die Karte trotz Texturen wie
    /// eine gut beleuchtete Graukiste.
    ///
    /// Zwei Dinge muessen dafuer stimmen, und beide fehlten:
    ///
    /// 1. NICHTS war als statisch markiert. Ohne das Kennzeichen
    ///    "ContributeGI" nimmt der Lichtbacker ein Objekt gar nicht wahr - ein
    ///    Backvorgang haette schlicht ein leeres Ergebnis geliefert.
    ///
    /// 2. Die Wuerfel von <c>GameObject.CreatePrimitive</c> haben KEINE
    ///    zweite UV-Ebene. Ihre erste legt alle sechs Seiten uebereinander auf
    ///    dieselbe Flaeche. Fehlt die zweite, nimmt der Backer die erste - und
    ///    dann teilen sich Vorder- und Rueckseite einer Wand dieselben
    ///    Lichtwerte. Licht scheint durch Waende. Deshalb bekommt jeder
    ///    Primitivtyp hier EINE geteilte Kopie mit ausgepackter zweiter Ebene.
    /// </summary>
    public static class Backlicht
    {
        const string MeshDir = "Assets/_Project/Art/Meshes";

        static readonly Dictionary<string, Mesh> _ausgepackt = new Dictionary<string, Mesh>();

        /// <summary>
        /// Macht die gebaute Karte backfaehig: geteilte Meshes mit zweiter
        /// UV-Ebene, Static-Kennzeichen auf alles, was sich nicht bewegt.
        /// Wird am Ende des Szenenbaus gerufen, noch vor dem Speichern.
        /// </summary>
        public static void MacheKarteBackfaehig(Transform kartenWurzel)
        {
            if (kartenWurzel == null) return;

            _ausgepackt.Clear();
            Directory.CreateDirectory(MeshDir);

            int meshes = 0, markiert = 0;

            foreach (var mf in kartenWurzel.GetComponentsInChildren<MeshFilter>(true))
            {
                var m = mf.sharedMesh;
                if (m == null) continue;

                // Nur die eingebauten Primitive ersetzen. Importierte Modelle
                // bringen ihre eigene zweite Ebene mit (oder eben nicht - das
                // regelt der Modell-Import, nicht diese Stelle).
                if (IstPrimitiv(m.name))
                {
                    var ersatz = Ausgepackt(m);
                    if (ersatz != null && ersatz != m) { mf.sharedMesh = ersatz; meshes++; }
                }
            }

            foreach (var r in kartenWurzel.GetComponentsInChildren<Renderer>(true))
            {
                var go = r.gameObject;
                GameObjectUtility.SetStaticEditorFlags(go,
                    StaticEditorFlags.ContributeGI
                    | StaticEditorFlags.BatchingStatic
                    | StaticEditorFlags.OccluderStatic
                    | StaticEditorFlags.OccludeeStatic
                    | StaticEditorFlags.ReflectionProbeStatic);

                // Grosse Flaechen brauchen weniger Lichtkarten-Aufloesung als
                // kleine Deckung, sonst frisst eine Wand den halben Atlas.
                // Der Wert haengt nicht am Renderer selbst, sondern nur an der
                // serialisierten Eigenschaft - deshalb der Umweg.
                var b = r.bounds.size;
                float flaeche = Mathf.Max(b.x * b.y, Mathf.Max(b.x * b.z, b.y * b.z));
                float massstab = flaeche > 200f ? 0.35f : flaeche > 40f ? 0.6f : 1f;

                var so = new SerializedObject(r);
                var sp = so.FindProperty("m_ScaleInLightmap");
                if (sp != null) { sp.floatValue = massstab; so.ApplyModifiedPropertiesWithoutUndo(); }

                markiert++;
            }

            Debug.Log($"[Backlicht] {markiert} Renderer statisch markiert, "
                      + $"{meshes} Primitive auf ausgepackte Meshes umgestellt.");
        }

        /// <summary>
        /// Backt das indirekte Licht der Arena.
        ///
        /// Bewusst NUR das indirekte: die Lichter bleiben auf Mixed mit
        /// Modus "IndirectOnly". Direktes Licht und die Schatten der Figuren
        /// laufen also weiter in Echtzeit und sehen aus wie bisher - dazu
        /// kommt der Anteil, der vorher komplett fehlte: Licht, das von Boden
        /// und Waenden zurueckgeworfen wird. Das ist der risikoaermste Weg,
        /// weil nichts Bestehendes ersetzt wird, sondern etwas hinzukommt.
        ///
        /// <paramref name="aufloesung"/> ist die Zahl der Lichtkarten-Punkte
        /// je Meter. Klein anfangen: die Karte ist 90 x 90 m mit 869
        /// Flaechen, da wird aus einer scheinbar harmlosen Zahl schnell eine
        /// halbe Stunde Rechenzeit.
        /// </summary>
        public static void Backe(float aufloesung, int strahlen, float indirektStaerke)
        {
            var szene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                "Assets/_Project/Scenes/Arena.unity",
                UnityEditor.SceneManagement.OpenSceneMode.Single);

            int gemischt = 0;
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                l.lightmapBakeType = LightmapBakeType.Mixed;
                gemischt++;
            }

            var e = new LightingSettings
            {
                lightmapper            = LightingSettings.Lightmapper.ProgressiveCPU,
                mixedBakeMode          = MixedLightingMode.IndirectOnly,
                lightmapResolution     = aufloesung,
                lightmapPadding        = 2,
                lightmapMaxSize        = 1024,
                directSampleCount      = 16,
                indirectSampleCount    = strahlen,
                maxBounces             = 2,
                // Der indirekte Anteil ersetzt das flache Umgebungslicht, das
                // vorher ueberall gleich viel aufgehellt hat. Physikalisch
                // richtig ist das dunkler - spielbar ist es erst mit einem
                // Aufschlag. Der erste Backvorgang mit 1,0 hat den Median von
                // 85 auf 58 gedrueckt und die unlesbare Flaeche auf 43 %
                // gebracht; das Bild war schoener und unbenutzbar zugleich.
                indirectScale          = indirektStaerke,
                ao                     = true,
                aoMaxDistance          = 1.5f,
                aoExponentIndirect     = 1f,
                aoExponentDirect       = 0f,
                lightmapCompression    = LightmapCompression.NormalQuality,
                filteringMode          = LightingSettings.FilterMode.Auto,
                autoGenerate           = false,
            };
            Lightmapping.lightingSettings = e;

            Debug.Log($"[Backlicht] Start: {gemischt} Lichter auf Mixed/IndirectOnly, "
                      + $"{aufloesung} Punkte je Meter, {strahlen} Strahlen, indirekt x{indirektStaerke}.");

            var uhr = System.Diagnostics.Stopwatch.StartNew();
            bool ok = Lightmapping.Bake();
            uhr.Stop();

            var karten = LightmapSettings.lightmaps;
            Debug.Log($"[Backlicht] BACK_ERGEBNIS ok={ok} dauer={uhr.Elapsed.TotalMinutes:0.0}min "
                      + $"lichtkarten={(karten != null ? karten.Length : 0)}");

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(szene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(szene);
            AssetDatabase.SaveAssets();
        }

        /// <summary>Probelauf: sehr grob, nur um zu sehen, ob der Weg ueberhaupt
        /// funktioniert und wie lange er dauert.</summary>
        public static void BackeProbe() => Backe(1.5f, 64, 2.5f);

        /// <summary>Der richtige Durchlauf.</summary>
        public static void BackeFein() => Backe(4f, 256, 2.5f);

        static bool IstPrimitiv(string name)
            => name == "Cube" || name == "Cylinder" || name == "Capsule"
               || name == "Plane" || name == "Sphere";

        /// <summary>Eine geteilte Kopie des Primitivs mit zweiter UV-Ebene.
        /// Wird als Asset abgelegt, sonst landet fuer jede Wand eine eigene
        /// Kopie in der Szenendatei.</summary>
        static Mesh Ausgepackt(Mesh vorlage)
        {
            if (_ausgepackt.TryGetValue(vorlage.name, out var da)) return da;

            string pfad = $"{MeshDir}/{vorlage.name}_LM.asset";
            var vorhanden = AssetDatabase.LoadAssetAtPath<Mesh>(pfad);
            if (vorhanden != null)
            {
                _ausgepackt[vorlage.name] = vorhanden;
                return vorhanden;
            }

            var kopie = Object.Instantiate(vorlage);
            kopie.name = vorlage.name + "_LM";
            Unwrapping.GenerateSecondaryUVSet(kopie);
            AssetDatabase.CreateAsset(kopie, pfad);
            AssetDatabase.SaveAssets();

            _ausgepackt[vorlage.name] = kopie;
            int punkte = kopie.uv2 != null ? kopie.uv2.Length : 0;
            Debug.Log($"[Backlicht] {kopie.name}: zweite UV-Ebene erzeugt ({punkte} Punkte).");
            return kopie;
        }
    }
}
