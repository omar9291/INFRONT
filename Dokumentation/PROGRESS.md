# PROGRESS.md — Projektfortschritt

Diese Datei wird nach jeder Sitzung aktualisiert und zu Beginn jeder neuen
Sitzung ZUERST gelesen.

Letzte Aktualisierung: 2026-09-03

## GAMEPLAY-UEBERARBEITUNG - Etappe 2 "Wucht" (2026-09-03)

112 PlayMode-Tests gruen (+3 neu: WuchtTests). Mac-Build neu.

- **Geschoss-Zischen (`BulletWhiz.cs`, nur Spieler-Prefab)**: Der Server
  prueft bei jedem GEGNERISCHEN Schuss, ob die Kugel dicht (< 1,7 m) und
  weiter als 6 m vom Schuetzen am Spielerkopf vorbeifliegt, ohne zu treffen.
  Wenn ja: kurzes Zischen (2D) + Kamera-Zucken, per SendTo.Owner-RPC.
  Geometrie in `BulletWhiz.PassesNear` (getestet). Bots haben das Bauteil
  nicht - man hoert nur Kugeln, die an EINEM selbst vorbeigehen.
- **Ferner Schuss-Hall (`SoundId.SchussFern`)**: In `ShowFireEffectRpc` -
  ist der Schuss > 22 m von der Kamera weg, rollt zusaetzlich ein tiefes
  Grollen an, verzoegert um Entfernung/340 (Schallgeschwindigkeit). Neuer
  Parameter `AudioService.PlayAt(..., delay)`.
- **Staerkerer Kamera-Kick**: `NetworkPlayerController.AddRecoil` zittert
  jetzt nach Waffenwucht (`RecoilUp`) + kurzer Blickfeld-Stoss pro Schuss,
  beim Zielen deutlich gedaempft. Getroffen werden: Shake 0.32 -> 0.45.
- **Explosions-Taubheit (`BombExplosionFx`)**: nahe Explosion (`near > 0.12`)
  legt einen `AudioLowPassFilter` auf die Kamera (Cutoff 22 kHz -> 480 Hz)
  und spielt ein Ohren-Klingeln (`SoundId.OhrenPfeifen`). Erholt sich ueber
  ~3 s. Filter wird bei Szenenwechsel sicher zurueckgesetzt.
- Neue Platzhalter-Toene in `ProceduralSfx`: DistantBoom, Whiz, Ringing.
  Echte Dateien spaeter unter Audio/Resources/ eintauschbar
  (schuss_fern.wav, zischen.wav, ohren_pfeifen.wav).

### Die 5 Etappen
1. **Der Koerper** - FERTIG (Zielen, Scope, Ducken, Schleichen, Gewicht).
2. **Wucht** - FERTIG (Zischen, ferner Hall, Kamera-Kick, Taubheit).
3. **Die Welt lebt** - OFFEN. Ernste Beleuchtung mit Schatten, Dunst/Nebel
   pro Runde zufaellig, Staub, Umgebungston (Wind, fernes Gefecht,
   Artillerie), Karte entklotzen. Dafuer lade ich CC0-Assets (Liste an den
   Nutzer).
4. **Menschen** - OFFEN, auf Nutzer blockiert (Mixamo-FBX, Adobe-Login).
   Anbindung steht (`CharacterVisual`). Konto ist von keinem Browser hier
   erreichbar -> der Nutzer laedt die 5 FBX selbst herunter, ich hole sie
   aus dem Download-Ordner.
5. **Waffen** - OFFEN. Sturmgewehr + MP als echte CC0-Modelle.
   Menue nebenbei ernster.

## GAMEPLAY-UEBERARBEITUNG - Etappe 1 "Der Koerper" (2026-09-02)

Rueckmeldung eines Freundes: Menue gut, aber das Gameplay wirke wie ein
"totes Roblox-Spiel". Ziel des Nutzers: das Spiel soll sich echt nach Krieg
anfuehlen - realistische Waffen mit Zielfernrohr, buecken, intensiver Kampf,
gelegentlich Nebel, Menue ernster, Figuren wie echte Menschen. Grosser
Auftrag in 5 Etappen (siehe unten). Mixamo-Konto legt der Nutzer gerade an,
danach lade ich die Figuren.

Etappe 1 fertig (109 PlayMode-Tests gruen, +5 neu; Mac-Build neu):
- **Zielen ueber Kimme/Korn (rechte Maustaste)**: Bild zoomt leicht, Waffe
  vor die Blickmitte, Streuung stark reduziert (`WeaponStats.AdsSpreadMul`),
  langsameres Umsehen (`KeyboardMouseInputSource.SensitivityScale`),
  langsameres Gehen. Beim Sprinten zaehlt Zielen nicht (Exploit-Schutz).
- **Echtes Zielfernrohr** beim Scharfschuetzengewehr (`WeaponStats.ScopeZoom`
  = 4): schwarzes Rohr-Bild ueber dem ganzen Schirm (aus Code erzeugte
  Textur, kein Asset), 4x Zoom, Atem-Schwanken, Umschalt haelt die Luft an
  (begrenzt). Ohne Rohr streut die Waffe jetzt stark - das Rohr ist ein
  echter Vorteil. Neu: `ScopeOverlay.cs` (nur Besitzer, IMGUI).
- **Ducken (Strg)**: Kapsel + Augenhoehe + Trefferzonen sinken, langsamer,
  Schritte seltener, aufstehen nur mit Platz nach oben. Duck-Grad als
  NetworkVariable, Figur staucht sich (echte Duck-Animation kommt mit den
  Mixamo-Figuren in Etappe 4).
- **Schleichen (Alt)**: sehr langsam, dafuer fuer Gegner-Bots komplett
  unhoerbar (kein SoundEvents-Schritt).
- **Gewicht in der Bewegung**: man laeuft an und bremst ab
  (`_groundAccel/_groundDecel/_airAccel`) statt sofort auf Tempo zu sein;
  kurzer Blick-Ruck + Blickfeld-Stoss bei hartem Aufkommen.
- Menue: Steuerungs-Seite zeigt die neuen Tasten.

### Die 5 Etappen (Reihenfolge mit dem Nutzer abgestimmt)
1. **Der Koerper** - FERTIG (Zielen, Scope, Ducken, Schleichen, Gewicht).
2. **Wucht** - Schuss-Sound tiefer + Nachhall, staerkerer Kamera-Kick,
   Geschosse zischen am Kopf vorbei, Explosions-Taubheit.
3. **Die Welt lebt** - ernste Beleuchtung mit Schatten, Dunst/Nebel pro
   Runde zufaellig, Staub, Umgebungston (Wind, fernes Gefecht, Artillerie),
   Karte entklotzen. Dafuer lade ich CC0-Assets (Nutzer bekommt Liste).
4. **Menschen** - Mixamo-Figuren mit Ausruestung + Lauf-/Duck-/Ziel-/Sterbe-
   Animation. Nutzer laedt die FBX (Adobe-Login), ich binde sie an.
   Anbindung steht schon (`CharacterVisual`).
5. **Waffen** - Sturmgewehr + MP als echte CC0-Modelle statt Wuerfel.
   Menue nebenbei ernster (gedeckte Militaerfarben, weniger Leuchten).

## NACHT 8 (autonom, 2026-08-31 -> 2026-09-01)

Grosser Auftrag: "mach das Spiel 100 mal besser". Plan: Dokumentation/NACHTPLAN.md.
Reihenfolge P0..P11. Der Nutzer schlaeft; ein Stundenwecker setzt die Arbeit
nach einem evtl. Limit automatisch fort.

Fortschritt:
- [x] P0  Sicherung (INFRONT_Sicherung_vor_Nacht8/) + Ausgangstest 73/73
- [x] P1  Waffe in der Hand (ViewModel) - 76/76 gruen
- [x] P2  Der Look (Post-Processing + lesbare Karte) - 79/79 gruen
- [x] P3  Figur aus Code statt Kapsel - 82/82 gruen
- [x] P4  Faehigkeiten-Maschine + Rauch + Blendgranate - 86/87 (1 Testfix)
- [x] P5+P6  alle 6 Faehigkeiten + Bots nutzen sie - 89/89 gruen
- [x] P7  Gegner mit Kopf (hoeren, Ansagen, menschl. Zielen, Schwierigkeit) - 91/91
- [x] P8  Momente (Doppelkill/Ace/Clutch/Beste + Laufbahn) - 93/93
- [x] P9  Deko (procedural: Faesser, Lampen, Rohre, Sandsaecke, Himmel) - 93/93
- [x] P10 Ladebildschirm schoener (Muster, Leuchten, Punkte, Ecken, Untertitel)
- [x] P11 Abschluss + Morgenbericht (Dokumentation/MORGENBERICHT.md)

### Nachts allein entschieden (der Nutzer prueft das morgen)
- **TeamMatchTests.Freeze_Time...**: misst jetzt nur die WAAGRECHTE Bewegung
  (senkrechtes Nachsacken auf den Boden ist kein Fehler). Das war der seit
  Sitzung 7 wackelige Test - jetzt stabil.
- **ViewModel**: neue additive Ereignisse in NetworkWeapon (LocalFired,
  ReloadingChanged, WeaponSwitched) - nichts Bestehendes geaendert.
  FirstPersonCamera behaelt den Platzhalter-Wuerfel als Rueckfallebene
  (HandOffViewModel schaltet ihn ab, wenn das echte ViewModel laeuft).
- **HDR wieder AN** (GraphicsTune). Begruendung: es gibt jetzt Tonemapping.
  Falls die senkrechten Streifen auf dem M1 zurueckkommen -> im Menue
  "BILD: SCHLICHT" waehlen. Bitte morgen im Playtest gezielt darauf achten.
- **asmdef**: Runtime + Tests referenzieren jetzt zusaetzlich
  Unity.RenderPipelines.Core.Runtime (fuer die Volume-Typen).

## Aktueller Stand

Phase 5 (Spielbar machen) ist abgeschlossen. Die V1-Kernschleife ist
KOMPLETT: Startmenue -> Runde (First Person, 3v3 mit Bots, Punktestand,
Rundenende) -> zurueck ins Menue, mehrfach ohne Absturz.

Damit sind alle 5 Kern-Phasen aus SCOPE.md fertig. Was noch offen ist,
steht in SCOPE.md unter "Spaeter".

### Bisheriger Stand (vor Phase 5)

Phase 4 (Team-Deathmatch-Regeln) war abgeschlossen und getestet.
Damit ist die V1-Kernschleife komplett: bewegen -> zielen -> schiessen ->
treffen -> sterben -> respawnen, in einem 3-gegen-3 mit Bots, mit
Punktestand und Rundenende.

Wartet auf Go fuer Phase 5 (Spielbar machen: Menue -> Runde -> Ende ->
zurueck ins Menue, alles ohne Absturz durchspielbar).

## Was fertig ist

### Phase 0-3 (Kurzfassung)
Grundgeruest, server-autoritative Bewegung, Zielen, Sturmgewehr (Hitscan),
Schaden/Tod/Respawn, Trainings-Dummy, Bot-Gegner mit Zustandsautomat und
NavMesh.

### Phase 4 - Team-Deathmatch
- Team als einfache Zahl (Team.Alpha/Bravo). TeamMember-Bauteil an Spieler
  und Bot, Team als server-geschriebene NetworkVariable.
- Combatants: Verzeichnis aller Kaempfer. Bots holen ihre Gegner hier.
- Kein Freundschaftsbeschuss: Kugeln fliegen durch Verbuendete hindurch;
  Bots zielen nur auf das Gegner-Team.
- MatchManager: Punktestand/Phase/Endzeit als server-geschriebene
  NetworkVariables. +1 pro Abschuss ans Team des Schuetzen. Rundenende bei
  25 Punkten oder nach 8 Minuten. Danach Sieger + sauberer Neustart
  (Punkte zurueck, alle wiederbelebt und an Team-Spawns, Magazine voll).
- Restzeit wird nicht laufend gesendet - Server nennt die Endzeit einmal.
- MatchDirector: teilt den Spieler ins kleinere Team, fuellt beide Teams
  mit Bots auf 3 auf, erzeugt danach den MatchManager. (Loest BotSpawner ab.)
- MatchHud: PLATZHALTER. Reiner IMGUI-Text (Leben, Munition, Punkte,
  Restzeit, Sieger-Banner). Kein Grafik-/Schriftart-Asset. Das echte HUD
  kommt in "Spaeter - Stufe 4" mit der uebrigen Grafik. Siehe SCOPE.md.
- 4 Team-Spawns (2 pro Team, gegenueberliegende Enden).
- IDamageable: zweite ApplyDamage-Ueberladung mit Verursacher; die alte
  bleibt gueltig -> alle Alt-Tests unveraendert.

### Phase 5 - Spielbar machen
- GameFlow: eine Stelle fuer Netzwerk-Abbau + Szenenwechsel. Behebt das
  "zwei NetworkManager beim zweiten Durchlauf"-Problem.
- Startmenue (Menu.unity, kein Netzwerk): Runde starten, Beenden,
  Teamgroesse 2-5, Bot-Schwierigkeit Leicht/Normal/Schwer, Maus-
  Empfindlichkeit. Einstellungen per PlayerPrefs dauerhaft.
- Rundenende: "Sofort weiter" / "Zurueck zum Menue". Automatischer
  Weiterlauf nach 6 s bleibt.
- CursorController: Maus gefangen nur im laufenden Spiel.
- First-Person-Umbau (davor): FirstPersonCamera, eigener Koerper
  unsichtbar, Platzhalter-Waffe, Fadenkreuz, Trefferstrahl aus Augenmitte.
- Grafik-Fixes (davor): HDR aus, Adaptive Performance aus (war die Ursache
  der senkrechten Streifen auf dem M1), Aufloesung 1440x900, VSync an.

### Nach dem ersten Playtest (2026-08-29)
- FIX: Bots hatten gar keine Schussspur (in Phase 3 vergessen). Jetzt ja.
- FIX: Tote konnten noch schiessen - NetworkWeapon prueft jetzt auch, ob
  der Schuetze lebt.
- NEU: DamageFeedback (nur Besitzer) - roter Bildrand in Trefferrichtung
  (Server schickt Angreiferposition gezielt nur an den Getroffenen),
  Fadenkreuz blitzt beim eigenen Treffer, Todes-Blende (schwarz) mit
  Respawn-Countdown.
- NEU: Health-Bar im HUD (gruen/gelb/rot) statt nur Text.
- Offen fuer den Nutzer: Timing der Blenden und ob die roten Raender
  sich richtig anfuehlen - kann ich nicht selbst beurteilen.

### Rundenmodus (2026-08-30)
Auf Wunsch des Nutzers: weg vom Team-Deathmatch, hin zu Ausscheiden pro Runde.
- Wer stirbt, bleibt die Runde tot. Kein Respawn mitten in der Runde.
- Team ausgeloescht -> anderes Team gewinnt die Runde. Zeitablauf ->
  Team mit mehr Ueberlebenden.
- Bis 15 Rundensiege -> Match, dann neues Match.
- Zuschauen bei lebenden Verbuendeten waehrend man tot ist
  (Links/Rechtsklick wechselt). Kamera an fremde Augen ueber IAimSource.
- Spawn-Fix: 6 Punkte pro Team, verteilte Aufstellung ohne Doppelungen
  (war die Ursache von "man spawnt in den Gegnern").
- Todes-Blende blinzelt nur kurz, dann klar zum Zuschauen.

### Counter-Strike Gruppe A "Ueberblick" (2026-08-30)
- Kill-Feed oben rechts (Freund blau, Feind rot, blendet aus).
- Todesbildschirm: "Getoetet von Bravo-2".
- Punktetabelle auf Tab: beide Teams, Name/Abschuesse/Tode/lebt.
  Zaehlt ueber das ganze Match.
- Freeze-Time: 3 s Startsperre pro Runde, Countdown im Bild. Niemand
  laeuft oder schiesst. Test-Schalter SkipFreezeForTests.
- Wunschliste B (Schiessgefuehl) und C (Waffen/Kaufmenue/Bombe) in SCOPE.md.

### Counter-Strike Gruppe B "Schiessgefuehl" (2026-08-30)
- Trefferzonen Kopf/Koerper (eigene Physik-Ebene). Kopfschuss = ein Treffer
  toetet (WeaponStats.HeadshotMultiplier). Loest die Altlast "keine echten
  Hitboxen" aus Phase 2.
- Rueckstoss: festes lernbares Muster, zieht die Sicht hoch, geht erst
  nach dem Feuern zurueck.
- Streuung: rechnet der Server nach Bewegungszustand + Aufbau pro Schuss.
- Fadenkreuz geht mit der Streuung auf.
- Alle Werte im WeaponStats-Asset. Layer Hitbox(6)/Character(7).

### Counter-Strike Gruppe C.1 "mehrere Waffen" (2026-08-30)
- Zwei Plaetze: Primaer (1), Pistole (2). Jede Waffe eigene Munition,
  Wechselzeit. WeaponCatalog-Asset (Netz = nur Index).
- 4 Waffen: Sturmgewehr, Maschinenpistole, Scharfschuetzengewehr, Pistole.
- Alle Werte im jeweiligen WeaponStats-Asset.
- Als naechstes in Gruppe C: eine richtige Karte, dann Kaufmenue, dann Bombe.

### Counter-Strike Gruppe C.2 "richtige Karte" (2026-08-30)
- Spiegelsymmetrische Karte: 3 Bahnen, Mitte lange Sichtachse, Seiten eng
  mit Deckung, 2 erhoehte Platz-Bereiche mit Rampen (spaeter Bombenplaetze),
  Sichtschutz vor den Spawns, Aussenwaende.
- Karte per Code als Block-Tabelle mit Z-Spiegel-Helfer.
- Werkzeug MapSnapshot: 2D-Grundriss als Bild (Menue Infront/Karte).
- Test: Bots finden einen Weg Spawn->Spawn. ClearArena baeckt NavMesh flach.

### Counter-Strike Gruppe C.3 "Kaufmenue mit Geld" (2026-08-30)
- Wallet-Bauteil pro Kaempfer: Geld als server-geschriebene NetworkVariable.
  Start 800, Rundensieg 3000, Niederlage 1400 + 500 pro Serienrunde
  (Deckel 3400), Abschuss 300, Gesamtdeckel 16000.
- Kaufzeit = die verlaengerte Startsperre (3 -> 10 s). "Bereit" (Knopf im
  Menue) beendet sie sofort. RequestEndBuyTimeRpc.
- Jede Runde startet man NUR mit der Pistole. Wer stirbt, verliert
  Primaerwaffe und Weste fuer die naechste Runde (MatchManager merkt sich
  _diedThisRound); wer ueberlebt, behaelt beides.
- NetworkWeapon: _primaryIdx == -1 = keine Primaerwaffe. ServerSetPistolOnly
  / ServerSetPrimary / ServerEquipDefaultPrimary (Letzteres nur fuer Tests).
- PurchaseAgent: prueft Kaeufe server-autoritativ (nur Kaufzeit, nur
  lebendig, nur wenn Geld reicht). Spieler-Client per Rpc, Bot direkt.
- BuyMenuHud (nur Besitzer, IMGUI): B oeffnet/schliesst, oeffnet sich am
  Rundenanfang selbst. Ziffern 1-3 Waffen, 4 Weste, "Bereit".
- BotBuyer (nur Server): kauft zu Beginn der Kaufzeit die teuerste
  bezahlbare Waffe, danach die Weste, mit Zufalls-Verzoegerung und
  gelegentlichen Sparrunden.
- Schutzweste: Health._armor (0-100). Schluckt die Haelfte des ankommenden
  Koerperschadens und verbraucht sich dabei. Kopfschuss (ignoreArmor)
  geht vorbei. 1000 $.
- WeaponCatalog.BuyEntries (Netz = nur Index). Bot-Versionen von MP und
  Sniper an Index 5 und 6 - Index 0..4 unveraendert (stehen fuer spaeter
  in Speicherdaten).
- MatchHud zeigt Geld ($) und einen schmalen blauen Westen-Balken.

### Counter-Strike Gruppe C.4 "Bomben-Modus" - Etappe 1 (2026-08-30)
Modus im Startmenue waehlbar ("Ausscheiden" / "Bombe"), per PlayerPrefs
gespeichert (GameSettings.GameMode).

- MatchManager: RoundMode (Ausscheiden/Bombe), AttackingTeam/DefendingTeam
  als server-geschriebene NetworkVariables. Alpha greift an (Seitenwechsel
  zur Halbzeit kommt in Etappe 3). Rundenende rollenbasiert:
  - Bombe gelegt: nur wenn ALLE Verteidiger tot sind, gewinnen die
    Angreifer sofort; alle Angreifer tot -> Runde laeuft weiter.
  - Bombe nicht gelegt: alle Angreifer tot -> Verteidiger; alle
    Verteidiger tot -> Angreifer; Zeitablauf -> Verteidiger.
  - Bombe explodiert -> Angreifer; entschaerft -> Verteidiger.
- Bomb (ein Netzwerk-Objekt pro Match, vom MatchDirector erzeugt, im
  Ausscheide-Modus inaktiv und unter der Karte geparkt). Zustaende
  Inactive/Carried/Dropped/Planted. Nur der Server rechnet. Legen 3,2 s,
  Entschaerfen 10 s (5 s mit Kit), Zuender 40 s. Legen/Entschaerfen ueber
  Start-/Endzeitpunkt wie die Kaufzeit - unterbrochen faellt es auf 0.
  Traeger stirbt -> Bombe faellt an die Todesstelle, jeder lebende
  Angreifer hebt sie durch Drueberlaufen auf. Explosion: Schaden faellt
  von 500 auf 0 ueber 14 m, geht an der Weste vorbei.
- BombAction (an Spieler UND Bot): meldet die E-Taste (Kanten) an den
  Server, Bot-KI ruft ServerSetUsing direkt. Haelt auch "hat Kit".
- BombSite: Zone in der Szene (kein Netzwerk), Server fragt SiteAt() ab.
  Zwei Zonen A/B auf den erhoehten Plaetzen (rote Bodenmarkierung).
- Kaufmenue: Entschaerfungs-Kit (400 $, Taste 5), nur fuer Verteidiger im
  Bomben-Modus. Wie die Weste: bei Tod weg, beim Ueberleben behalten.
- BombHud (nur Besitzer): "E halten zum Legen/Entschaerfen" + Balken,
  "Du traegst die Bombe - zu Platz A oder B". Grosser roter Zuender-
  Countdown im MatchHud.
- IPlayerInputSource.UseHeld (E-Taste).
- Bots kaufen/kaempfen unveraendert - sie legen und entschaerfen noch
  NICHT. Das ist Etappe 2 (braucht Ziel-Verstaendnis in der KI).

### Counter-Strike Gruppe C.4 "Bomben-Modus" - Etappe 2 (2026-08-30)
Zwei Playtest-Befunde behoben + Bots verstehen das Ziel.

- ROLLEN-ANZEIGE (Playtest-Befund "man weiss nicht wer attacker ist"):
  MatchHud markiert im Bomben-Modus das eigene Team im Punktestand
  (>ALPHA<) und zeigt eine feste Rollenzeile (orange "ANGRIFF" / blau
  "VERTEIDIGUNG"); in der Kaufzeit zusaetzlich gross in der Mitte
  ("DU GREIFST AN" / "DU VERTEIDIGST"). BombHud sagt Angreifern ohne
  Bombe, ob ein Mitspieler sie traegt oder sie am Boden liegt.
- VORRUECKEN (Playtest-Befund "Teammate-Bots machen fast nichts"): der
  Patrouillen-Punkt jedes Bots wird beim Rundenstart 18 m entlang der
  Spawn-Blickrichtung (= Richtung Kartenmitte) verschoben. Vorher
  patrouillierte jedes Team nur in einer 12-m-Blase um den eigenen
  Spawn, die Sichtschutzwand bei z=+/-22 dazwischen - die Teams trafen
  sich nie, nur der Spieler stellte Kontakt her. Gilt in BEIDEN Modi.
  BotBrain.ServerAnchorForward(), aufgerufen aus MatchManager.PlaceTeam.
- BOT-ZIEL: neue Komponente BotObjective (Server, an jedem Bot).
  Setzt je nach Rolle BotBrain.ServerSetObjective(punkt) und
  BombAction.ServerSetUsing:
  - Angreifer-Traeger: zum naechsten Platz, dort STEHEN + E halten -> legt.
  - Andere Angreifer: laufen zur Bombe / zum Traeger (Begleitschutz).
  - Verteidiger vor dem Legen: Plaetze nach Team-Slot aufteilen + bewachen.
  - Verteidiger nach dem Legen: zur Bombe + in Reichweite E halten -> entschaerft.
  BotBrain mit aktivem Auftrag geht geradewegs zum Zielpunkt und bleibt
  dort stehen (kein Umherwandern, sonst reisst das Legen/Entschaerfen ab).
  Kampf schlaegt weiter alles: sieht der Bot einen Gegner, kaempft er wie
  bisher und kehrt danach zum Auftrag zurueck (nicht mehr zum Spawn).

### Counter-Strike Gruppe C.4 "Bomben-Modus" - Etappe 3 (2026-08-31)
Halbzeit, Geld-Boni, Explosions-Optik, Kill-Feed-Meldungen.

- HALBZEIT: _roundsToWin 15 -> 16, neues Feld _roundsPerHalf = 15. Match
  laeuft jetzt ueber max. 30 Runden, Sieg bei 16 - wie CS. (Nebenwirkung:
  auch der Ausscheide-Modus geht bis 16; dort kein Wechsel/Reset.)
  Neue NetworkVariable _roundsPlayed, in EndRound +1. Bei
  _roundsPlayed == _roundsPerHalf und Bomben-Modus: ServerHalfTime() -
  AttackingTeam auf das andere Team, alle Wallets auf _moneyStart,
  Niederlagen-Serien auf 0, _freshMatch = true (Runde 16 startet fuer alle
  nur mit Pistole). MatchHud zeigt in der Rundenende-Pause "HALBZEIT".
- GELD-BONI (MatchManager): Legen +300 an den Leger (ServerOnBombPlanted),
  Entschaerfen +300 an den Entschaerfer (ServerOnBombDefused). In
  AwardRoundMoney: verlieren die Angreifer trotz gelegter Bombe, bekommt
  jeder Angreifer zusaetzlich _moneyPlantedButLost = 800.
- KILL-FEED (MatchManager.BombEvent + BombEventReported + Rpc, wie
  KillReported): "X hat die Bombe gelegt" (orange), "X hat die Bombe
  entschaerft" (blau), "Die Bombe ist explodiert!" (rot). KillFeedHud
  zeichnet Entrys mit gesetztem Note-Feld als reinen Text.
- EXPLOSIONS-OPTIK: neue Komponente BombExplosionFx am Bomben-Prefab,
  per Bomb.ExplodedRpc(center) auf allen Clients ausgeloest. Wachsende,
  verblassende Feuerkugel (additiver URP/Unlit) + oranger Punktlicht-Blitz
  + Vollbild-Aufblitzen (OnGUI) + Kamera-Wackeln ueber die neue Methode
  FirstPersonCamera.Shake(amplitude, duration) - Staerke nach Entfernung
  zur Kamera. OPTIK SELBST UNGEPRUEFT (keine Screenshots moeglich); die
  Tests pruefen nur, dass die Ereignisse ausgeloest werden.

### Neues Menue + Ladebildschirm mit Unity UI Toolkit (2026-08-31)
Stil "Dark Tactical": fast schwarz, gedeckte Graustufen, Orange-Akzent.
Das echte UI-System von Unity 6 (UIElements), NICHT mehr IMGUI.

- UiTheme.cs: gemeinsame Farben + kleine Bausteine. Akzentfarbe in einer
  Zeile aenderbar (Wechsel auf Giftgruen: nur Accent + AccentBright).
- MainMenuUi.cs: neues Hauptmenue, kompletter Baum PER CODE gebaut (kein
  UXML - kann so nicht still beim Import kaputtgehen). Zwei Spalten: links
  Navigation (SPIELEN / STEUERUNG / BEENDEN), rechts Inhalt. Segment-
  Schalter fuer Modus / Teamgroesse / Schwierigkeit, grosser "RUNDE
  STARTEN"-Knopf, echte Tastenuebersicht (gab es vorher nicht),
  Beenden-Sicherheitsdialog. Hover ueber MouseEnter/MouseLeave (kein USS).
  Schreibt direkt in GameSettings + Save().
- LoadingOverlay.cs: Ladebildschirm am GameFlow-Objekt, ueberlebt den
  Szenenwechsel. INFRONT-Wortmarke, Fortschrittsbalken (echter
  Ladefortschritt via LoadSceneAsync.progress), Prozentzahl, wechselnde
  Tipps, wandernde Scan-Linie, Modus-Anzeige. Min. 1,5 s sichtbar, dann
  Ausblenden. Im Testlauf (batchmode) ohne Wartezeit/Fade.
- SceneBuilder: erzeugt Assets/_Project/UI/Resources/InfrontPanel.asset
  (PanelSettings, 1920x1080, ScaleWithScreenSize) + InfrontRuntimeTheme.tss
  (Standard-Laufzeit-Thema). Baut MenuUI (UIDocument + MainMenuUi) in die
  Menue-Szene. Liegt unter Resources, damit LoadingOverlay es per
  Resources.Load findet.
- RUECKFALLEBENE (zerstoerungsfrei): das alte IMGUI-Menue (MainMenu.cs)
  bleibt vollstaendig im Objektbaum. Neu: static bool MainMenu.Suppressed
  + Ausstieg in OnGUI. MainMenuUi setzt Suppressed=true; schlaegt der
  Aufbau fehl (kein Panel, Ausnahme), wird es zurueckgesetzt -> altes Menue
  erscheint statt schwarzem Bild. F10 im Menue schaltet jederzeit von Hand
  zurueck aufs alte Menue (F9 bleibt Screenshot).
- NICHT PRUEFBAR auf diesem Mac: wie es aussieht (Farben, Abstaende,
  Hover, ob der Slider mit dem Standard-Thema bedienbar ist). Die Tests
  pruefen nur Aufbau + dass Klicks in GameSettings landen.

## Sound-System (Masterplan Etappe A, Paket 1) (2026-08-31)

Erster Ton im Projekt ueberhaupt - vorher gab es kein einziges AudioSource.
Alles laeuft mit Platzhalter-Toenen aus Code; echte Dateien werden spaeter
einzeln eingetauscht, ohne dass Code angefasst werden muss.

- SoundId.cs: Aufzaehlung aller 24 Toene. Der Name ist zugleich der
  Datei-Name zum Austauschen (SchussGewehr -> schuss_gewehr).
- ProceduralSfx.cs: baut je einen Platzhalter-Clip per Code (Sinus,
  gefiltertes Rauschen, kurze Huellkurven). Klingt nach Prototyp, aber
  Ortung / Lautstaerke / Timing stimmen.
- AudioService.cs: die eine Stelle, die Toene abspielt. Auf dem
  GameFlow-Objekt (ueberlebt Szenenwechsel). Ring aus 16 wiederverwendeten
  3D-AudioSources + eine 2D-Quelle. Clip-Cache: erst Resources.Load einer
  echten Datei versuchen, sonst ProceduralSfx. Gesamtlautstaerke aus
  GameSettings.SfxVolume (neuer PlayerPrefs-Schluessel infront.sfxVolume,
  Regler auf der Steuerungsseite im Menue).
- SceneBuilder legt Assets/_Project/Audio/Resources/ + LIESMICH.txt an
  (Namensliste, Gratis-Quellen). Solange leer -> Platzhalter.
- Einhaengepunkte (alles server-autoritativ ausgeloest, Ton bei allen
  Clients):
  - Schuss: ShowFireEffectRpc, Ton pro Waffe (neues Feld WeaponStats.ShotSound).
  - Einschlag: am Trefferpunkt, Wand vs. Koerper.
  - Nachladen: Health-artige NetworkVariable _reloading.OnValueChanged.
  - Waffenwechsel: neuer SwitchEffectRpc (Slot-Wechsel + Kauf).
  - Schritte: neue Komponente FootstepSounds (Spieler + Bot). Lautstaerke
    nach Tempo aus der Positionsaenderung - Sprinten ist weithin hoerbar,
    eigene Schritte leiser. Teleport (Rundenstart) macht keinen Schritt.
  - Treffer / Kopftreffer / Abschuss / eigener Tod: neue Komponente
    CombatAudio (nur Besitzer, 2D). LocalHitConfirmed traegt jetzt einen
    bool (Kopftreffer).
  - Rundenstart / -sieg / -niederlage / Kaufzeit-Ende: neue Komponente
    MatchAudio auf dem HUD-Objekt. MatchManager hat dafuer neue Events
    RoundStarted / RoundEnded / BuyTimeEnded (per RPC an alle).
  - Bombe: Piepen (schneller werdend) in Bomb.LateUpdate, Legen /
    Entschaerfen ueber MatchAudio, Explosion in BombExplosionFx.
- Zerstoerungsfrei: fehlt eine Datei -> Platzhalter. Fehlt der
  AudioService -> die Aufrufer pruefen auf null, nichts bricht.
- NICHT PRUEFBAR auf diesem Mac: wie es klingt, ob die Ortung ueberzeugt,
  ob die Lautstaerke-Mischung passt. Die Tests pruefen nur, dass die
  richtigen Toene *angefordert* werden.

## Trefferrueckmeldung (Masterplan Etappe A, Paket 2) (2026-08-31)

Die Optik, die einen Schuss "treffen" laesst. Alles per Code, feste Pools,
zerstoerungsfrei ergaenzt.

- ShotFx.cs: kleine Struktur mit allem fuer die Schuss-Optik (Muendung,
  Auftreffpunkt, Flaechennormale, Trefferart 0/1/2). Der Server schickt
  sie per RPC an alle.
- NetworkWeapon: FireVisual traegt jetzt ein ShotFx (statt zwei Vektoren).
  Neu: static AnyShotFx (ein Abo fuer alle Waffen). LocalHitConfirmed
  traegt jetzt (Kopftreffer?, toedlich?).
- MuzzleFlash.cs (Spieler + Bot): kurzer Lichtblitz + helles Viereck am
  Lauf pro Schuss. Wiederverwendet, kein Erzeugen/Zerstoeren.
- ShellEjector.cs (Spieler + Bot): fliegende Patronenhuelsen, einfache
  Flugbahn per Hand (keine Physik-Engine), Ring aus 10 Wuerfeln.
- ImpactPool.cs (auf dem HUD-Objekt, hoert auf AnyShotFx): Wand ->
  Funkenstrahl + Einschussloch, das BLEIBT (Pool von 40, aelteste werden
  recycelt). Koerper -> kurzer roter Stoss, kein Loch.
- Kill-Bestaetigung (DamageFeedback, nur Besitzer): kraeftiges X ueber dem
  Fadenkreuz + kurzer Zoom (FirstPersonCamera.AddFovKick, neu) bei einem
  eigenen Abschuss.
- Kamera-Erschuetterung (FirstPersonCamera.Shake, war schon da): leicht
  pro Schuss (NetworkPlayerController.AddRecoil), kraeftig beim
  Getroffenwerden (DamageFeedback.OnDamageFrom).
- NICHT PRUEFBAR auf diesem Mac: wie Muendungsfeuer / Funken / Huelsen /
  Loecher / Ruckeln aussehen. Die Tests pruefen: Trefferart wird korrekt
  gemeldet (Wand vs. Koerper), toedliche Treffer als "lethal", der
  Loch-Pool recycelt statt zu wachsen.

## Tests (headless PlayMode) - 73 von 73 gruen (2026-08-31)

    Unity -batchmode -runTests -testPlatform PlayMode -projectPath <PROJ>

Bewegung (2), Schaden/Waffe (13), Bots (6), Teams (8), Kaufmenue (7),
Bomben-Modus (9), Bomben-Bots (3), Bomben-Wirtschaft (6), Menue+Ladeb. (7),
Sound (6), Trefferrueckmeldung (4), Zuschauen+Pause (2).
(TeamMatchTests.Freeze_Time_blockiert_Bewegung_am_Rundenstart wackelt
gelegentlich am Grenzwert 0.5 - laeuft beim zweiten Versuch durch.)

Zuschauen-/Pause-Tests (SpectatorPauseTests):
- Zuschauen_wechselt_zu_Gegnern_wenn_das_ganze_Team_tot_ist
- Solo_Pause_haelt_die_Rundenuhr_an

Trefferrueckmeldung-Tests (HitFeedbackTests):
- ImpactPool_recycelt_die_Einschlagloecher
- Schuss_auf_den_Koerper_meldet_Koerper_Einschlag
- Schuss_auf_ein_Hindernis_meldet_Wand_Einschlag
- Toedlicher_Treffer_wird_als_lethal_gemeldet

Sound-Tests (AudioTests):
- Jeder_Ton_hat_einen_Platzhalter
- Dateiname_folgt_der_Konvention
- Gesamtlautstaerke_null_macht_still
- Ton_wird_zwischengespeichert
- Schritt_Lautstaerke_haengt_am_Tempo
- Ein_Schuss_fordert_einen_Ton_an

Menue-/Ladebildschirm-Tests (MenuUiTests):
- Menue_baut_den_Baum_und_schaltet_das_alte_stumm
- Altes_Menue_bleibt_als_Rueckfallebene_erhalten
- Modus_Schalter_schreibt_und_speichert_GameSettings
- Teamgroesse_und_Schwierigkeit_landen_in_GameSettings
- Steuerungsseite_setzt_die_Empfindlichkeit
- Steuerungsseite_setzt_die_Lautstaerke
- Ladebildschirm_zeigt_Fortschritt_und_verschwindet

Bomben-Wirtschaft-Tests (BombEconomyTests):
- Halbzeit_wechselt_die_Seiten
- Halbzeit_setzt_das_Geld_auf_Start
- Legen_bringt_dem_Leger_Geld
- Angreifer_verlieren_trotz_Bombe_bekommen_Trostgeld
- Meldungen_Legen_und_Entschaerfen_erreichen_den_Kill_Feed
- Meldung_Explosion_erreicht_den_Kill_Feed

Kaufmenue-Tests (BuyMenuTests):
- Kauf_zieht_Geld_ab_und_gibt_die_Waffe
- Zu_wenig_Geld_kein_Kauf
- Kauf_nur_in_der_Kaufzeit
- Wer_stirbt_verliert_die_Primaerwaffe
- Wer_ueberlebt_behaelt_die_Primaerwaffe
- Rundensieg_gibt_mehr_Geld_als_Niederlage
- Weste_halbiert_den_Koerperschaden

Bomben-Modus-Tests (BombModeTests):
- Bombe_legen_dauert_die_volle_Zeit
- Ausserhalb_des_Platzes_kein_Legen
- Entschaerfen_gewinnt_die_Runde_fuer_die_Verteidiger
- Explosion_gewinnt_die_Runde_fuer_die_Angreifer
- Alle_Angreifer_tot_nach_dem_Legen_Runde_laeuft_weiter
- Alle_Angreifer_tot_vor_dem_Legen_Verteidiger_gewinnen
- Zeit_ablaeuft_ohne_Legen_Verteidiger_gewinnen
- Traeger_stirbt_Bombe_faellt_und_wird_aufgehoben
- Bombe_ist_im_Ausscheide_Modus_inaktiv

Bomben-Bot-Tests (BombBotTests):
- Patrouillen_Punkt_rueckt_zur_Kartenmitte_vor
- Angreifer_Bot_legt_die_Bombe_auf_dem_Platz
- Verteidiger_Bot_entschaerft_die_Bombe

Der Bewegungs- und der Bot-Schiesstest legen jetzt zuerst die anderen Bots
still, weil sonst das laufende Gefecht den Test stoert. Der MatchTestHarness
ruestet nach dem Laden jedem Kaempfer die Standardwaffe aus (das Spiel
startet jetzt mit der Pistole).

Nicht automatisiert geprueft (auf diesem Mac nicht moeglich):
Aussehen, HUD-Lesbarkeit, Kampf-/Rundengefuehl, Framerate mit 5 Bots.

## Playtest-Fixes (Phase 4.5, 2026-08-29)

Erster echter Playtest deckte auf (headless-Tests konnten das nicht sehen):
- BEHOBEN: Kamera-Rueckkopplung. Koerper drehte zur Laufrichtung, Kamera
  folgte dem Koerper -> Ruckeln + "vertauschte Tasten". Jetzt fuehrt die
  Maus die Kamera, der Koerper folgt der Blickrichtung.
- BEHOBEN: Build rendert in 1280x720 statt Retina 2880x1800
  (macRetinaSupport aus). VSync fix an, Zielbildrate 60.
- BEHOBEN: Schussspur unsichtbar (alter Nicht-URP-Shader) + Leistungsfresser
  (pro Schuss ein GameObject). Jetzt URP-Shader, wiederverwendete Linien.
- NEU: Esc-Pause mit Maus-Freigabe (Weiter / Beenden). Platzhalter-IMGUI.
- Tests: gemeinsamer MatchTestHarness, deterministischer Startzustand,
  Gefecht eingefroren. 5 volle Laeufe hintereinander gruen.
- OFFEN: "Linien, manche andersherum" - koennte zerrissenes Bild (jetzt
  VSync an) oder kaputte Schussspur (jetzt URP-Shader) gewesen sein.
  Muss der Nutzer im naechsten Build pruefen.

## Bekannte offene Probleme / Risiken

Stand 2026-08-30. Aeltere Eintraege, die inzwischen erledigt sind, wurden
entfernt (Input-Bug, Pause-Menue, echte Hitboxen - alles behoben).

### Bewusst vertagt (mit Plan)
- Lag-Kompensation fehlt. Im Host-Modus egal. clientRenderTime-Feld in
  NetworkWeapon ist der Einhaengepunkt. Plan in NETCODE.md.
- Kein echtes Online-Multiplayer. Server-autoritativ ist gebaut, aber nur
  Host-Modus getestet. Dedizierte Server / Matchmaking = Spaeter Stufe 3.
- Reservemunition: Nachladen ist unbegrenzt (auch nach dem Kaufmenue noch).
- "Bereit" beendet die Kaufzeit fuer ALLE sofort. Richtig, solange nur
  ein Mensch gegen Bots spielt. Bei echten Mitspielern spaeter: pro
  Spieler bereit, oder erst wenn alle bereit sind.
- Spieler, die mitten im Match dazukommen, haben 0 Geld bis zum naechsten
  Matchstart (MatchDirector.OnClientConnected). Nur Host-Modus, egal.
- Bomben-Modus Etappe 1 + 2 + 3 stehen. Offen:
  - Explosions-Optik ist gebaut, aber NICHT optisch geprueft (keine
    Screenshots auf diesem Mac). Nutzer muss Feuerkugel / Blitz / Wackeln
    im Spiel beurteilen.
  - Bei perfektem 15:15 nach 30 Runden laeuft das Match ohne weiteren
    Seitenwechsel weiter (kein Overtime-Regelwerk - seltener Sonderfall).
  - "E halten zum Legen" laesst den Spieler dabei noch frei laufen (kein
    Stillstand-Zwang). Fuer Platzhalter ok.

### Technische Schulden
- HUD (Leben/Munition/Punkte/Kill-Feed/Kaufmenue/Pause) ist weiter
  IMGUI-Platzhalter. Nur das Hauptmenue + der Ladebildschirm sind jetzt
  echtes UI Toolkit ("Dark Tactical"). Das restliche HUD kommt spaeter.
- MainMenuUi baut den Baum per Code statt per UXML/USS. Bewusst, damit
  nichts still beim Import kaputtgeht. Nachteil: Optik-Feinschliff nur im
  Code, kein Live-Vorschau-Editor. Kann spaeter auf UXML/USS umziehen.
- Neuer Test-Haken: MainMenu.Suppressed (static). Sauber, aber Menue-
  Umschaltung ueber globalen Zustand.
- Statischer Zustand: Combatants, SpawnService, MatchManager.Instance,
  BotBrain.GloballyFrozen. Funktioniert fuer eine Spielinstanz; global
  veraenderlicher Zustand bleibt ein Risiko (die Test-Flakiness kam daher).
- Test-Haken in Produktionsklassen: SuspendedForTests, SkipFreezeForTests,
  GloballyFrozen, ServerApplyTestConfig, Hitbox.Configure. Klar benannt,
  aber Testcode in Runtime-Dateien.
- Bot-KI: patrouillieren, sehen, verfolgen, schiessen, suchen, seit
  Etappe 2 ein Vorrueck-Punkt Richtung Mitte und (im Bomben-Modus) ein
  Rollen-Ziel ueber BotObjective. Weiter keine Deckungsnutzung, keine
  echte Absprache, kein Nachziehen zu einem umkaempften Platz als Gruppe.
- Waffen-Sichtmodell ist ein schwebender grauer Quader.
- Kein .inputactions-Asset, keine Tastenbelegung, kein Gamepad.
- Zuschauen waehlt per Listen-Index - kann springen, wenn Verbuendete sterben.

### Nicht messbar auf diesem Mac
- Bildrate. Der M1/8 GB ist nicht die Zielhardware, und Unity 6 + URP + Metal
  hat eigene Macken (die Streifen kamen von Adaptive Performance).
- Aussehen und Spielgefuehl - nur per F9-Screenshot und Nutzer-Rueckmeldung.
- Neues Menue + Ladebildschirm: Optik komplett ungeprueft. Offene Fragen
  fuer den Playtest: Sieht das "Dark Tactical" gut aus? Sind die Segment-
  Schalter gut bedienbar? Funktioniert der Empfindlichkeits-Slider mit dem
  Standard-Thema (sonst auf Preset-Knoepfe umbauen)? Blitzt der
  Ladebildschirm nur kurz auf oder passt die Dauer? Werden die Glyphen
  (Pfeil im Start-Knopf) angezeigt?
- Sound: wie es klingt, ob die 3D-Ortung ueberzeugt (Schritte/Schuesse
  aus einer Richtung), ob die Lautstaerke-Mischung stimmt. Die Tests
  pruefen nur, dass die richtigen Toene angefordert werden.
- Trefferrueckmeldung: wie Muendungsfeuer / Funken / Huelsen /
  Einschussloecher / Kill-X / Kamera-Ruckeln aussehen und ob die Staerke
  passt. Die Tests pruefen nur die Trefferart-Meldung + Pool-Recycling.

### Aus dem Original-Auftrag noch nicht gebaut (alles "Spaeter")
Granaten/Gadgets, Fahrzeuge, Sliden, Klettern, zerstoerbare Umgebung,
Battle Pass, Skins, Grafik, Animationen. Sound: Grundsystem steht
(Platzhalter-Toene), echte Dateien und Feinschliff spaeter.

## Naechster geplanter Schritt

Es gilt jetzt der **MASTERPLAN.md** (2026-08-31): INFRONT soll den
Valorant-Weg gehen (Faehigkeiten als Herzstueck), in klaren Etappen A-G.

**Etappe A "Wucht"** laeuft:
- [x] Paket 1: Sound-System (siehe oben).
- [x] Paket 2: Trefferrueckmeldung (Muendungsfeuer, Einschlagfunken +
  bleibende Loecher, Kill-Bestaetigung, Kamera-Erschuetterung, Huelsen).
- [ ] Paket 3: sichtbare Waffe in der Hand (ViewModel mit Bob/Sway,
  Rueckstoss-, Nachlade-, Wechsel-Animation). Aktuell zeigt
  FirstPersonCamera nur einen Platzhalter-Wuerfel.

Zuerst aber: **Playtest durch den Nutzer** -
- SOUND: Ein paar Runden spielen. Hoert man Schuesse / eigene Treffer /
  Schritte (auch die der Gegner aus einer Richtung)? Ist Sprinten laut?
  Bombe piept? Runde-Sieg/-Niederlage-Ton? Lautstaerke-Regler im Menue
  (Steuerung) - regelt er wirklich alles? Klingt es ertraeglich oder
  nervig? (Es sind Platzhalter - es MUSS noch nicht gut klingen, nur
  funktionieren.)
- TREFFER: Sieht man Muendungsfeuer beim Schiessen? Funken + bleibende
  Loecher an der Wand? Huelsen, die rausfliegen? Kommt bei einem eigenen
  Abschuss das X + der kurze Zoom? Ruckelt die Kamera beim
  Getroffenwerden (kraeftig) und beim Schiessen (leicht) - oder ist es
  zu viel / macht schwindelig?
- Noch offen aus Sitzung 6: Neues Menue + Ladebildschirm einmal
  durchklicken, F10-Rueckfallebene. Etappe-3-Explosions-Optik. Ein
  voller Durchlauf ueber die Halbzeit hinaus.

## Sitzungsprotokoll

### 2026-08-29 (Sitzung 1)
Phasen 0-4 komplett an einem Tag. Grundgeruest bis Team-Deathmatch mit
Bots, Punktestand und Rundenende. 17 PlayMode-Tests gruen. V1-Kernschleife
steht.

### 2026-08-30 (Sitzung 2)
Rundenmodus (Ausscheiden statt Deathmatch), Hit-Feedback, Team-Erkennung.
Dann Counter-Strike-Wunschliste: Gruppe A (Ueberblick), B (Schiessgefuehl),
C.1 (mehrere Waffen), C.2 (richtige Karte), C.3 (Kaufmenue mit Geld).
36 PlayMode-Tests gruen. Offen: Bomben-Modus.

### 2026-08-30 (Sitzung 3)
Bomben-Modus Etappe 1: Modus-Umschalter im Menue, Bombe legen/entschaerfen/
explodieren, Bombenzonen A/B, "E" halten, Bombe faellt beim Tod des Traegers,
Entschaerfungs-Kit im Kaufmenue. Bots kaempfen dabei noch wie bisher.
9 neue PlayMode-Tests, 45 von 45 gruen (3 saubere Laeufe). Mac-Build neu.
Offen: Etappe 2 (Bots spielen das Ziel), Etappe 3 (Halbzeit, Boni, Optik).

### 2026-08-30 (Sitzung 4)
Playtest von Etappe 1 brachte zwei Befunde: (1) Teammate-Bots taten fast
nichts, (2) man sah nicht, wer angreift. Ursache zu (1): jedes Team
patrouillierte nur in einer Blase um den eigenen Spawn, 50 m auseinander,
Sichtschutzwand dazwischen - sie trafen sich nie. Behoben durch einen
Vorrueck-Punkt Richtung Kartenmitte (gilt in beiden Modi). Dazu Etappe 2:
Rollen-Anzeige im HUD und die neue Komponente BotObjective (Bots legen,
begleiten, bewachen, entschaerfen von allein; Kampf hat Vorrang).
3 neue PlayMode-Tests, 48 von 48 gruen. Mac-Build neu.
Offen: Etappe 3 (Halbzeit-Seitenwechsel, Geld-Boni, Optik, Kill-Feed).

### 2026-08-31 (Sitzung 5)
Bomben-Modus Etappe 3: Halbzeit-Seitenwechsel (nach 15 Runden, Sieg bei
16 - wie CS), Geld auf Start zurueck. Geld-Boni: Legen +300,
Entschaerfen +300, Angreifer-Trostgeld +800 bei verlorener Runde trotz
gelegter Bombe. Kill-Feed-Meldungen fuer gelegt / entschaerft / explodiert.
Explosions-Optik: Feuerkugel + Lichtblitz + Bildschirm-Blitz +
Kamera-Wackeln (FirstPersonCamera.Shake) - Optik selbst ungeprueft.
6 neue PlayMode-Tests, 54 von 54 gruen. Mac-Build neu.
Damit ist Gruppe C komplett. Offen: Sound, dann "Spaeter"-Stufen.

### 2026-08-31 (Sitzung 6)
Neues Hauptmenue + Ladebildschirm mit Unity UI Toolkit ("Dark Tactical":
fast schwarz, Orange-Akzent). Erstes echtes UIElements-Stueck im Projekt -
alles Uebrige ist noch IMGUI. Menue per Code gebaut (kein UXML), Segment-
Schalter, Tastenuebersicht, Beenden-Dialog. Ladebildschirm ueberbrueckt
den Szenenwechsel mit echtem Fortschrittsbalken. Altes IMGUI-Menue bleibt
als Rueckfallebene (F10). 6 neue PlayMode-Tests, 60 von 60 gruen.
Mac-Build neu. Nebenbei: haengende INFRONT.app aus Sitzung 5 (Port 7777)
beendet - hatte den ersten Testlauf blockiert.

### 2026-08-31 (Sitzung 7)
Weltklasse-Plan aufgesetzt: MASTERPLAN.md. Entscheidung Valorant-Weg
(Faehigkeiten), Etappen A-G, jede Etappe fuer sich spielbar. Neue
CLAUDE.md-Regel: echte Umlaute ueberall (ae/oe/ue/ss nur noch in
Dateinamen / Code-Bezeichnern).
Dann Etappe A Paket 1: das komplette Sound-System (vorher gab es kein
einziges AudioSource im Projekt). 24 Toene als Code-Platzhalter, echte
Dateien spaeter einzeln eintauschbar (Assets/_Project/Audio/Resources/).
Schuss / Einschlag / Nachladen / Waffenwechsel / Schritte (nach Tempo) /
Treffer / Abschuss / eigener Tod / Rundenmeldungen / Bombe. Lautstaerke-
Regler im Menue. 6 neue Sound-Tests + 1 Menue-Test, 67 von 67 gruen.
Mac-Build neu. Nebenbei: haengende INFRONT.app aus Sitzung 6 (Port 7777)
beendet.
Dann Etappe A Paket 2: Trefferrueckmeldung. Muendungsfeuer + Huelsen
(Spieler + Bot), Einschlagfunken + bleibende Loecher (ImpactPool auf
AnyShotFx), Kill-X + kurzer Zoom (FovKick), Kamera-Ruckeln leicht beim
Schiessen / kraeftig beim Getroffenwerden. Neue ShotFx-Struktur.
4 neue Tests, 71 von 71 gruen. Mac-Build neu.
Optik selbst ungeprueft - wartet auf Playtest.

Playtest-Rueckmeldung Sitzung 7 - zwei Fehler behoben:
1. Nach dem Tod fror die Kamera ein, sobald kein Verbuendeter mehr lebte
   (NetworkPlayerController.UpdateSpectator: bisher "return" bei leerer
   Liste). Jetzt: kein Verbuendeter mehr am Leben -> man schaut lebenden
   GEGNERN zu (Anzeige "Gegner Bravo-2"); ist wirklich niemand mehr da,
   freie Maus-Sicht statt Standbild. Nebenbei: BotBrain.AimDirection
   folgt jetzt auch ausserhalb des Kampfes der Blickrichtung.
2. Esc hielt die Zeit nicht an. Jetzt echte Solo-Pause (nur wenn kein
   zweiter Spieler verbunden): Time.timeScale = 0 + BotBrain.GloballyFrozen
   + AudioListener.pause, und MatchManager.ServerEndSoloPause schiebt beim
   Fortsetzen Rundenuhr / Kaufzeit / Bombenzuender um die Pausendauer
   nach hinten (ServerTime laeuft bei timeScale 0 weiter). GameFlow.Go
   ruft PauseMenu.ForceResume, damit nichts in Zeitlupe in den
   Szenenwechsel laeuft.
2 neue Tests (SpectatorPauseTests), 73 von 73 gruen. Mac-Build neu.
Offen: Etappe A Paket 3 (ViewModel - sichtbare Waffe).

### 2026-09-01 (Nacht 8) - Etappe A Paket 3: Waffe in der Hand
Neu ViewModel.cs (nur Spieler-Prefab, nur Besitzer, wie DamageFeedback):
baut aus Code-Wuerfeln ein stilisiertes Gewehr bzw. eine kurze Pistole vor
der Kamera. Bewegung: Laufwippen (Bob, staerker beim Sprinten), Umsehen-
Nachschwingen (Sway), Rueckstoss-Ruck pro Schuss, Nachlade-Bewegung
(Waffe kippt weg, Magazin faellt und kommt zurueck), Ziehen beim
Waffenwechsel. Unsichtbar bei Tod / beim Zuschauen. Waffenform richtet
sich nach WeaponStats.SlotKind (Gewehr / Pistole).
NetworkWeapon: 3 neue Ereignisse (LocalFired, ReloadingChanged,
WeaponSwitched) - rein additiv. FirstPersonCamera: HandOffViewModel()
entfernt den Platzhalter-Wuerfel, sobald das echte ViewModel uebernimmt;
faellt sonst auf den Wuerfel zurueck. IsSpectating oeffentlich gemacht.
SceneBuilder: ViewModel ans Spieler-Prefab.
3 neue Tests (ViewModelTests), 76 von 76 gruen (auch der frueher wackelige
Freeze-Test). Optik/Gefuehl ungeprueft - wartet auf Playtest.

### 2026-09-01 (Nacht 8) - Etappe B: Der Look
Neu PostFxController.cs (Menue + Arena): baut zur Laufzeit ein globales
Post-Processing-Volume - ACES-Tonemapping, Bloom, Vignette, Farbanpassung
(mehr Kontrast, warmer Filter), Filmkorn, dazu Nebel in der Ferne. Alles
per Code, kein Volume-Profil-Asset.
GraphicsTune: HDR jetzt AN + HDR-Farbgraduierung (vorher aus - "kein
Tonemapping"; jetzt gibt es welches). Adaptive Performance bleibt aus
(war die echte Streifen-Ursache auf dem M1). SetupEverything ruft
GraphicsTune jetzt mit.
NEUE EINSTELLUNG "BILD: VOLL / SCHLICHT" im Menue (GameSettings.
GraphicsQuality, PlayerPrefs). "Schlicht" schaltet Volume + Nebel komplett
ab - die Rueckfallebene, falls die volle Optik irgendwo Streifen/Ruckeln
macht.
SceneBuilder: Arena- und Menue-Kamera mit renderPostProcessing + FXAA.
Karte lesbarer: dunkler kuehler Boden statt Weiss, leuchtende orange
Akzentstreifen auf Trennwaenden / Sichtschutz (Bloom laesst sie strahlen),
grosse A/B-Buchstaben aus leuchtenden Balken auf den Bombenplaetzen,
Punktlichter an Mitte / Lane-Luecken / beiden Plaetzen, Sonnenlicht leicht
gedimmt (Post-Processing hebt an).
3 neue Tests (LookTests), 79 von 79 gruen. Optik ungeprueft.

### 2026-09-01 (Nacht 8) - Etappe B Teil 2: Figur aus Code
Neu CharacterVisual.cs (Spieler + Bot): baut aus Code-Wuerfeln eine
stilisierte Figur (Huefte, Rumpf, Rucksack, Kopf mit Helm, zwei Arme,
zwei Beine). Beine/Arme pendeln aus der Laufgeschwindigkeit (aus der
Positionsaenderung - klappt bei Spieler und Bot, Server und Client),
Kopf neigt sich in die Zielrichtung, beim Tod kippt die Figur nach vorne.
Die alte Kapsel ("Body") bleibt als GameObject, nur ihr Renderer geht aus
(Rueckfallebene). Eigene Figur unsichtbar (IsLocalPlayer - NICHT IsOwner,
sonst waeren die Bots als "eigen" auch weg).
TeamTint: neue Methode RefreshRenderers() - sammelt die nachtraeglich
gebauten Figur-Teile ein und faerbt sie mit.
Mixamo-Anbindung: noch NICHT gebaut (kommt in P11-Notiz / spaeter). Die
Code-Figur ist erst mal die Loesung.
3 neue Tests (CharacterVisualTests), 82 von 82 gruen.

### 2026-09-01 (Nacht 8) - Etappe C: Faehigkeiten (die "Werkzeuge")
Die komplette Faehigkeiten-Maschine, aufgebaut wie das Waffen-System:
- AbilityStats (Asset) + AbilityCatalog (Asset, per SceneBuilder erzeugt)
  + AbilityKind/AbilitySlot-Enums.
- AbilityHolder (NetworkBehaviour, Spieler + Bot): 3 Plaetze Q/F/G,
  Ladungen pro Runde als NetworkVariables, Abklingzeit, server-autoritativ
  (RequestUseRpc -> Server entscheidet). ServerGrant beim Kauf,
  ServerClearLoadout bei frischer Runde, ServerRefreshCharges wenn man
  ueberlebt hat.
- AbilitySpawner + AbilityEffects: alle 6 Werkzeuge, alles per Code:
  * Rauchwand - waechst auf/haelt/loest sich auf. SmokeRegistry: die
    Bot-Sichtpruefung fragt "liegt Rauch zwischen Auge und Ziel?" -> Rauch
    blockiert die Bot-Sicht WIRKLICH (Segment-Kugel-Test, kein Collider).
  * Blendgranate - greller Blitz; wer in Reichweite + Sichtlinie + grob
    hinschaut wird geblendet. Spieler: weisser Bildschirm (AbilityHud).
    Bot: BotBrain.ServerBlind -> sieht nichts, schiesst nicht, weicht zurueck.
  * Splittergranate - kurzer Zuender, dann Flaechenschaden mit Abfall
    (15..90) nur bei freier Sicht. Explosions-Lichtblitz + Ton.
  * Scan-Puls - ScanRegistry markiert Gegner in Reichweite fuer X s.
    Spieler: gelber Kasten im HUD (auch durch Waende). Bot: BotBrain
    behandelt aufgeklaerte Gegner als sichtbar (ohne Sichtlinie/Winkel).
  * Brandwand - Reihe Feuer quer zur Blickrichtung, Schaden pro Sekunde,
    8 s. (Bots weichen noch NICHT aktiv aus - Notiz fuer P7.)
  * Stolperdraht - unsichtbare Linie, Gegner der durchlaeuft wird kurz
    geblendet (Alarm-Ton). Einmal pro Gegner.
- Kauf im BESTEHENDEN Kaufmenue (Tasten 6..0) mit dem BESTEHENDEN
  Geld-System (PurchaseAgent.ServerBuyAbility + RequestBuyAbilityRpc).
- Bots kaufen (BotBuyer) eine zufaellige Faehigkeit und zuenden sie
  (BotBrain.MaybeUseAbility): Blendgranate vor dem Sturm, Rauch auf dem
  Anmarsch.
- Eingabe: IPlayerInputSource.UseAbilitySlot (Q/F/G),
  KeyboardMouseInputSource + FakePlayerInput erweitert.
- HUD: AbilityHud (Q/F/G-Leiste mit Ladungen/Abklingzeit + Blitz-Bildschirm
  + Scan-Kaesten).
- Replikation der Effekt-Optik an weitere Clients: kommt mit Etappe F
  (Online). Aktuell Host-Modus - der Host sieht alles.
6 neue Tests (AbilityTests, AbilitySightTests), 89 von 89 gruen.
Optik/Gefuehl der Effekte ungeprueft.

### 2026-09-01 (Nacht 8) - Etappe D: Gegner mit Kopf
- Bots HOEREN: neu SoundEvents (Verzeichnis der letzten Geraeusche).
  NetworkWeapon meldet jeden Schuss (laut), NetworkPlayerController meldet
  Schritte (sprinten weithin, gehen leise). BotBrain.UpdatePerception:
  ohne Sichtkontakt -> SoundEvents.TryHear -> Verdachtspunkt, Zustand
  Search, geht nachschauen. Reichweite haengt an BotStats.Hearing.
- Bots SAGEN AN: neu MatchManager.CalloutReported (+RPC), KillFeedHud
  zeigt es. BotBrain.Callout (gedrosselt, an Teamwork-Stufe geknuepft):
  "Feind gesichtet!" beim Entdecken, "Hoere was!" bei Geraeusch,
  "Brauche Hilfe!" bei wenig Leben.
- MENSCHLICHES ZIELEN: FaceAndAim zieht die Blickrichtung jetzt mit
  BEGRENZTER Geschwindigkeit nach (BotStats.AimTrackSpeed), plus ein
  abklingender Zielfehler mit gelegentlichem Stoss (Ueberkorrektur /
  kurz daneben). Gefeuert wird erst, wenn die Richtung grob stimmt
  (AimIsOnTarget) - so wirkt sich das traege Nachziehen wirklich aus.
- AGGRESSIVITAET: BotStats.Aggression steuert den Wunschabstand im Kampf
  (defensiv = auf Abstand, aggressiv = ranpushen; zu nah -> Rueckzug).
- SCHWIERIGKEIT neu: Leicht/Normal/Schwer stellen jetzt Reaktion,
  Zielguete, Nachzieh-Tempo, Aggressivitaet, Hoervermoegen und Teamwork
  ein (SceneBuilder.LoadOrCreateBotStats).
2 neue Tests (BotSenseTests), 91 von 91 gruen. Alle alten Bot-Tests
weiter gruen.
NOCH OFFEN aus Etappe D (Notiz): Bots weichen Feuer/Brandwand noch nicht
aktiv aus; echte Deckungspunkte + Peek/Rueckzug + Flankier-Rollen sind
nur angedeutet (Aggression-Abstand), nicht voll ausgebaut.

### 2026-09-01 (Nacht 8) - Etappe E: Momente + Laufbahn
- HighlightTracker (am MatchManager-Prefab, nur Server): erkennt
  Doppelkill / Dreifachkill (2 bzw. 3 Abschuesse im 5-s-Fenster), Ace
  (alle Gegner allein, alle Abschuesse des Teams), Clutch (als Letzter
  gegen Ueberzahl die Runde gewonnen), Beste der Runde. Meldung ueber
  MatchManager.ServerReportHighlight (+RPC HighlightReported).
- HighlightBanner (Arena-HUD): grosses Banner in der Bildmitte + Ton.
  Zeigt eigene Momente immer, Ace/Clutch von anderen mit Namen.
- CareerStats (PlayerPrefs): Matches, Siege, Aces, laengste Siegesserie.
  RecordAce beim eigenen Ace, RecordMatch bei MatchManager.MatchEnded.
  Im Menue unter der Navigation sichtbar ("LAUFBAHN").
- MatchManager: neue Events MatchEnded + HighlightReported + BroadcastRpcs.
2 neue Tests (HighlightTests), 93 von 93 gruen.
NICHT gemacht (Notiz): der Rundenend-/Match-Endbildschirm ist noch das
IMGUI-Platzhaltermenue (MatchHud) - kein UI-Toolkit-Umbau. Kill-Feed und
HUD sind ebenfalls noch IMGUI. Das war als "wenn noetig" markiert; der
Fokus lag auf Gameplay-Substanz. Guter Kandidat fuer die naechste Sitzung.

### 2026-09-01 (Nacht 8) - P9 Deko + P10 Ladebildschirm
- SceneBuilder.BuildDecoration (unter Map/Deko, alles OHNE Collider -
  stoert NavMesh/Gameplay nicht): 10 Faesser mit Orange-Band, 9
  Haengelampen (leuchtend), Rohrleitungen an den 4 Aussenwaenden,
  Sandsack-Reihen vor beiden Spawns, 14 dunkle Boden-Flecken, 2 Eck-Masten.
- Dunkler prozeduraler Himmel: ArenaSky.mat (Skybox/Procedural, wenig
  Belichtung, blaugrauer Tint), Trilight-Umgebungslicht kuehl+dunkel.
- Sonnenlicht auf 1.0 gedimmt (Post-Processing hebt an).
- LoadingOverlay ausgebaut: driftendes Streifenmuster im Hintergrund,
  pulsierendes Leuchten hinter "INFRONT" (jetzt 42px), Untertitel
  "TAKTISCHER TEAM-SHOOTER", animierte Lade-Punkte, HUD-Eckklammern,
  Kartenname "ARENA" neben dem Modus, Tipp wechselt jetzt auch waehrend
  des Ladens alle 4,5 s. Alle Test-Haken unveraendert.
- ASSETS.md: begruendete Entscheidung, KEINE externen CC0-Pakete im
  autonomen Lauf einzubinden (Import headless zu fehleranfaellig) - Deko
  komplett per Code. Import-Pfad fuer spaeter dort notiert.
1 neuer Test-Check (Deko in LookTests), 93 von 93 gruen.

## NACHT 8 ABGESCHLOSSEN

Etappen A3, B, C, D, E gebaut + Deko + Ladebildschirm. 73 -> 93 Tests, alle
gruen. Mac-Build neu, Spiel gestartet. Morgenbericht: Dokumentation/
MORGENBERICHT.md. Alles Optische/Klangliche ist UNGEPRUEFT (harte Grenze
dieses Rechners) - der Playtest-Auftrag steht im Morgenbericht.

---

# HUD-UMBAU (2026-09-01, nach Playtest-Rueckmeldung "noch nicht schoen/cool")

Plan: Dokumentation/UMBAUPLAN-HUD.md. Auftrag: uebersichtlicher + Animation.
Nutzer-Entscheidung: ganzes HUD neu in UI Toolkit, am Stueck durchziehen.
Automatische Fortsetzung: Cron b21ba5f2 (stuendlich :23).

## U1 — Fundament + MatchHud/BombHud/Banner/Killfeed/Pause absorbiert

- UiTheme.cs additiv erweitert: HUD-Farben (Good/Warn/Bad/Money/Armor/Team),
  feste Schriftstufen FontXS..FontXL, HudBox(), HudLabel(), IgnorePickingTree().
- NEU HudController.cs: EIN UIDocument (sortingOrder 10), festes Zonen-Raster.
  Zeichnet Punktestand + Uhr + Rolle + NEU Lebende-Rauten + eine Statuszeile
  (Prioritaet: Bombe gelegt > Kaufzeit), unten links Leben/Weste/Geld-Kasten
  mit Geisterbalken + Rot-Blitz bei Schaden + hochzaehlendem Geld, unten
  rechts Munition (gross, rot < 25 %, Puls beim Schuss) + Waffenslots,
  unten Mitte Faehigkeiten Q/F/G als Kacheln (Ladungspunkte, Cooldown-Schleier,
  Blitz beim Einsatz), oben rechts Kill-Feed mit Zeilenhintergrund, Mitte
  Ereignis-Banner (rutscht rein), Bomben-Hinweis + Balken, Rundenende-Kasten
  mit echten Knoepfen, Pause-Overlay. Alles PickingMode.Ignore ausser den
  echten Knoepfen.
- MatchHud.cs GELOESCHT (komplett von HudController abgeloest).
- KillFeedHud.cs: nur noch Datenquelle (EntriesForHud, Instance), kein OnGUI.
- HighlightBanner.cs: meldet an HudController.ShowBanner, kein OnGUI.
  LastBannerForTests bleibt.
- BombHud.cs: fuettert HudController.SetBombPrompt pro Frame, kein OnGUI.
- AbilityHud.cs: nur noch Blitz-Bildschirm (Blendgranate) + Scan-Kaesten
  (IMGUI, muessen oben liegen). Leiste wanderte in HudController.
- PauseMenu.cs: nur noch Zustand + Solo-Pause-Logik, kein OnGUI.
  NEU SetPausedExternally() fuer den HUD-Knopf.
- SceneBuilder: HUD-GameObject bekommt HudController statt MatchHud.
- Noch IMGUI (spaetere Pakete): DamageFeedback (Fadenkreuz), FriendlyNameplates,
  Scoreboard (U3), BuyMenuHud (U4).
- Build OK, 93 von 93 Tests gruen.

## U3 — Punktetabelle (Tab)

- Scoreboard.cs GELOESCHT. In HudController absorbiert: Tab zeigt jetzt einen
  UI-Toolkit-Kasten mit zwei Spalten (ALPHA/BRAVO), Kopfzeile mit
  Team-Unterstrich, Zeilen "Name  K / T", Tote ausgegraut. Gleicher Panel-Stil
  wie Menue + restliches HUD.
- SceneBuilder: Scoreboard-Komponente entfernt.

## U4 — Kaufmenue neu

- BuyMenuHud.cs: nur noch Zustand + Tastatur + Kauf-Aktionen, kein OnGUI.
  NEU: BuyMenuHud.Local, ShouldShowMenu/ShouldShowHint, OwnsWeapon/OwnsArmor/
  OwnsKit/OwnsAbility, Money, Ready(). ServerBuy*-Logik unveraendert in
  PurchaseAgent -> BuyMenuTests bleiben gueltig.
- HudController: zweispaltiges Kaufmenue (links Waffen, rechts Ausruestung +
  Faehigkeiten). Jede Zeile: Tastenkuerzel im Kaestchen, Name, Preis bzw.
  "gekauft" (gruen), nicht Leistbares ausgegraut + nicht klickbar. Titel mit
  Geld + Restsekunden. "Bereit"-Knopf. Zeilen werden nur bei echter Aenderung
  neu gebaut (Signatur aus Geld-Stufe + Besitz). Hinweiszeile "[B] fuer
  Kaufmenue", wenn Kaufzeit laeuft aber Menue zu.
- Klick-Sicherheit: PickingMode.Ignore auf einem Eltern-Element haelt Kinder
  NICHT vom Klick ab; nur die echten Knoepfe fangen die Maus, die
  Vollbild-Zonen bleiben durchlaessig -> Schiessen bleibt moeglich.
- Build OK, 93 von 93 Tests gruen.

TEIL 1 (Uebersichtlichkeit) fertig: ganzes Spiel-HUD + Kaufmenue laufen jetzt
in einem UIDocument mit dem Menue-Design, festes Zonen-Raster, eine
Schrift-Hierarchie, nichts ueberdeckt sich mehr. Rest IMGUI: nur noch
Fadenkreuz (DamageFeedback) + Verbuendeten-Schilder (FriendlyNameplates) +
Blend-Blitz/Scan-Kaesten (AbilityHud) - alles Sachen, die absichtlich oben
liegen und nicht mit den Zonen kollidieren.

## TEIL 2 — Animation (A1-A4)

### A1 — HUD lebendig (alles in HudController)
- Geld zaehlt hoch/runter statt zu springen.
- Lebensbalken: Geisterbalken laeuft dem echten Wert nach; Fuellbalken blitzt
  weiss + der ganze Kasten ruckelt kurz + Rand blitzt rot bei Schaden
  (Erkennung intern ueber sinkende Lebenszahl, kein externer Aufruf).
- Munitionszahl pulst beim Schuss (sinkende Ammo), wird rot < 25 %, Accent
  waehrend Nachladen.
- Faehigkeitskachel: schrumpft + Rand blitzt Accent, wenn eine Ladung sinkt.
- Kill-Feed-Zeilen rutschen von rechts rein (erste ~0,18 s) und blenden weich aus.
- Ereignis-Banner rutscht von oben rein statt hartem Auftauchen.
- Ganzes HUD blendet beim Aufbau kurz ein (unten links/rechts von unten).

### A2 — Kampf-Wumms
- NEU CinematicMoments.cs am Arena-HUD: kurze Zeitlupe (Time.timeScale ~0.3)
  bei Ace, Clutch und Matchgewinn. NUR solo (ConnectedClients <= 1), NUR wenn
  kein Test laeuft (SuspendedForTests/SkipFreezeForTests), nie ueber die
  echte Solo-Pause; setzt sich immer selbst zurueck (Coroutine + OnDisable +
  OnDestroy).
- TracerEffect: dicker (0.07) + laenger sichtbar (0.12 s) + wird beim
  Verblassen duenner (AnimationCurve).

### A3 — Figur und Waffe
- ViewModel: Sprinthaltung - beim Sprinten (Input.Sprint + Tempo) kippt die
  Waffe schraeg nach unten/zur Seite, kein Nachladen in der Haltung.
- ViewModel: Landungs-Stauchung - beim Aufkommen nach einem Sprung sackt die
  Waffe kurz ab (aus VerticalVelocity-Sprung erkannt).
- CharacterVisual: die Figur kippt beim Tod jetzt in die Richtung, in die der
  Schuss sie schiebt (Kippachse aus Health.DiedWithInstigator; Fallback wie
  bisher nach vorne). LeaningForTests unveraendert.

### A4 — Uebergaenge
- MainMenuUi: beim Seitenwechsel blenden die Inhalte nacheinander von unten
  ein (schedule + UI-Toolkit-Transition, 45 ms Versatz pro Element).

### Neue Tests
- HudControllerTests.cs (5): HUD wird gebaut; Leben + Munition stehen richtig
  im HUD; Lebende-Rauten zaehlen bei einem Abschuss runter; Statuszeile zeigt
  die Kaufzeit; HUD faengt in der Bildmitte keinen Mausklick ab (panel.Pick).
- 93 -> 98 Tests, alle gruen.

## HUD-UMBAU ABGESCHLOSSEN

Teil 1 (uebersichtlicher) + Teil 2 (Animation) fertig. 98/98 Tests gruen.
Alles Optische/Klangliche bleibt UNGEPRUEFT (Grenze dieses Rechners).
Cron b21ba5f2 wird nach Mac-Build + Bericht geloescht.

## ECHTE ASSET-PAKETE (2026-09-01)

Auftrag: "jetzt vielleicht so Packs benutzen anstatt nur Code". Stil: realistisch.
Nacht-8-Entscheidung ("nur Code") auf Wunsch zurueckgenommen. Details:
BERICHT-ASSETS.md, UMBAUPLAN-ASSETS.md, ASSETS.md.

### P1 - Fundament
- AssetLibrary.cs: Resources.Load-Nachschlag fuer Models/Materials + Rueckfall-
  Zaehler (RealCount/FallbackCount). SpawnModel() fuer Deko.
- AssetImporterTools.cs (Editor): baut aus heruntergeladenen Texturordnern
  URP/Lit-Materialien, aus FBX Resources-Prefabs, aus HDRI eine Skybox.
- Neue Test-Assembly Infront.Tests.EditMode (AssetImportTests, 3 Tests):
  Material aus Texturordner, Normalmap-Markierung, sRGB-Flags.
- AssetFallbackTests (PlayMode): fehlendes Modell -> null + Rueckfall;
  Figur/Waffe bauen sich auch ohne Datei; echte Modelle sind maszstaeblich
  plausibel; echte Schuss-Sounds < 3 s.

### P2 - Flaechen-Texturen (ambientCG, CC0)
Concrete034/016, Asphalt031, Metal046A, PavingStones128 (1K JPG). SceneBuilder:
Block/Crate/Ground/Site-Platform nehmen echtes Textur-Material mit
pro-Objekt-Kachelung, sonst die bisherigen Farbtoene.

### P3 - HDRI-Himmel (Poly Haven, CC0)
industrial_sunset_02 (2K HDRI) -> Skybox/Panoramic-Material. Umgebungslicht
auf Skybox, Sonne + Ambient gedimmt. Sonst der prozedurale Himmel wie bisher.

### P4 - Deko-Modelle (Poly Haven, CC0)
Barrel_01, ammo_box, wooden_military_crate, metal_jerrycan_green,
modular_industrial_pipes_01, hanging_industrial_lamp, cement_bag (1K FBX).
BuildDecoModel: Material aus textures/-Unterordner, Collider raus,
Resources-Prefab. SceneBuilder-Deko (Barrel/Lamp/Pipe/Sandsack + neue Kisten)
nimmt die Modelle, sonst Grundkoerper. Alle Maszstaebe geprueft: 0.26-1.95 m.

### P5 - Waffen (Poly Haven, CC0) - 2 von 4
service_pistol -> waffe_pistole, bolt_action_rifle_7_62 -> waffe_sniper.
BuildWeaponModel: mehrere Material-Gruppen ueber Slot-Namen, fester Ziel-
Maszstab, CleanWeaponVariants entfernt _b-Varianten + lose Patronen + leere
Magazine. ViewModel.RefreshShape: bei Pistole/Sniper echtes Modell an
PoseFor()-Position, sonst die Wuerfel. Sturmgewehr/MP bleiben Wuerfel.
Haltungs-Zahlen zentral in ViewModel.PoseFor() (zum Nachjustieren).

### P6 - Schuss-Sounds (Free Firearm Sound Library, OpenGameArt, CC0)
AK-47/PPSh/Mosin-Nagant/Walther-PPQ -> schuss_gewehr/mp/sniper/pistole.wav.
Rohdateien 96kHz/24bit/stereo/bis 17 s -> auf Knall + 0.6 s zurechtgeschnitten
(proc_sfx.py), 44.1kHz/16bit/mono, je 58 KB. AudioService laedt sie automatisch
(Resources.Load), Rest bleibt ProceduralSfx.

### P7 - Figuren - VORBEREITET, auf Nutzer blockiert
CharacterVisual: laedt Resources/Models/figur mit Animator (Speed-Blend + Dead),
sonst die Wuerfel-Figur. AssetImporterTools.BuildFigureModel baut aus
Art/Figures/{basis,idle,walk,run,death}.fbx (Mixamo) Humanoid-Rig +
AnimatorController + Prefab. Braucht Adobe-Login -> nur der Nutzer.
UNGETESTET (FBX liegen noch nicht vor).

### Tests
98 -> 105 PlayMode (+7: AssetFallbackTests) + 3 EditMode. Alle gruen.
Download-Rohdaten: Art/ ~73 MB, Audio/ 0.2 MB. Alles CC0.

---

## UMZUG AUF GITHUB (2026-09-01)

Das Projekt liegt jetzt auf GitHub: **https://github.com/omar9291/INFRONT**
(öffentlich). Ab sofort wird nach jeder fertigen Arbeitseinheit committet und
hochgeladen.

Was eingerichtet wurde:
- SSH-Schlüssel auf dem Mac (`~/.ssh/id_ed25519`), öffentlicher Teil bei
  GitHub eingetragen. Kein Passwort mehr nötig.
- Git-Identität für dieses Repo auf die GitHub-Schutzadresse gesetzt, damit
  bei öffentlichen Commits keine echte Mail sichtbar ist.
- Kein Git LFS. Die vorbereiteten LFS-Regeln in `.gitattributes` sind
  auskommentiert (Projekt zu klein, größte Datei ~6 MB). Anleitung zum
  späteren Anschalten steht in der Datei.
- `.gitignore`: `scratch_*`-Dateien und die vom Testläufer erzeugten
  `InitTestScene*`-Szenen raus.
- README auf den echten Stand gebracht (First-Person, rundenbasiert).

Der ganze bis dahin ungesicherte Stand (HUD-Umbau + Asset-Umbau, zusammen
~430 geänderte Dateien) ging in zwei ehrlichen Commits hoch: erst Code/Doku/
Einstellungen, dann die ~74 MB Asset-Dateien. Ein frischer Klon wurde
gegengeprüft — 598 Dateien, Prüfsummen der großen Binärdateien identisch.

---

## HAUPTMENUE: STRUKTUR-UMBAU (2026-09-01)

Rückmeldung von Freunden: sieht gut aus, aber die Struktur stimmt nicht.
Ursache: das Menü warf drei verschiedene Arten von Sachen in denselben Topf
(Runden-Wahl, Dauer-Einstellungen, Nachschlage-Liste) und zeigte alle gleich
groß. `MainMenuUi.cs` neu geordnet:

- **Neue Seite EINSTELLUNGEN** — BILD (von SPIELEN weg), Maus-Empfindlichkeit
  und Lautstärke (von STEUERUNG weg). Alles, was man einmal einstellt, an
  einem Ort.
- **STEUERUNG** ist jetzt reine Tastenreferenz — Belegung als kleine
  Tastenkappen statt Fließtext.
- **SPIELEN** enthält nur noch die Runde: Spielmodus groß als zwei Karten mit
  Erklärzeile, Teamgröße + Bot-Stärke klein nebeneinander, Trennlinie,
  Zusammenfassungszeile ("BOMBE · 5 GEGEN 5 · BOTS NORMAL"), dann Startknopf.
- **Beenden** rutscht unter eine Trennlinie in der Navigation, gedämpft —
  kein gleichwertiger Reiter mehr. Die Beenden-Seite selbst unverändert.
- **Orange nur noch für den Startknopf.** Ausgewählte Segmente: heller Kasten
  + weiße Schrift + Akzent-Rand statt vollflächig orange. Beenden-Bestätigung
  jetzt rot (zerstörerisch), nicht orange.
- Inhaltsbreite auf 640 px begrenzt (vorher über den halben Monitor gezogen),
  Panel auf 780 px. Echte L-Ecke oben links statt kaum sichtbarem Strich.
  LAUFBAHN zeigt bei 0 Matches "Noch keine Runde gespielt" statt vier Nullen.
  Version kommt aus `Application.version` statt fest "V0.9".

Nichts gelöscht: alte IMGUI-Rückfallebene (F10), alle Element-Namen und die
Beenden-Seite bleiben. Tests angepasst (`nav-steuerung` → `nav-einstellungen`
für die Regler) + 1 neuer Test für die verschobene BILD-Einstellung.

**Ungeprüft:** wie es aussieht (Abstände, Kartenbreiten, Tastenkappen-Ausrichtung,
L-Ecke) — headless nicht sichtbar. Muss im Editor gegengeschaut werden.

### Tests
104 PlayMode, alle grün (war 103, +1 neuer Menü-Test).
