# PROGRESS.md — Projektfortschritt

Diese Datei wird nach jeder Sitzung aktualisiert und zu Beginn jeder neuen
Sitzung ZUERST gelesen.

Letzte Aktualisierung: 2026-08-29 (Phase 5)

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

## Tests (headless PlayMode) - 26 von 26 gruen (2026-08-29)

    Unity -batchmode -runTests -testPlatform PlayMode -projectPath <PROJ>

Bewegung (2), Schaden/Waffe (6), Bots (5), Teams (4):
- Spieler_und_Bots_haben_Teams
- Abschuss_gibt_dem_Schuetzen_Team_einen_Punkt
- Kein_Freundschaftsbeschuss_Kugel_fliegt_durch_Verbuendete
- Runde_endet_bei_Punktelimit_und_startet_neu

Der Bewegungs- und der Bot-Schiesstest legen jetzt zuerst die anderen Bots
still, weil sonst das laufende Gefecht den Test stoert.

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

- BEHOBEN 2026-08-29: Im ersten macOS-Build konnte man sich nicht bewegen.
  Ursache: activeInputHandler stand auf 0 (nur altes Input Manager), der
  Code nutzt aber das neue Input System. Jetzt auf 2 (beide). Lehre: die
  headless-Tests faelschen die Eingabe und pruefen den echten Geraete-Pfad
  nicht - ein echter Build/Playtest bleibt noetig.
- Cursor wird jetzt im Spiel gefangen (Maus-Look). Freigabe/Pause-Menue
  fehlt noch -> Phase 5. Zum Beenden Cmd+Q.

- HUD ist ein Platzhalter (s.o.). Nicht mit "fertig" verwechseln.
- Bildrate mit 5 Bots (3v3) auf dem M1 mit 8 GB ist ungemessen. Wenn es
  ruckelt: Teamgroesse in MatchDirector runter, Wahrnehmung ist schon
  gedrosselt.
- Lag-Kompensation fehlt (bewusst, im Host-Modus egal). Plan in NETCODE.md.
- Rundenneustart per Test (2 Runden am Stueck) gruen - aber nur headless
  geprueft, nicht optisch.
- Treffer weiterhin ueber die Kapsel, keine Kopf-/Koerper-Hitboxen.
- Combatants/SpawnService/MatchManager.Instance sind statisch. In den Tests
  sauber (TearDown schaltet alles ab), aber bei einem Editor-Domain-Reload-
  Wechsel im Blick behalten.
- Speicher: 11 GB frei, Projekt 1,7 GB.

## Naechster geplanter Schritt

Phase 5: Spielbar machen.
Definition of Done (Vorschlag, vor Start bestaetigen):
- Startmenue (Platzhalter-IMGUI): "Runde starten", "Beenden".
- Menue -> Arena -> Runde spielen -> Rundenende-Bildschirm -> zurueck ins
  Menue, ohne Absturz, mehrfach hintereinander.
- Host wird erst aus dem Menue gestartet, nicht mehr automatisch beim
  Szenenstart (MatchBootstrap/Auto-Host anpassen).
- Maus-Sichtbarkeit/-Sperre: im Menue frei, im Spiel gefangen.
- Ein "Neustart"- und ein "Zurueck zum Menue"-Weg, die alles sauber
  abbauen (NetworkManager.Shutdown, Szene neu laden).
- PlayMode-Test: kompletter Durchlauf Menue -> Runde -> Menue -> Runde.

## Sitzungsprotokoll

### 2026-08-29 (Sitzung 1)
Phasen 0-4 komplett an einem Tag. Grundgeruest bis Team-Deathmatch mit
Bots, Punktestand und Rundenende. 17 PlayMode-Tests gruen. V1-Kernschleife
steht.
