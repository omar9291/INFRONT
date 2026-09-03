# MORGENBERICHT — Nacht 9 (2026-09-03 auf 2026-09-04)

Guten Morgen. Die Nachtrunde ist durch. Hier steht, was passiert ist, was du
dir ansehen solltest und was offen bleibt.

## Auftrag

Du wolltest nach der Freund-Rückmeldung ("Gameplay wirkt wie ein totes
Roblox-Spiel"): realistischer, intensiver, "wie Krieg", Nebel der manchmal
auftaucht, echte Schüsse, ernsteres Menü. Entschieden hattest du:
**Etappe 3 komplett, dann Etappe 5** — Nebel **nur Optik** (Sichtweite bleibt
gleich), Waffen **aus Code detailliert bauen**.

## Was gebaut wurde (7 Pakete, alle committet und gepusht)

| # | Paket | Kurz |
|---|---|---|
| P1 | **Wetter pro Runde** | 5 Lagen (Klar, Dunst, Staubwind, Bodennebel, Rauch), pro Rundenstart eine neue, ~2 s weiche Blende. Nebelfarbe, Sonnenstärke, Staub, flache Nebelbank. **Sichtweite ändert sich NICHT** (Nebeldichte hart gedeckelt, Bot-Sicht unangetastet). |
| P2 | **Ernste Beleuchtung** | Echte weiche Schatten von 5 grossen Ankerlichtern + Sonne. Umgebungslicht runter, damit Schatten und Kanten lesen. Objekte "stehen" jetzt auf dem Boden statt zu schweben. |
| P3 | **Lichtschächte** | 6 schräge Lichtkegel (Halle, Bombenplätze, Aussenwege). Der treibende Staub fängt sie ein — sichtbarer Schacht ohne Zusatz-Geometrie. |
| P4 | **Umgebungston** | Dauerhaftes Windbett + ferne Ereignisse: Dauerfeuer, Artillerie (mit Schall-Verzögerung), Hubschrauber, knarzendes Metall. In der Kaufzeit ruhiger. **Das ist der grösste Posten gegen "tot".** |
| P5 | **Karte entklotzt** | Fensterrahmen, Säulenköpfe/-sockel, Geländer, Trümmerhaufen, mehr Sandsackstellungen, Kabelstränge, Wandkästen, Dachlatten. Nur Deko — Gameplay und NavMesh unberührt. |
| P6 | **Menü ernster** | Zurückhaltend (der Freund findet das Menü gut): weniger Leuchten, entsättigt, ruhigere Scheinwerfer, Eisblau-Neon fast aus. Struktur unverändert. |
| P7 | **Waffen aus Code** | Sturmgewehr (~26 Teile) und MP (~15 Teile) statt der groben Würfel — Schiene, Handschutz, Mündungsbremse, verstellbarer/Klapp-Schaft, gebogenes Magazin, getrennte Visierung. |

## Was du dir ansehen / hören solltest

- **Runde für Runde anderes Wetter.** Ein paarmal neu starten — bei "Bodennebel"
  liegt eine Nebelbank am Boden, bei "Staubwind" ist alles warm-braun und
  staubig, bei "Rauch" grau und die Sonne gedämpft. Prüf, ob sich das gut
  anfühlt und ob die Sicht wirklich immer reicht.
- **Der Ton.** Läuft jetzt ständig etwas im Hintergrund — Wind, in der Ferne
  Gefechte, ab und zu ein Artillerie-Einschlag oder ein Hubschrauber. Sag mir,
  ob das zu viel, zu wenig oder zu oft ist — das sind alles Zahlen, die ich
  nach deinem Ohr einstelle.
- **Die Schatten.** Steht in der Halle und an den Bombenplätzen jetzt Kontrast?
  Wirken die Kisten "aufgestellt"?
- **Die Waffe in der Hand.** Sturmgewehr und MP sollten deutlich mehr nach
  Waffe aussehen. Wenn etwas verdreht oder verschoben in der Hand hängt: sag
  mir welche Waffe und wie — ich kann es nicht sehen, aber im Code an einer
  Stelle nachjustieren.
- **Das Menü.** Wirkt es ernster? Falls zu dunkel/flau: einzeln zurückdrehbar.

## Ungeprüft (kann ich auf diesem Rechner nicht)

Wie alles **aussieht und klingt**. Ich habe für jedes Paket automatische Tests
geschrieben (Wetter bleibt im sicheren Nebel-Band, Schatten sind an,
Windbett läuft, Waffe hat genug Teile, Karte hat Detail bekommen …), aber
Optik und Klang beurteilst du.

## Nachts allein entschieden

- **P1: WeatherDirector als einfache MonoBehaviour** statt NetworkVariable —
  Host-V1 hat nur einen echten Spieler. Wird bei echtem Multiplayer nachgezogen.
- **P1: Nebeldichte hart auf 0.013 gedeckelt** — die Garantie für "Sichtweite
  bleibt gleich". Ein Test bewacht das.
- **P2: SSAO (Umgebungsverdeckung) NICHT gemacht.** Der Einbau in URP ist
  fragil und die nötige API in Unity 6 als veraltet markiert (gab schon
  Build-Fehler). Zu riskant für einen blinden Nachtlauf. Stattdessen echte
  Schatten + dunkleres Umgebungslicht — bringt denselben "steht auf dem Boden"-
  Effekt. SSAO bleibt als sauberer nächster Schritt mit Sicht aufs Bild.
- **P6: `UiTheme.Ice` gedeckter** — betrifft auch die HUD-Zahlen im Spiel, im
  Sinne des ernsteren Tons. Einzeilig zurückdrehbar, wenn dir das nicht gefällt.

## Offen / als Nächstes

- **Etappe 4 „Menschen"** — weiter auf dich blockiert: du brauchst ein
  Adobe-Konto und lädst 5 Mixamo-Dateien herunter (basis.fbx, idle.fbx,
  walk.fbx, run.fbx, death.fbx) nach `Assets/_Project/Art/Figures/`. Die
  Anbindung steht. Von keinem Browser hier komme ich an das Konto.
- **Platzhalter-Töne** noch als Code: wind, artillerie, hubschrauber usw. —
  klingen nach Prototyp. Echte CC0-Dateien später eintauschbar ohne Code.
- **SSAO** (siehe oben).
- **Balance-Feintuning** von Wetter, Ton-Lautstärke/-Häufigkeit, Schatten-
  Stärke — alles nach deinem Auge/Ohr.

## Zahlen

- PlayMode-Tests: **126/126 grün** (Start der Nacht: 112 - dazu 14 neue:
  4 WetterTests, 4 BeleuchtungTests, 4 AmbientTests, 2 ViewModelTests).
- Commits: P1 `32e98fa`, P2 `865944a`, P3+P4 `b60d763`, P5+P6+P7 (+ Version 1.1).
- Mac-Build neu gebaut (Version **1.1**) und gestartet.
