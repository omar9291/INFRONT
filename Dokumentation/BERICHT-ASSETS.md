# Bericht — Echte Asset-Pakete statt nur Code (2026-09-01)

Du hattest gesagt: *„jetzt vielleicht so Packs benutzen anstatt nur Code."*
Stil-Richtung: **realistisch**. Downloads: CC0 ohne Login erlaubt.

---

## Kurz gesagt

Vorher lag im ganzen Projekt **keine einzige externe Datei** — alles aus
Grundkörpern und Code. Jetzt sind echte CC0-Pakete drin:

- **Boden, Wände, Deckung, Plattformen** haben echte Beton-/Asphalt-/Metall-
  Texturen (ambientCG).
- **Der Himmel** ist ein echtes HDRI (Poly Haven) — Sonnenuntergang-Stimmung,
  das Licht der Arena kommt jetzt von dort.
- **Fässer, Kisten, Rohre, Lampen, Kanister, Zementsäcke** sind echte
  3D-Modelle statt Zylinder und Würfel (Poly Haven).
- **Pistole und Scharfschützengewehr** in der Hand sind echte Waffenmodelle
  (Poly Haven). Sturmgewehr und MP bleiben vorerst die Code-Waffe — CC0 gibt es
  dafür nichts Passendes.
- **Vier echte Schuss-Sounds** (Gewehr, MP, Sniper, Pistole) aus der „Free
  Firearm Sound Library" (CC0). Die restlichen Töne bleiben synthetisch.

**105 von 105 PlayMode-Tests grün, 3 von 3 EditMode-Tests grün.**

**Alles CC0** — kommerzielle Nutzung erlaubt, keine Namensnennung nötig. Sauber
für eine spätere Veröffentlichung unter „Driftlab" auf itch.io. Eingetragen in
`ASSETS.md`.

---

## Das Prinzip: nichts wird ersetzt, nur ergänzt

Es gibt jetzt eine Stelle namens `AssetLibrary`, die nach echten Dateien sucht —
genau wie `AudioService` schon immer nach echten Sounddateien sucht:

> Liegt eine Datei da → benutze sie. Fehlt sie → baue die Würfel wie bisher.

Der ganze alte Code, der Fässer, Figuren und Waffen aus Grundkörpern baut,
**steht weiter da** und ist die Rückfallebene. Gefällt dir ein Modell nicht:
Datei in `Assets/_Project/Art/` löschen, „Infront/Setup/2 – Arena und Spieler
bauen" neu laufen — der alte Stand ist zurück.

---

## Was ich NICHT prüfen kann

Screenshots sind auf diesem Rechner gesperrt. Ich kann **nicht sehen**, ob es
gut aussieht. Was die Tests rechnerisch prüfen konnten (und was grün ist):

- Importiert jede Datei fehlerfrei?
- Hat jedes Modell ein Mesh mit Eckpunkten?
- **Ist der Maßstab plausibel?** — die Pistole misst 0,22 m, das Gewehr 1,23 m,
  die Fässer 0,88 m usw. Kein „100 m langes Gewehr"-Importfehler.
- Trägt jedes Material eine Textur, ist die Normalmap als Normalmap markiert?
- Sind die Schuss-Sounds kürzer als 3 Sekunden (die Rohdateien waren bis 17 s
  lang — ich habe sie auf ~0,6 s zurechtgeschnitten)?
- **Läuft alles auch OHNE die Dateien** (Rückfall auf Code)?

Was ungeprüft bleibt und **du beim Spielen beurteilen musst**:
- Sieht die Textur-Kachelung richtig aus, oder ist der Beton gestreckt/zu klein?
- Hängt die Pistole / das Gewehr richtig in der Hand, oder verdreht/verschoben?
- Ist der Himmel zu hell/zu dunkel für die Arena?
- Klingen die Schüsse gut, oder abgeschnitten?

---

## Spieltest-Auftrag

Bitte starten und darauf achten:

**Übersicht / Optik**
- Wände und Boden: echte Textur sichtbar, oder wirkt sie verwaschen/gestreckt?
- Deko-Modelle (Fässer, Kisten): stehen sie am Boden, oder schweben/versinken
  sie? (Der Dreh-/Höhen-Wert ist eine Ein-Zeilen-Korrektur.)
- Himmel: passt die Sonnenuntergang-Stimmung, oder ist es zu hell?

**Waffen**
- Pistole in der Hand: richtig gehalten, richtige Größe, zeigt sie nach vorn?
- Beim Kaufmenü das Scharfschützengewehr nehmen: sitzt es in der Hand?
- (Sturmgewehr/MP sehen aus wie vorher — das ist gewollt.)
- Wichtig: Feuert die Waffe noch normal, Nachladen ok?

**Sound**
- Klingen Gewehr / MP / Sniper / Pistole jetzt „echt", oder abgeschnitten/
  knackend?

**Was kaputt sein könnte, bitte melden**
- Modell an falscher Stelle, im Boden, in der Luft, doppelt.
- Waffe verdreht in der Hand.
- Textur komplett schwarz oder pink (Material-Fehler).
- Spiel ruckelt stärker als vorher (die Texturen sind größer als einfarbige
  Flächen).

---

## Noch offen

**P7 — echte Spielfiguren (braucht dich).** Realistische, *animierte* Figuren
gibt es CC0-ohne-Login nicht. Der gute Weg heißt **Mixamo** (gratis von Adobe,
kommerziell erlaubt) — braucht aber einen Adobe-Login, und Konten anlegen bzw.
Passwörter eingeben darf ich nicht.

Die Anbindung ist **fertig vorbereitet**: `CharacterVisual` lädt automatisch
eine Figur aus `Resources/Models/figur`, sobald sie da ist, samt Animationen
(Stehen/Gehen/Laufen/Sterben). Der Import-Knopf steht bereit
(„Infront/Assets/Figur aus Mixamo bauen").

**Was du tun müsstest** (wenn du willst):
1. Auf mixamo.com mit Adobe-Konto anmelden (gratis).
2. Einen Charakter wählen, als FBX herunterladen (T-Pose, mit Haut).
3. Vier Animationen wählen und je als FBX herunterladen: „Idle", „Walking",
   „Running", „Falling Back Death" — jeweils **Without Skin** und **In Place**.
4. Die fünf Dateien nach `Assets/_Project/Art/Figures/` legen und so benennen:
   `basis.fbx`, `idle.fbx`, `walk.fbx`, `run.fbx`, `death.fbx`.
5. Mir Bescheid geben — ich baue sie ein und teste.

**Sturmgewehr + MP:** bleiben die Code-Waffe. Wenn dir das wichtig ist, kann ich
später auf dem Unity Asset Store nach einem passenden Waffen-Set schauen (dort
gibt es welche, aber mit anderer Lizenz — müssten wir prüfen).

**Restliche Sounds** (Nachladen, Schritte, Bombe, Rundenmeldungen): bleiben
synthetisch. Die „Free Firearm Sound Library" hat nur Schüsse. Für den Rest
bräuchten wir eine zweite CC0-Quelle.

---

## Rückweg

- Voller Code-Stand vor dem Umbau: Scratchpad-Backup.
- Jede echte Datei ist einzeln löschbar → Rückfall auf die Code-Version.
- Die Roh-Downloads liegen in `Assets/_Project/Art/Textures|Sky|Models` (ca.
  73 MB) und `Assets/_Project/Audio/Resources` (0,2 MB).
