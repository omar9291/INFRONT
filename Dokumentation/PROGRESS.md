# PROGRESS.md — Projektfortschritt

Diese Datei wird nach jeder Sitzung aktualisiert und zu Beginn jeder neuen
Sitzung ZUERST gelesen.

Letzte Aktualisierung: 2026-08-29

## Aktueller Stand

Phase 4 (Team-Deathmatch-Regeln) ist abgeschlossen und getestet.
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

## Tests (headless PlayMode) - 17 von 17 gruen (2026-08-29)

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

## Bekannte offene Probleme / Risiken

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
