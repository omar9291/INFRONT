# Umbauplan — Echte Asset-Pakete statt nur Code

> **STATUS (2026-09-01): P1–P6 umgesetzt und getestet** (105 PlayMode + 3
> EditMode grün). P7 (Figuren) ist vorbereitet, aber auf den Nutzer blockiert
> (Mixamo-Login). Bericht: `BERICHT-ASSETS.md`.

Auftrag vom 2026-09-01: *„jetzt vielleicht so Packs benutzen anstatt nur Code"*

Entschieden mit dem Nutzer:
- Stil-Richtung: **realistisch**
- Umfang: Sounds, Boden-/Wandtexturen, Waffenmodelle, Figuren + Animationen
- Downloads: **erlaubt**, CC0 ohne Login

Damit wird die Entscheidung aus `ASSETS.md` → „Nacht 8" bewusst zurückgenommen.

---

## Ausgangslage (geprüft, nicht geschätzt)

Im ganzen Projekt liegt **keine einzige externe Datei**: kein Bild, kein
Modell, kein Ton. 25 Grundkörper aus `SceneBuilder.cs`, 20 im Code gebaute
Materialien, Sound vollständig synthetisch aus `ProceduralSfx.cs`.

`Assets/_Project/Art/` und `Assets/ThirdParty/` enthalten nur `.gitkeep`.

---

## Die tragende Idee: das Audio-Muster hochziehen

`AudioService.cs:100` macht bereits genau das Richtige:

```csharp
AudioClip clip = Resources.Load<AudioClip>(FileName(id));   // echte Datei?
if (clip == null) clip = ProceduralSfx.Build(id);           // sonst Platzhalter
```

**Dasselbe Muster kommt für Geometrie und Material.** Neue Datei
`AssetLibrary.cs`:

```
Modell unter Art/Models/<name> vorhanden?  -> echtes Mesh benutzen
sonst                                      -> bisherige Code-Geometrie
```

Folgen, die alle drei Hausregeln erfüllen:
- **Nichts wird gelöscht.** Der ganze prozedurale Code bleibt als Rückfallweg
  stehen und läuft weiter, wenn eine Datei fehlt.
- **Rückweg ohne Werkzeug:** Gefällt ein Modell nicht, löscht der Nutzer die
  Datei — der alte Stand ist sofort zurück.
- **Testbar in beiden Richtungen:** Tests können mit und ohne Assets laufen,
  die 98 vorhandenen Tests bleiben gültig.

---

## Was CC0-ohne-Login wirklich hergibt (geprüft am 2026-09-01)

| Bereich | Quelle | Befund |
|---|---|---|
| PBR-Texturen | ambientCG | ✅ voll da, Direktdownload bestätigt, ~3,7 MB je 1K-Zip |
| Himmel/Licht | Poly Haven | ✅ 700 Außen-HDRIs |
| Deko-Modelle | Poly Haven | ✅ Fässer, Kisten, Rohre, Lampen, Gasflaschen — passt fast 1:1 auf `BuildDecoration` |
| Waffen | Poly Haven | ⚠️ nur `service_pistol` + `bolt_action_rifle_7_62` (+ `stick_grenade`) |
| Figuren | Poly Haven | ❌ **null** — alle 521 Modelle durchsucht, kein Charakter |
| Waffensounds | OpenGameArt | ⚠️ „The Free Firearm Sound Library", CC0, 194 MB **als .7z** |

**Lizenz geprüft:** Poly Haven ist vollständig CC0 — kommerzielle Nutzung
erlaubt, Namensnennung nicht erforderlich. ambientCG ebenfalls CC0.
Für eine spätere Veröffentlichung unter „Driftlab" auf itch.io ist beides
sauber.

### Zwei bekannte Hindernisse

1. **Kein 7z-Entpacker auf diesem Mac** (`7z`, `7za`, `unar`, `py7zr` fehlen
   alle). Die Waffensound-Bibliothek liegt aber nur als `.7z` vor. Lösung:
   entweder `pip install py7zr` (klein, eigener Download) oder eine andere
   Quelle. Wird in P5 entschieden, nicht vorher.
2. **Figuren gehen ohne den Nutzer nicht.** Realistische *animierte* Figuren
   gibt es CC0-ohne-Login nicht. Der Weg ist **Mixamo** (gratis, kommerziell
   erlaubt) — der braucht einen Adobe-Login. Konten anlegen und Passwörter
   eingeben ist mir verboten. Der Nutzer lädt herunter, ich baue ein.

---

## Reihenfolge — und warum sie von der ursprünglichen abweicht

Angekündigt war Sound → Texturen → Waffen → Figuren. Geändert auf
**Welt → Waffen → Sound → Figuren**, aus zwei Gründen:

- **„Realistisch" ist eine Entscheidung über das Auge.** Der größte und
  sicherste Sprung liegt bei Texturen und Himmel, nicht beim Ton.
- **Halb tauschen macht es schlechter.** Eine fotorealistische Waffe vor einer
  einfarbigen Würfel-Map sieht *kaputter* aus als alles-Würfel. Realistische
  Welt mit noch eckigen Spielern ist dagegen ein verbreiteter, akzeptierter
  Look. Also von außen nach innen.
- Der Sound hat zusätzlich das 7z-Hindernis und rutscht deshalb hinter das,
  was ohne Hindernis läuft.

---

## P1 — Fundament: `AssetLibrary` + Import-Prüfung

Neue Dateien:
- `Runtime/AssetLibrary.cs` — der Nachschlage-Weg oben, plus Zähler, wie viele
  Assets echt und wie viele Rückfall sind (für die Tests und den Bericht).
- `Editor/AssetImporterTools.cs` — baut aus heruntergeladenen Texturen echte
  URP/Lit-Materialien und setzt die Importer-Schalter richtig.
- `Tests/AssetImportTests.cs` — die Antwort auf den alten „nicht prüfbar"-
  Einwand aus Nacht 8.

**Was der Test rechnerisch prüfen kann, ohne etwas zu sehen:**
- Importiert die Datei ohne Fehler, ist sie über `AssetDatabase` ladbar?
- Hat das Mesh Eckpunkte (> 0)?
- **Ist der Maßstab plausibel?** Das ist der klassische Import-Fehler und rein
  rechnerisch erkennbar. Poly Haven liefert die echten Maße per API mit
  (`service_pistol` = 301 mm lang) — der Test vergleicht die Bounds des
  importierten Meshes damit. Eine 100 m lange Pistole fällt sofort auf.
- Hat das Material eine Textur (nicht `null`) und ist die Normalmap wirklich
  als Normalmap markiert?
- Ist der Ton länger als 0 Sekunden?
- Läuft alles auch noch, wenn die Dateien **fehlen** (Rückfall auf Code)?

**Was auch danach ungeprüft bleibt** (Screenshots sind auf diesem Rechner
gesperrt): ob es schön aussieht, ob eine Waffe verdreht in der Hand hängt, ob
eine Kachelgröße daneben liegt. Das muss der Nutzer beurteilen.

### Wichtige technische Festlegung für Materialien

Poly Haven und ambientCG liefern getrennte Texturdateien (diff, nor_gl, rough,
metal, ao, arm). Unity ordnet die **nicht** von allein einem URP/Lit-Material
zu, und `rough` ist das Gegenteil von URPs `smoothness`.

Erste Stufe deshalb bewusst schlicht und robust:
**BaseMap + NormalMap + skalares Metallic/Smoothness.** Das sieht rund 90 %
so gut aus wie die volle Verkabelung und hat einen Bruchteil der Fehlerquellen.
Die volle ARM-Verkabelung erst, wenn die erste Stufe steht und gefällt.

Ebenfalls festgelegt: **1K-JPG statt 4K-EXR.** Eine einzige 4K-EXR-Rauheitskarte
wiegt 12,6 MB; 1K reicht für dieses Spiel vollständig und hält Ladezeit und
Speicher klein.

## P2 — Boden, Wände, Deko-Texturen (ambientCG)

Echte Beton-, Metall-, Fliesen- und Asphaltmaterialien auf Map und Deko statt
einfarbiger Flächen. Größter sichtbarer Sprung, geringstes Risiko. Kachelgröße
ist die einzige Fehlerquelle — und die ist eine Zahl zum Korrigieren.

## P3 — Himmel und Licht (Poly Haven HDRI)

Echter Himmel mit echter Beleuchtung statt der prozeduralen Skybox
(`ArenaSky.mat` bleibt als Rückfall liegen). Verändert die Stimmung der ganzen
Arena auf einen Schlag. Achtung: die Helligkeit muss zur bestehenden
Post-Processing-Einstellung passen, und der Menüpunkt **BILD → SCHLICHT**
(für die Streifen auf dem M1) muss weiter funktionieren.

## P4 — Deko-Modelle (Poly Haven)

Fässer, Munitions- und Militärkisten, Plastikkisten, Rohre, Industrielampen,
Gasflaschen, Zementsäcke, Lüftungsrohre ersetzen die Würfel-Deko in
`SceneBuilder.BuildDecoration`. Über `AssetLibrary`, also mit Rückfall.

## P5 — Waffen (Poly Haven, 2 von 4)

- Pistole → `service_pistol`
- Sniper → `bolt_action_rifle_7_62`
- Gewehr und MP bleiben vorerst Code — es gibt dort nichts Passendes.

Position und Drehung in der Hand kann ich **nicht sehen**. Deshalb: alle
Halte-Zahlen an **einer** klar benannten Stelle, damit eine Rückmeldung wie
„die Pistole hängt zu weit links" eine Ein-Zeilen-Korrektur ist.

## P6 — Sounds

„The Free Firearm Sound Library" (CC0). Der Haken ist schon eingebaut: Datei
mit dem richtigen Namen nach `Audio/Resources/` legen, `AudioService` nimmt sie
automatisch. Kein Code nötig. Vorher das 7z-Hindernis klären.

## P7 — Figuren + Animationen (braucht den Nutzer)

Blockiert auf den Mixamo-Download. Sobald die Dateien da sind: Humanoid-Rig
einrichten, Animator-Controller bauen, `CharacterVisual.cs` auf echte
Animationen umstellen — mit Rückfall auf die jetzige Würfelfigur.

---

## Absicherung vor dem Start

- Voller Backup von `Assets/_Project/Code` ins Scratchpad, wie beim HUD-Umbau.
- Nach jedem Paket: `SceneBuilder.Build` + **voller** Testlauf (nicht teilweise).
- Jedes Paket wird in `ASSETS.md` eingetragen (Name, Quelle, Lizenz,
  kommerziell ja/nein), **bevor** es benutzt wird.
- Download-Umfang insgesamt geschätzt rund 300 MB.
