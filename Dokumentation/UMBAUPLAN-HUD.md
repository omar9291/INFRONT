# Umbauplan — „Übersichtlicher und cooler"

> **STATUS: ABGESCHLOSSEN (2026-09-01).** Alle Pakete U1–U6 und A1–A4 umgesetzt.
> 98/98 PlayMode-Tests grün. Details in `PROGRESS.md`. Optik bleibt ungeprüft
> (Screenshots auf diesem Rechner gesperrt).

Auftrag vom 2026-09-01: *„mach es erstens übersichtlicher und zweitens coolere
Sachen, z. B. Animation"* + *„mach das ganze Spiel übersichtlicher"*.

Entschieden mit dem Nutzer:
- Am unübersichtlichsten: **das HUD im Spiel** und **das Kaufmenü**
- Umfang: **ganzes HUD neu in UI Toolkit** (alle 12 IMGUI-Zeichner ersetzen)
- Animation: **alle vier Bereiche** (HUD, Kampf-Wumms, Figuren/Waffe, Übergänge)

---

## Warum überhaupt

Menü und Ladebildschirm laufen auf UI Toolkit mit gemeinsamem Design
(`UiTheme.cs`, Dark Tactical, Orange-Akzent). Das HUD im Spiel besteht dagegen
aus 12 unabhängigen IMGUI-Skripten, die jedes für sich irgendwohin malen:

`MatchHud`, `AbilityHud`, `BombHud`, `KillFeedHud`, `DamageFeedback`,
`Scoreboard`, `BuyMenuHud`, `FriendlyNameplates`, `HighlightBanner`,
`PauseMenu`, `MainMenu`, `BombExplosionFx`

Gefundene Probleme:

1. **10 verschiedene Schriftgrößen** (11, 13, 14, 15, 16, 18, 20, 22, 26, 40) —
   jede Datei erfindet ihre eigene. Dazu Unitys graue Systemschrift.
2. **Die Bildmitte ist überfüllt.** Fünf Dinge zielen auf dieselbe Spalte und
   können gleichzeitig erscheinen: Kaufzeit (13 % Höhe), Rolle (13 % + 40 px),
   „BOMBE GELEGT" (19 %), Highlight-Banner (28 %), Bombenbalken (62 % / 70 %).
3. **Nichts hat einen Rahmen.** Leben, Geld, Munition sind nackter Text unten
   links. Das Auge findet nichts wieder.
4. **Das HUD bewegt sich nie.** Zahlen springen um. IMGUI kann Animation
   prinzipiell schlecht — deswegen lösen Umbau und Animation sich gemeinsam.

---

## Technische Grundregeln für den Umbau

- **Klassennamen und alle `...ForTests`-Haken bleiben erhalten.** Es wird nur
  das Zeichnen ausgetauscht (OnGUI → VisualElements), nicht die Logik. Damit
  bleiben die 93 Tests gültig. Betroffen laut Test-Suche: `KillFeedHud`
  (BotSenseTests, BombEconomyTests), `HighlightBanner`, `LookTests`.
- **`PickingMode.Ignore` auf allem, was nicht anklickbar ist.** Sonst frisst
  das HUD die Mausklicks und man kann nicht mehr schießen. Nur Kaufmenü,
  Pausenmenü und Rundenende-Knöpfe dürfen Klicks annehmen.
- **Ein einziges UIDocument** fürs ganze HUD, `sortingOrder = 10`
  (Ladebildschirm liegt auf 100, bleibt darüber). PanelSettings ist das
  vorhandene `Resources/InfrontPanel` (ScaleWithScreenSize, 1920×1080) —
  damit skaliert das HUD genau wie das Menü.
- **Nichts wird ersatzlos gelöscht.** Ein IMGUI-Zeichner verschwindet erst,
  wenn sein UI-Toolkit-Ersatz steht und der Test grün ist.
- Nach jedem Paket: `SceneBuilder.Build` + voller Testlauf.

---

## TEIL 1 — Übersichtlichkeit

### U1 — Fundament: `HudRoot` + Theme-Erweiterung
Neue Datei `HudRoot.cs`. Ein UIDocument, das ein festes **Zonen-Raster**
aufspannt: ObenLinks / ObenMitte / ObenRechts / Mitte / UntenLinks /
UntenMitte / UntenRechts. Jedes HUD-Teil bekommt **eine feste Zone** — damit
können sich Anzeigen nicht mehr gegenseitig überlagern.

`UiTheme` wird additiv erweitert (nichts gelöscht): feste Schriftstufen
(XS 12 / S 14 / M 18 / L 26 / XL 44), Team-Farben, Bausteine `HudPanel()`,
`HudBar()`, `HudChip()`.

### U2 — Linker Block: Leben / Weste / Geld / Munition
Zusammenhängendes Panel unten links statt drei loser Texte:
Lebensbalken mit Zahl darin, Westenbalken darüber, Geld als eigene Zelle.
Munition wandert nach **unten rechts** und wird groß (`24 / 30`) — trennt
„mein Zustand" (links) von „meine Waffe" (rechts). Waffenslots als zwei
Kästchen `[1] [2]` statt Fließtext.

### U3 — Kopfbereich: Punktestand, Zeit, Rolle, Lebende
Eine Leiste oben Mitte: `ALPHA 5` — Timer — `3 BRAVO`, eigenes Team farbig
hinterlegt statt `>ALPHA<`.

Darunter **eine einzige Statuszeile** nach Priorität, statt fünf gestapelter
Texte: Kaufzeit / Rolle / „Bombe gelegt 32 s". Immer nur eine Meldung.

Neu: **Lebende-Anzeige** — pro Team eine Reihe Rauten, ausgegraut wenn tot.
Das ist die größte Übersichts-Verbesserung, die so ein Spiel haben kann.

### U4 — Fähigkeitsleiste und Killfeed
Fähigkeiten unten Mitte als drei echte Kacheln mit Rahmen: Taste im Eck,
Ladungen als Punkte, Abklingzeit als dunkler Balken, der leerläuft.
Killfeed oben rechts mit Zeilenhintergrund statt nacktem Text.

### U5 — Kaufmenü neu
Zweispaltig: links Waffen, rechts Ausrüstung + Fähigkeiten. Jede Zeile mit
Name, Preis, Tastenkürzel im Kästchen, Haken bei „gekauft". Nicht Leistbares
ausgegraut. Geld und Restzeit oben groß, Kaufzeit als Fortschrittsbalken.

### U6 — Scoreboard, Pause, Rundenende
Gleicher Panel-Stil, Tabellen mit Kopfzeile. Der Rundenende-Bildschirm wird
ein richtiges Panel statt zweier nackter Knöpfe mitten im Bild.

---

## TEIL 2 — Animation und Wumms

### A1 — HUD lebendig
- Geld zählt hoch/runter statt zu springen
- Lebensbalken mit „Geisterbalken", der dem echten Wert hinterherläuft;
  Panel blitzt rot und ruckelt kurz bei Schaden
- Munitionszahl pulst beim Schuss, wird rot unter 25 %, Nachlade-Ring
- Fähigkeitskachel blitzt auf und schrumpft kurz beim Einsatz
- Killfeed-Zeilen fahren von rechts ein und verblassen weich
- Statuszeile blendet über statt hart zu wechseln

### A2 — Kampf-Wumms
- Trefferbestätigung: Fadenkreuz-Haken skaliert auf; Kopftreffer eigene Farbe
- Schadenszahlen, die am Gegner hochsteigen und ausblenden
- **Zeitlupe beim letzten Kill der Runde** (~0,35 s auf 40 % Tempo)
- Kräftigerer Mündungsblitz, dickere Tracer
- Ace/Clutch-Banner fährt ein statt nur ein-/auszublenden

### A3 — Figuren und Waffe
- Figur neigt sich beim Sprint und in Kurven, sackt bei der Landung ein
- Waffe wird im Stehen ruhiger, wackelt beim Sprint stärker (Sprinthaltung:
  Waffe schräg nach unten)
- Nachladen auch an der Figur sichtbar
- Umkippen beim Tod in die Richtung, aus der der Schuss kam

### A4 — Übergänge
- Menüpunkte fahren beim Öffnen nacheinander ein
- Rundenstart: Rolle groß eingeblendet, fährt dann zur Statuszeile hoch
- Menü → Ladebildschirm → Arena weich statt harter Schnitt

---

## Was ich weiterhin NICHT prüfen kann

Auf diesem Mac sind Screenshots und synthetische Eingaben gesperrt. Ich kann
**nicht sehen**, ob es schön aussieht, ob etwas verrutscht ist oder ob eine
Animation ruckelt. Die Tests prüfen nur: existiert das Element, hat es den
richtigen Wert, reagiert es auf das richtige Ereignis, stürzt nichts ab.
Die Optik muss der Nutzer beurteilen.
