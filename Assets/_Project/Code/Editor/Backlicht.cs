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

                // Flache Schmutz-Quads liegen 2 cm ueber dem Boden. Drei Dinge
                // liessen sie als hartkantiges Rechteck stehen ("treppenfoermiger
                // Bodenfleck"):
                //  - ContributeGI: der Backer buk sie als eigene Flaeche mit
                //    einer AO-Naht ringsum.
                //  - Schattenempfang: die Schattenkarte rasterte die Kante als
                //    gestrichelte Linie.
                //  - Schattenwurf: der 2-cm-Spalt warf einen feinen Rahmen auf
                //    den Boden.
                // Als reine Deko ohne GI und ohne Schatten folgen sie jetzt der
                // Beleuchtung des Bodens darunter.
                if (go.name.StartsWith("Fleck"))
                {
                    GameObjectUtility.SetStaticEditorFlags(go,
                        StaticEditorFlags.BatchingStatic
                        | StaticEditorFlags.OccludeeStatic);
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    r.receiveShadows = false;
                    // m_ReceiveGI: 1 = Lichtkarten, 2 = Lichtsonden. Ohne
                    // ContributeGI wuerde der Backer sonst trotzdem eine
                    // (leere) Lichtkarten-Kachel anlegen.
                    var soF = new SerializedObject(r);
                    var spF = soF.FindProperty("m_ReceiveGI");
                    if (spF != null)
                    {
                        spF.intValue = 2;
                        soF.ApplyModifiedPropertiesWithoutUndo();
                    }
                    continue;
                }

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
                // Der Boden ist die Flaeche, die der Spieler am laengsten
                // ansieht, und er ist als Raster aus 20-m-Platten gebaut. Nach
                // der reinen Flaechenregel landet jede davon bei 0,35 - das
                // waeren 1,4 Texel je Meter, also grosse weiche Flecken statt
                // Licht. Volle Aufloesung kostet fuer alle 25 Platten zusammen
                // rund 160 000 Texel und passt locker in einen Atlas.
                float massstab = go.name.StartsWith("Boden_") ? 1f
                                 : flaeche > 200f ? 0.35f : flaeche > 40f ? 0.6f : 1f;

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
        /// ACHTUNG, gescheiterter Versuch am 2026-09-10 - nicht blind
        /// wiederholen. Umstellung auf <c>MixedLightingMode.Shadowmask</c>
        /// (dazu QualitySettings shadowmaskMode 1 -> 0 auf den Stufen High,
        /// Very High, Ultra) sollte Kontaktschatten bringen. Ergebnis: die
        /// Halle leuchtete selbst, mit Bloom-Halo bis in den Himmel.
        /// Gemessen ueber alle 28 Rundgang-Bilder:
        ///
        ///   Backvorgang 1: Median 98 -> 219, ausgebrannt 0,9 % -> 21,7 %
        ///   Backvorgang 2: Median 98 -> 252, ausgebrannt 0,9 % -> 64,9 %
        ///
        /// Der zweite war HELLER als der erste, obwohl die einzige Aenderung
        /// mehr Verdeckung war - es wird also von Backvorgang zu Backvorgang
        /// schlimmer statt stabil. Das riecht nach Rueckkopplung, nicht nach
        /// einem falschen Zahlenwert. Passend dazu: die neun
        /// Arena-Reflexionssonden wurden dabei alle byte-gleich und schrumpften
        /// von je ~1,2 MB auf 550 kB (Lauf 1) und dann auf 6 kB (Lauf 2).
        /// Das Menue - eigene Szene, nicht neu gebacken - blieb unveraendert
        /// bei Median 5. Der Fehler sitzt also im Arena-Backvorgang.
        ///
        /// Wer das nochmal angeht: zuerst klaeren, wie <c>indirectScale</c> = 7
        /// mit Shadowmask zusammenwirkt (die 7 ist eine Kruecke, die nur fuer
        /// IndirectOnly eingemessen wurde), und die Reflexionssonden im Auge
        /// behalten. Ein Versuch mit indirectScale = 1 waere der naechste
        /// Schritt gewesen.
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
                // Drei statt zwei Sprüngen. In einem offenen Hof reichen
                // zwei - da geht der Rest ohnehin in den Himmel. Eine
                // geschlossene Halle ist ein Kasten: dort lebt das Licht in
                // den Ecken erst vom dritten Sprung.
                maxBounces             = 3,
                // Der indirekte Anteil ersetzt das flache Umgebungslicht, das
                // vorher ueberall gleich viel aufgehellt hat. Physikalisch
                // richtig ist das dunkler - spielbar ist es erst mit einem
                // Aufschlag. Der erste Backvorgang mit 1,0 hat den Median von
                // 85 auf 58 gedrueckt und die unlesbare Flaeche auf 43 %
                // gebracht; das Bild war schoener und unbenutzbar zugleich.
                indirectScale          = indirektStaerke,
                ao                     = true,
                // BLEIBT bei 1,5. Am 2026-09-10 auf 0,8 verkuerzt, weil das
                // fuer Kontaktschatten enger und richtiger klang. Ergebnis
                // gemessen: Median 98 -> 219, ausgebrannte Flaeche 0,9 % ->
                // 21,7 %, die Halle war weiss. Grund: indirectScale steht auf
                // 7, und die Verdeckung ist das Einzige, was diesen
                // siebenfachen Streulicht-Anteil in Schach haelt. Wird ihre
                // Reichweite halbiert, faellt die Bremse weg - mal sieben.
                aoMaxDistance          = 1.5f,
                aoExponentIndirect     = 1f,
                // Bleibt 0, solange oben IndirectOnly steht: bei IndirectOnly
                // gibt es im Lichtkarten-Backvorgang gar keinen direkten
                // Anteil, den dieser Wert verdunkeln koennte. Er ist der
                // richtige Hebel fuer Kontaktschatten - aber nur zusammen mit
                // einem Backmodus, der direktes Licht kennt.
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
        public static void BackeProbe() => Backe(1.5f, 64, 7f);

        /// <summary>Der richtige Durchlauf.</summary>
        // Indirekt-Staerke von 5,5 auf 7: die Lichtbaender im Dach leuchten
        // seit der Verglasungs-Runde nur noch halb so stark (sie brannten
        // sonst flaechig auf 255 aus). Der Kegel-Anteil ist separat
        // nachgezogen worden; dieser Wert holt den ANDEREN Teil zurueck, den
        // die grosse leuchtende Decke getragen hat: die Aufhellung der
        // Schattenseiten. Gemessen war dort der Verlust am groessten
        // (schwarz 8,5 % auf 12,0 %, unlesbar 24,4 % auf 28,3 %).
        public static void BackeFein() => Backe(4f, 256, 7f);

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
