# NACHTPLAN — Sitzung 9 (Nacht vom 2026-09-03 auf 2026-09-04)

> **STATUS: ABGESCHLOSSEN.** P1–P8 alle fertig, 126/126 Tests grün, Mac-Build
> neu (Version 1.1) und gestartet. Ergebnis: `Dokumentation/MORGENBERICHT-9.md`.
> Falls der Wecker noch feuert: nichts mehr zu tun - nur den Stand melden.

**Diese Datei ist die Arbeitsanweisung für die Nacht.** Nach jedem Komprimieren
des Gesprächs zuerst wieder hier lesen.

Auftrag des Nutzers (aus dieser Sitzung, wörtlich):
> „ich möchte, dass das spiel richtig richtig realistisch wird. […] das spiel
> soll intensiver werden, also es soll sich mehr wie ein krieg anfühlen. nebel
> usw (realistisch natürlich) soll auch manchmal auftauchen. […] man soll
> wirklich das gefühl von kampf erleben. ich möchte, dass das spiel richtig
> krass wird, egal wie lange es dauert."

Und für heute Nacht: „weiter, wir machen wieder eine nachtrunde".

---

## 0. Entscheidungen des Nutzers (vor dem Schlafen abgefragt)

| Frage | Antwort |
|---|---|
| Umfang | **Etappe 3 komplett, danach Etappe 5, so weit es geht.** |
| Nebel | **Nur Optik. Die Sichtweite im Spiel bleibt gleich.** Kein Eingriff in Bot-Sicht, keine Balance-Änderung. |
| Waffen | **Aus Code richtig detailliert bauen** (Magazin, Handschutz, Schaft, Visier, Schiene, Mündungsbremse). Nicht auf CC0-Modelle warten. |
| Modell | Plan auf Opus, Umsetzung die ganze Nacht auf Sonnet. **Kein weiteres STOPP in der Nacht.** |
| Entscheidungen nachts | Immer die vorsichtigste, nicht-zerstörende Variante wählen, weiterbauen, und in `PROGRESS.md` unter „Nachts allein entschieden" notieren. |

Weiter gültig: alles auf Deutsch mit **echten Umlauten** (Dateinamen, Klassen-,
Methoden-, Variablennamen und Asset-Ids bleiben ASCII), alles per Code erzeugt,
alles headless getestet. Optik und Klang kann ich **nicht** prüfen und behaupte
das auch nie.

---

## 1. Eiserne Regeln für die Nacht

1. **Nach jedem Paket: voller Testlauf** (aktuell 112 Tests). Rot heißt:
   reparieren oder das Paket zurücknehmen. Niemals rot schlafen gehen.
2. **Nach jedem Paket: Commit + Push.** Repo `omar9291/INFRONT`, Branch `main`,
   Push ist für dieses Repo erlaubt. Vor dem Commit:
   `git add -A && git reset -q ProjectSettings/PackageManagerSettings.asset ProjectSettings/URPProjectSettings.asset`
3. **Nach jedem Paket: `PROGRESS.md` fortschreiben** und hier den Haken setzen.
   Wenn das Gespräch komprimiert wird, ist das die einzige Erinnerung.
4. **Rückfallebene für alles Neue.** Fehlt eine Tondatei → Platzhalter-Ton.
   Fehlt ein Modell → Code-Geometrie. Neues System stürzt ab → das alte läuft
   weiter. Alles Teure hängt an „Bild: Voll" und ist bei „Schlicht" aus.
5. **Nichts löschen, nur ergänzen.** Bei der Karte ausschließlich hinzufügen,
   keine bestehende Geometrie verschieben — sonst kippt die Balance.
6. **Am Ende der Nacht:** neu bauen, Spiel starten, `MORGENBERICHT-9.md`.

## 2. Werkzeug-Spickzettel

Unity: `/Applications/Unity/Hub/Editor/6000.5.8f1/Unity.app/Contents/MacOS/Unity`
Projekt: `/Users/user/UnityProjects/INFRONT`

Szene neu bauen:
`Unity -batchmode -quit -projectPath <PROJ> -executeMethod Infront.EditorTools.SceneBuilder.Build -logFile Logs/scene.log`
→ Erfolg = `SCENE_BUILD_OK` im Log. Fehler = `error CS...`.

Tests (voll): `/Users/user/.unity/bin/unity test <PROJ> --mode PlayMode --output Logs/all-tests.xml --timeout 1800 --non-interactive`
Gefiltert: `--filter "Infront.Tests.KlasseA|Infront.Tests.KlasseB"`

Bauen: `-executeMethod Infront.EditorTools.GameBuilder.BuildMac` → `BUILD_RESULT Succeeded`
Starten: `open Builds/INFRONT.app`

**Vor jedem Netzwerk-Testlauf zwingend:**
```
pkill -f "Builds/INFRONT.app/Contents/MacOS/INFRONT"; pkill -f AssetImportWorker
rm -f Logs/all-tests.xml
```
Sonst hält die laufende App den UDP-Port 7777 und alle Netz-Tests fallen um.

Ausgangslage: **112/112 Tests grün**, HEAD = `ccc6fe8`, Mac-Build frisch.

---

## 3. Die Pakete

### [x] P1 — Wetter pro Runde (nur Optik) — FERTIG, 116 Tests grün, committet

Neu: `WeatherDirector` (Runtime). Server wählt beim Rundenstart eine von fünf
Wetterlagen und verteilt sie als `NetworkVariable`, damit Host und Clients
dasselbe Bild haben. Blendet über ~2 s weich um.

| Lage | Wirkung |
|---|---|
| Klar | wie bisher, kühl |
| Dunst | leichter Schleier, entsättigt |
| Staubwind | warm-braun, Dichte höher, mehr Staubpartikel |
| Bodennebel | dichte Nebelbank **unter Hüfthöhe**, Fernsicht bleibt frei |
| Rauch nach Beschuss | grau, dunkel, Sonne gedämpft |

**Harte Grenze aus der Nutzer-Entscheidung: die Sichtweite darf sich nicht
ändern.** Deshalb:
- Die Distanz-Nebeldichte bleibt in einem sicheren Band (Obergrenze so, dass
  auf 60 m ein Gegner klar lesbar bleibt). Ein Test prüft diese Obergrenze.
- Der eigentliche „Nebel-Effekt" kommt aus einer **Bodennebel-Partikelschicht**
  unterhalb der Augenhöhe. Sieht stark aus, verdeckt aber keinen stehenden
  Gegner.
- Bot-Sichtweite wird **nicht** angefasst.

Tests (`WetterTests`): Wetter wechselt zwischen Runden; jede Lage bleibt unter
der Dichte-Obergrenze; bei „Bild: Schlicht" ist alles aus; Bot-Sichtweite ist
in allen Lagen identisch.

### [x] P2 — Ernste Beleuchtung, echte Schatten — FERTIG, 119 Tests grün, committet

**Gebaut:** URP-Asset (GraphicsTune): Zusatzlicht-Schatten AN, weiche Schatten
AN, Schattenweite 70, 4 Kaskaden. Sonne: shadowStrength 0.85. Fünf grosse
Ankerlichter (MidGlow, SiteLight_A/B, HalleLight_1/2) werfen jetzt echte weiche
Schatten (ForcePixel, damit sie nie wegoptimiert werden). Umgebungslicht hart
runter (ambientIntensity 0.6 → 0.4 bzw. Trilight-Farben halbiert), damit die
Schatten und die Kanten-Abdunklung überhaupt lesen. Tunnel-Notlichter bleiben
schattenlos (Kosten + sie flackern).

**SSAO: heute Nacht NICHT gemacht** — siehe Abschnitt 4.

- **SSAO** in den URP-Renderer (Editor-Code, `UrpSetup`). Das ist der Effekt,
  der Ecken abdunkelt und Objekte auf den Boden „stellt".
- Schattenweite und Kaskaden sinnvoll setzen (Karte ist 100×100 m).
- Die wichtigsten Punktlichter bekommen echte Schatten (nicht alle — Kosten!):
  Halle, Bombenplätze, Mittelpodest. Tunnel-Notlichter bleiben schattenlos.
- Umgebungslicht weiter herunter, damit die Schatten überhaupt zu sehen sind.
- Alles Teure hängt an „Bild: Voll".

Tests (`BeleuchtungTests`): SSAO ist im Renderer; mindestens N Lichter werfen
Schatten; bei „Schlicht" sind die teuren Sachen aus.

### [x] P3 — Staub und Lichtschächte — FERTIG, 124 Tests grün, committet

Staub kam schon mit P1 (`AtmosphereDust`, 4 Volumen: ganze Karte, Halle, beide
Sites). Dazu jetzt: 6 `ShaftLight`-Spots (2 Halle, je 1 Site, je 1 Aussenweg) -
der additive Staub fängt den Kegel ein, das ergibt den sichtbaren Lichtschacht
ganz ohne Zusatz-Geometrie. Neuer Test in `BeleuchtungTests`.

- `AtmosphereDust` (aus `MenuDust` abgeleitet, das Original bleibt unangetastet)
  an mehreren Stellen der Arena, Dichte und Farbe folgen dem Wetter.
- Sichtbare Lichtkegel an Hallenöffnungen und Dachlücken (Spot-Lichter plus
  additive Kegel-Geometrie), damit der Staub etwas hat, worin er leuchtet.

Tests: Partikelsysteme existieren und laufen; bei „Schlicht" aus.

### [x] P4 — Umgebungston (Krieg drumherum) — FERTIG, 124 Tests grün, committet

`AmbientWar.cs` (am HUD-Objekt der Arena): dauerhaftes Windbett (eigene
Schleifen-AudioSource), dazu in unregelmässigen Abständen ferne 3D-Ereignisse -
Dauerfeuer (45 %), Artillerie mit Schall-Verzögerung (20 %), Hubschrauber
(17 %), knarzendes Metall (18 %). In der Kaufzeit leiser + längere Abstände.
Wind folgt dem Wetter (Staubwind lauter). Ferne Ereignisse pausieren, solange
der MatchManager für Tests ausgesetzt ist. 5 neue `SoundId` + Platzhalter in
`ProceduralSfx`, echte Dateien später eintauschbar. Neuer Test-Zugang
`AudioService.Resolve(id)`. Neue Tests: `AmbientTests` (4).

Neu: `AmbientWar` (Runtime, in der Arena). Das ist der größte Einzelposten
gegen „tot".
- **Windbett**, dauerhaft leise, Stärke nach Wetterlage.
- **Zufällige ferne Ereignisse** im Abstand von einigen Sekunden: Artillerie
  (tiefer Einschlag mit Verzögerung), fernes Dauerfeuer, vorbeiziehender
  Hubschrauber, knarzendes Metall.
- Lautstärke und Häufigkeit hängen an der Rundenphase (Kaufzeit ruhiger).
- Neue `SoundId`-Einträge + Platzhalter in `ProceduralSfx`. Der `AudioService`
  nimmt automatisch eine echte Datei, sobald sie unter
  `Assets/_Project/Audio/Resources/<name>.wav` liegt — Austausch später möglich,
  ohne Code anzufassen.

Tests (`AmbientTests`): Wind läuft; ferne Ereignisse feuern innerhalb eines
Zeitfensters; in der Kaufzeit leiser; Töne fallen auf Platzhalter zurück.

### [x] P5 — Karte entklotzen (nur ergänzen) — FERTIG

`BuildDetailWerk()` (nur Deko, keine Collider - NavMesh unberührt): Fensterrahmen
in Hallen- und Aussenwänden (dort wo die Lichtschächte einfallen), Säulenköpfe
und -sockel, Deko-Geländer an Podest- und Balkonkanten, sechs Trümmerhaufen,
vier zusätzliche Sandsackstellungen an den Bombenplätzen, Kabelstränge an den
Wandoberkanten, Wandkästen, Dachlatten quer über den Aussenwegen (brechen das
Licht). Neuer Test in `BeleuchtungTests`.

Ausschließlich zusätzliche Geometrie, nichts Bestehendes verschieben:
Fensterrahmen in den Hallenwänden, mehr Geländer an Podest und Balkonen,
Säulenköpfe und -sockel, Trümmerhaufen, Sandsackstellungen (`sandsack`-Modell
ist schon da), Kabelstränge, Wandkästen, Dachlatten über den Außenwegen (die
werfen die Lichtschächte aus P3).

Test: NavMesh bäckt weiter, Bots erreichen beide Bombenplätze (vorhandene
Bot-Tests müssen grün bleiben).

### [x] P6 — Menü ernster — FERTIG (zurückhaltend)

Der Freund findet das Menü gut, deshalb nur eine Stimmungs-Korrektur, kein
Umbau: im Kino-Look weniger Bloom, etwas mehr Vignette, mehr Kontrast, das
Menü-Bild klar entsättigt (ColorAdjustments nur bei `_menuLook`). Backdrop:
Suchscheinwerfer langsamer/schwächer, Eisblau-Neon fast ausgeschaltet.
`UiTheme.Ice` gedeckter (betrifft auch HUD-Zahlen - im Sinne des ernsteren
Tons, einzeilig zurückdrehbar), `Sheen` dezenter. Struktur unverändert,
`MenuUiTests` bleiben grün.

Gedämpfte Militärfarben statt Eisblau-Leuchten, weniger Bloom und Glas-Glanz,
ruhigere Animation, härtere Kanten. Die Struktur des Menüs bleibt.

Test: `MenuUiTests` bleiben grün.

### [x] P7 — Waffen aus Code, richtig detailliert — FERTIG

`ViewModel.RefreshShape` teilt jetzt in drei Zweige: Pistole / Maschinenpistole /
Sturmgewehr (Sniper + Pistole haben schon echte CC0-Modelle - die bleiben).
- Sturmgewehr: ~26 Teile - Gehäuse, Oberschiene mit Picatinny-Zähnen,
  Handschutz mit Lüftungsschlitzen, langer Lauf, dreiteilige Mündungsbremse,
  Ladehebel, verstellbarer Schaft mit Wange, Pistolengriff, Abzugsbügel,
  gebogenes Magazin, getrennte Kimme/Korn.
- Maschinenpistole: ~15 Teile - kompakt, Vordergriff, Klappschaft (zwei
  Streben + Platte), langes steiles Magazin, kurze Schiene.
Neuer Test in `ViewModelTests` (`PartCountForTests`).

### [x] P8 — Abschluss — FERTIG

126/126 Tests grün. Szene neu gebaut, Mac-App neu (Version 1.1) + gestartet.
`MORGENBERICHT-9.md` geschrieben, `PROGRESS.md` fortgeschrieben. Alles
committet und gepusht. (ASSETS.md: keine Änderung - alle neuen Töne sind
Code-Platzhalter, keine heruntergeladenen Pakete.)

---

## 4. Nachts allein entschieden

(Hier alles eintragen, was ohne Rückfrage entschieden wurde — der Nutzer liest
das morgens.)

- **P1: WeatherDirector als reine MonoBehaviour statt NetworkVariable.**
  Grund: Host-Modus V1 hat nur den einen echten Spieler, Bots sehen kein
  Wetter. Eine feste Tabelle + Sitzungs-Zufall reicht und ist risikofrei
  (kein Netz-Sync, der schiefgehen kann). Wenn später echtes Multiplayer
  kommt, wird daraus eine NetworkVariable.
- **P1: Nebeldichte hart auf 0.013 gedeckelt.** Die stärkste Lage ("Rauch")
  läge sonst bei 0.012 — schon spürbar. Der Deckel ist die Garantie für
  "Sichtweite bleibt gleich". Ein Test bewacht ihn.
- **P2: SSAO (Umgebungsverdeckung) heute Nacht nicht gemacht.** Grund: SSAO
  ist in URP ein "Renderer Feature", das man nur über einen fragilen
  SerializedObject-Eingriff ins Renderer-Asset einhängt — und die dafür
  nötige `GetInstanceID()`-API ist in Unity 6 als veraltet markiert, was
  hier Build-Fehler gibt (schon einmal passiert). Ein blinder nächtlicher
  Versuch ist zu riskant. Stattdessen: echte Schatten von 5 Ankerlichtern
  + Umgebungslicht runter — das "stellt" die Objekte auch auf den Boden.
  SSAO bleibt als sauberer nächster Schritt (manuell, mit Sicht aufs Bild).
- **P5: Karte nur ergänzt, nichts verschoben.** Alle neuen Teile sind Deko
  ohne Collider — Wege, Deckungen, NavMesh und damit die Balance sind
  garantiert unverändert.
- **P6: sehr zurückhaltend.** Der Freund findet das Menü gut, also nur eine
  Stimmungs-Korrektur (weniger Leuchten, entsättigt), kein Umbau. `UiTheme.Ice`
  betrifft dabei auch die HUD-Zahlen im Spiel — bewusst so gelassen, weil es
  zum ernsteren Ton passt; eine Zeile in `UiTheme.cs` dreht es zurück.
- **P7: Waffen aus Code, nicht heruntergeladen.** War deine Entscheidung.
  Sturmgewehr/MP kommen bei Poly Haven nicht als CC0 vor; die Code-Modelle
  sind jetzt so detailliert wie Würfel es zulassen. Ein echtes Modell wäre
  noch besser — falls du mal eine gute CC0-Quelle findest, ist der Weg
  (`AssetLibrary.Model` + `ViewModel.PoseFor`) schon da.
- **`Application.version` auf 1.1 gezogen** (stand als offener Punkt an), damit
  Freunde die Builds auseinanderhalten können.
