# NACHTPLAN — Sitzung 8 (Nacht vom 2026-08-31 auf 2026-09-01)

> **STATUS: ABGESCHLOSSEN.** P0-P11 alle fertig, 93/93 Tests gruen, Mac-Build
> neu, Spiel gestartet. Ergebnis: Dokumentation/MORGENBERICHT.md und der
> Verlauf in Dokumentation/PROGRESS.md ("NACHT 8").
> Falls der Wecker noch feuert: nichts mehr zu tun - nur den Stand melden.


**Diese Datei ist die Arbeitsanweisung für die Nacht.** Nach jedem
Komprimieren des Gesprächs zuerst wieder hier hereinschauen und beim
nächsten offenen Paket weitermachen.

Auftrag des Nutzers (wörtlich):
> „mach das spiel 100 mal besser, das heisst, alles was noch nicht ingame
> ist, was wichitig ist, hinzufügen. dann dekoration, spiel verschönern mit
> packs und credits, ladebildschirm schöner machen, sodass das spiel einfach
> wirklich gut ist. natürlich den plan umsetzen. morgen früh wache ich auf
> und schaue mir an wie weit du gekommen bist."

---

## 0. Entscheidungen des Nutzers (vor dem Schlafen abgefragt)

| Frage | Antwort |
|---|---|
| Umfang | **So weit wie möglich: Etappe A3 → B → C → D → E** |
| Asset-Pakete | **CC0-Direktdownloads ohne Login erlaubt** (z.B. Kenney, Quaternius). Jedes Paket VORHER in `ASSETS.md` mit Lizenz eintragen. Kein Konto, kein Login, kein CAPTCHA. |
| Modell | Plan auf Opus, Umsetzung die ganze Nacht auf Sonnet. **Kein weiteres STOPP in der Nacht.** |
| Entscheidungen nachts | Immer die vorsichtigste, nicht-zerstörende Variante wählen, weiterbauen, und in `PROGRESS.md` unter „Nachts allein entschieden" notieren. |
| Figuren | **Figur aus Code bauen** (stilisiert, Team-Farben, Lauf-Animation). Mixamo-Anbindung trotzdem vorbereiten. |
| Credits | **Heute Nacht nicht bauen.** Als offener Punkt notieren. |
| Ladebildschirm | **Dark Tactical ausbauen** (Balken, Logo, Tipps, Kartenname, bewegter Hintergrund) — kein Bruch mit dem Menü. |
| HUD IMGUI → UI Toolkit | **Ersetzen ist ausdrücklich erlaubt**, wenn nötig. („nie gesagt, dass nichts altes gelöscht werden darf, wenns nötig ist, mach.") |

Weiter gültig: alles auf Deutsch mit echten Umlauten (Dateinamen und
Bezeichner bleiben ASCII), alles per Code erzeugt, alles headless getestet,
Optik/Klang kann ich **nicht** prüfen und behaupte das auch nie.

---

## 1. Eiserne Regeln für die Nacht

1. **Nach jedem Paket: voller Testlauf.** Rot heißt: reparieren oder das
   Paket zurücknehmen. Niemals rot schlafen gehen.
2. **Nach jedem Paket: `PROGRESS.md` fortschreiben.** Wenn das Gespräch
   komprimiert wird, ist das die einzige Erinnerung.
3. **Rückfallebene für alles Neue.** Fehlt eine Datei → Platzhalter. Stürzt
   ein neues System ab → das alte läuft weiter.
4. **Nichts löschen, wo Ergänzen reicht.** Ausnahme HUD (ausdrücklich erlaubt).
5. **Am Ende der Nacht:** neu bauen, Spiel starten, Morgenbericht schreiben.
6. Kein Commit, kein Push (nicht beauftragt; Projekt ist kein Git-Repo).

## 2. Werkzeug-Spickzettel

Unity: `/Applications/Unity/Hub/Editor/6000.5.8f1/Unity.app/Contents/MacOS/Unity`
Projekt: `/Users/user/UnityProjects/INFRONT`

Tests (voll):
`-batchmode -projectPath <PROJ> -runTests -testPlatform PlayMode -testResults <xml> -logFile <log>`
Gefiltert: zusätzlich `-testFilter "Infront.Tests.KlasseA|Infront.Tests.KlasseB"`

Bauen: `-batchmode -quit -projectPath <PROJ> -executeMethod Infront.EditorTools.GameBuilder.BuildMac -logFile <log>`
Starten: `open Builds/INFRONT.app`

**Vor jedem Testlauf Port 7777 prüfen** (`lsof -nP -i :7777`) und ein noch
laufendes `INFRONT.app` beenden — sonst scheitern alle Netzwerk-Tests.
Hintergrund-Unity per PID überwachen, **nie** `pgrep -f runTests`.

Ausgangslage: **73/73 Tests grün**, Mac-Build frisch.
Bekannt wackelig: `TeamMatchTests.Freeze_Time_blockiert_Bewegung_am_Rundenstart`
(Schwelle 0.5, geht beim zweiten Lauf durch) — kein Grund zur Panik.

---

## 3. Die Pakete in Reihenfolge

### P0 — Sicherung + Ausgangsmessung
- Kopie von `Assets/_Project/Code` und `Dokumentation` nach
  `/Users/user/UnityProjects/INFRONT_Sicherung_vor_Nacht8/`.
- Voller Testlauf als Ausgangswert (muss 73/73 sein).

### P1 — Etappe A, Paket 3: Waffe in der Hand (`ViewModel`)
Der letzte offene Punkt der Wucht-Etappe. Aktuell hält der Spieler nichts.
- Neu `ViewModel.cs`: aus Code gebautes, stilisiertes Gewehr an der Kamera
  (Körper, Lauf, Magazin, Schaft, Griff, Visier), Materialien aus `UiTheme`.
- Bewegung: Wippen beim Laufen (Bob, Stärke nach Tempo), Nachschwingen beim
  Umsehen (Sway), Rückstoß-Ruck beim Schuss, Nachlade-Animation
  (kippen + Magazinwechsel), Wechsel-Animation.
- Versteckt bei Tod und beim Zuschauen. Waffenmodell passt sich der
  gekauften Waffe an (Länge/Farbe je `WeaponStats`).
- Ersetzt den Platzhalter-Würfel in `FirstPersonCamera.EnsureViewModel()`;
  der Würfel bleibt als Rückfall, falls der Aufbau scheitert.
- Neu `ViewModelTests`: Rückstoß verschiebt die Waffe messbar, Bob hängt am
  Tempo, beim Tod ist sie ausgeblendet.

### P2 — Etappe B, Paket 1: Der Look (Bild + Karte)
- `GraphicsTune.cs` erweitern: globales Volume mit **ACES-Tonemapping,
  Bloom, Vignette, Farbgraduierung (kühle Schatten / warme Lichter),
  Filmkorn**, SSAO als Renderer-Feature, Nebel in der Ferne.
- **Achtung HDR:** HDR wurde damals wegen der senkrechten Streifen auf dem
  M1 abgeschaltet (Ursache war laut PROGRESS eigentlich Adaptive
  Performance). Deshalb HDR **nur zusammen mit einem Schalter** wieder
  anmachen: Einstellung „Grafik: Voll / Schlicht" im Menü, Voreinstellung
  „Voll". Taucht das Streifenbild wieder auf, kommt der Nutzer mit einem
  Klick zurück. In den Morgenbericht schreiben.
- `SceneBuilder`: drei Material-Familien (Boden / Wand / Deckung) statt
  Zufallsfarben, leuchtende orange Akzentstreifen an Kanten und
  Durchgängen, farbige Bodenmarkierungen für Bombenplatz A und B,
  Punktlichter an Engstellen, Team-Farben klar blau/rot.
- Neu `LookTests`: Volume existiert mit den erwarteten Effekten, der
  Schlicht-Schalter schaltet sie wirklich ab, die Karte hat die Marker.

### P3 — Etappe B, Paket 2: Figur aus Code (`CharacterVisual`)
- Stilisierte Figur aus Grundkörpern (Rumpf, Kopf, Arme, Beine) statt
  Kapsel, eingefärbt nach Team, mit Lauf-Animation aus der Geschwindigkeit
  (Beine/Arme pendeln), Kopf dreht in Blickrichtung, Sterb-Kippen.
- **Kapsel bleibt als Rückfallebene erhalten** (abschaltbar).
- Mixamo-Anbindung vorbereiten: liegt später ein Modell unter
  `Assets/_Project/Art/Resources/Figur`, wird es statt der Code-Figur
  benutzt. Der Nutzer muss dann nur die Datei ablegen.
- Neu `CharacterVisualTests`: Beine bewegen sich bei Bewegung, Team-Farbe
  stimmt, Rückfall auf Kapsel funktioniert.

### P4 — Etappe C, Paket 1: Fähigkeiten-Maschine + Rauch + Blendgranate
- Neu `AbilityStats` (Asset, wie `WeaponStats`), `AbilityCatalog`,
  `AbilityHolder` (NetworkBehaviour: Ladungen, Wirkzeit, Abklingzeit,
  server-autoritativ — Client fragt, Server entscheidet).
- Tasten **Q** / **F** / **G**. Kauf im **bestehenden** Kaufmenü mit dem
  **bestehenden** Geld-System.
- `SmokeEffect`: Rauchwand 15 s, blockiert Sicht — **auch die Sichtprüfung
  der Bots** (in `BotBrain` einhängen).
- `FlashEffect`: Blendgranate, weißer Bildschirm ~2 s beim Spieler,
  geblendete Bots zielen daneben und suchen Deckung.
- Fähigkeiten-Leiste im HUD (Ladungen + Abklingzeit).
- Neu `AbilityTests`, `AbilitySightTests`.

### P5 — Etappe C, Paket 2: Splittergranate + Scan-Puls
- `FragEffect`: Flächenschaden mit Abfall nach Entfernung, Sichtprüfung
  (keine Wandtreffer), Explosionsoptik + Ton.
- `ScanEffect`: zeigt Gegner 3 s durch Wände (Umrisse im HUD).
- Tests dafür ergänzen.

### P6 — Etappe C, Paket 3: Brandwand + Stolperdraht + Bots nutzen alles
- `FireWallEffect` (8 s Sperre, Schaden beim Durchlaufen, Bots laufen nicht
  hinein), `TripwireEffect` (Alarm + kurze Blendung).
- `BotBuyer`/`BotObjective` erweitern: Bots **kaufen und zünden** sinnvoll —
  Angreifer rauchen die Engstelle ein und blenden vor dem Sturm,
  Verteidiger sperren den Zugang mit Feuer.
- Neu `BotAbilityTests`.

### P7 — Etappe D: Gegner mit Kopf
- Bots **hören** Schüsse und Sprint-Schritte → Verdachtspunkt, nachschauen.
- Bots **nutzen Deckung**: Deckungspunkte, Peek, Rückzug, an anderer Stelle
  wieder heraus.
- Bots **halten Winkel** (Verteidiger stellen sich auf eine Tür ein).
- **Rollen**: Vorstoß / Unterstützung / Flankierer / Scharfschütze — der
  Flankierer nimmt bewusst den langen Weg.
- **Menschliches Zielen**: Reaktionszeit, Zielfehler, gelegentliches
  Danebenschießen, Überkorrektur.
- **Ansagen** im Kill-Feed („Feind Mitte!", „Ich gehe A!", „Brauche Hilfe B!").
- Schwierigkeitsgrade stellen jetzt Reaktion, Zielgüte, Aggressivität,
  Fähigkeits-Nutzung und Teamwork ein.
- Neu `BotSenseTests`, `BotCoverTests`, `BotRoleTests`. Alle alten
  Bot-Tests müssen grün bleiben.

### P8 — Etappe E: Momente
- Erkannte Momente mit Banner + Ton: Doppelkill, Dreifachkill, **Ace**,
  Kopfschuss-Kill, Rache-Kill, **Clutch**, Entschärfung in letzter Sekunde.
- „Bester der Runde" am Rundenende.
- **Endbildschirm im Dark-Tactical-Stil** (UI Toolkit) statt IMGUI:
  Abschüsse/Tode, Kopfschuss-Quote, Schaden, bester Moment.
- **Laufbahn-Statistik** in PlayerPrefs (Matches, Siege, Aces, längste
  Serie), im Menü sichtbar.
- HUD und Kill-Feed auf UI Toolkit umstellen (Ersetzen ist erlaubt).
- Neu `HighlightTests`, `CareerStatsTests`.

### P9 — Dekoration + CC0-Pakete
- Karte ausstatten: Kisten, Fässer, Container, Absperrungen, Rohre,
  Schilder, Lichtmasten — erst aus Code, dann wo sinnvoll mit
  CC0-Modellen (Kenney / Quaternius, Direktdownload ohne Login).
- **Vor jedem Einbau: Eintrag in `ASSETS.md`** (Name, Quelle, Lizenz,
  kommerziell erlaubt ja/nein).
- Skybox / Umgebungshimmel, Staubpartikel im Lichtkegel.

### P10 — Ladebildschirm (Dark Tactical ausbauen)
- `LoadingOverlay` aufwerten: INFRONT-Schriftzug, animierter Fortschritts-
  balken, bewegtes Hintergrundmuster in Dunkelgrau/Orange, wechselnde
  Spieltipps, Kartenname und Modus, weiches Ein-/Ausblenden.
- Neu `LoadingScreenTests` (Elemente vorhanden, Tipp wechselt, blendet aus).

### P11 — Abschluss
- Voller Testlauf, Mac-Build, Spiel starten.
- `PROGRESS.md` + `MASTERPLAN.md` + `ASSETS.md` fortschreiben.
- **Morgenbericht** schreiben: was ist neu, welche Taste löst was aus,
  woran erkennt man, dass es geht, was könnte kaputt sein — und ehrlich
  auflisten, was ich **nicht** prüfen konnte (alles Optische und Klangliche).

---

## 4. Offene Punkte für den Nutzer (nicht heute Nacht)

- **Credits-Bildschirm** — ausdrücklich vertagt. Beim Aufgreifen fragen:
  welche Namen (Driftlab, Vorname, Leonit?), welche Danksagungen.
- **Mixamo-Figur** — braucht ein Adobe-Konto, das kann nur der Nutzer.
  Anbindung ist ab P3 fertig, es fehlt nur die Datei.
- **Echte Sounddateien** — nach `Assets/_Project/Audio/Resources/` legen,
  Benennung steht in der dortigen `LIESMICH.txt`.
- **Etappe F (Online mit Freunden)** — bleibt bewusst vertagt, braucht ein
  Unity-Konto.
- **Etappe G (Charaktere)** — erst wenn sich die Fähigkeiten gut anfühlen.
