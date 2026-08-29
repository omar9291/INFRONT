# PROGRESS.md — Projektfortschritt

Diese Datei wird nach jeder Sitzung aktualisiert und zu Beginn jeder neuen
Sitzung ZUERST gelesen.

Letzte Aktualisierung: 2026-08-29

## Aktueller Stand

Phase 3 (Bot-Gegner) ist abgeschlossen und getestet.
Wartet auf Go fuer Phase 4 (Team-Deathmatch-Regeln).

## Was fertig ist

### Phase 0 - Grundgeruest
Diagnose, Engine-Wechsel Unreal->Unity, Speicher 5->15 GB, Projekt,
Ordnerstruktur, Git, Doku.

### Phase 1 - Bewegung
Pakete (URP, Netcode 2.13.2, Input System). Server-autoritativer Charakter:
Laufen/Sprinten/Springen. Schulterkamera. Arena per Code.

### Phase 2 - Schiessen & Schaden
Zielen hoch/runter. Health-Bauteil (server-geschriebene NetworkVariables).
Sturmgewehr als Hitscan, server-autoritativ, Magazin/Nachladen. Schussspur.
Tod + Respawn (ausblenden statt loeschen). Trainings-Dummy.

### Phase 3 - Bot-Gegner
- IAimSource: Spieler und Bot liefern der Waffe Ursprung/Richtung. Die
  Waffe (NetworkWeapon) hat jetzt einen server-Feuerpfad (ServerTryFire),
  den der Bot direkt nutzt - kein RPC an sich selbst.
- NavMeshBaker: baeckt die Arena beim Host-Start zur Laufzeit. Kein
  Handklick. Funktioniert headless.
- BotBrain (nur Server): Zustandsautomat Patrol -> Chase -> Combat -> Search.
  Sichtlinien-Wahrnehmung, auf 10 Pruefungen/Sekunde gedrosselt.
  Kurzzeitgedaechtnis (laeuft zur letzten bekannten Stelle). Zielt mit
  Streuung (AimSpread) und Reaktionszeit. Bewegung per NavMeshAgent.
- BotLifecycle: Tod/Respawn wie beim Spieler.
- BotStats-Asset: Schwierigkeitsstufen sind spaeter neue Assets, kein Code.
- Bot-Waffe schwaecher (12 statt 18 Schaden).
- Altlast aus Phase 1/2 erledigt: CharacterController auf Nicht-Server-
  Instanzen ist jetzt aus.
- Arena: 3 Bots, 1 Dummy, 4 SpawnPoints.

## Tests (headless PlayMode) - 13 von 13 gruen (2026-08-29)

    Unity -batchmode -runTests -testPlatform PlayMode -projectPath <PROJ>

Bewegung (2), Schaden/Waffe (6), Bots (5):
- NavMesh_wird_zur_Laufzeit_gebacken
- Bot_spawnt_und_steht_auf_dem_NavMesh
- Bot_entdeckt_und_verfolgt_den_Spieler
- Bot_schiesst_auf_den_Spieler
- Bot_stirbt_und_respawnt

Nicht automatisiert geprueft (auf diesem Mac nicht moeglich):
Aussehen, Kamera-/Schiessgefuehl, wie "clever" die Bots wirken, Framerate.

## Bekannte offene Probleme / Risiken

- Lag-Kompensation fehlt (bewusst, im Host-Modus irrelevant). Vorbereitet
  ueber clientRenderTime-Feld. Plan in NETCODE.md. Erst Stufe 3 noetig.
- Treffer-Kollision ist die Kapsel (CharacterController bzw. Body-Capsule),
  keine echten Hitboxen (Kopf/Koerper).
- Bots und Spieler sind noch teamlos - jeder trifft jeden. Teams kommen in
  Phase 4. Aktuell koennen Bots sich theoretisch gegenseitig treffen
  (Raycast unterscheidet nur "lebendes IDamageable"). In Phase 4 ueber
  Team-Pruefung ausschliessen.
- Bot-Wahrnehmung sucht nur echte Spieler (ConnectedClients), keine anderen
  Bots. Fuer Phase 4 (Bot-vs-Bot-Teams) erweitern.
- NavMesh wird bei jedem Host-Start neu gebacken (~0,3 s). Ok fuer die
  kleine Arena; bei grossen Karten spaeter cachen.
- Speicher: 11 GB frei, Projekt 1,7 GB.

## Naechster geplanter Schritt

Phase 4: Team-Deathmatch-Regeln.
Definition of Done (Vorschlag, vor Start bestaetigen):
- Zwei Teams. Spieler und Bots gehoeren einem Team an (w's einstellbar).
- Kein Freundschaftsbeschuss: Raycast und Bot-Wahrnehmung ignorieren das
  eigene Team.
- Punktezaehler pro Team als server-geschriebene NetworkVariable, +1 pro
  Abschuss.
- Rundenende bei Punktelimit oder Zeitlimit; danach Sieger anzeigen und
  Runde neu starten.
- Bots fuellen die Teams auf die gewaehlte Groesse auf.
- Minimal-HUD: Leben, Munition, Punktestand beider Teams.
- PlayMode-Tests: Abschuss gibt dem richtigen Team einen Punkt; kein
  Freundschaftsbeschuss; Runde endet bei Limit.

## Sitzungsprotokoll

### 2026-08-29 (Sitzung 1)
Phasen 0-3 komplett. Grundgeruest, server-autoritative Bewegung, Zielen,
Sturmgewehr mit Hitscan, Schaden/Tod/Respawn, Trainings-Dummy,
Bot-Gegner mit Zustandsautomat und NavMesh. 13 PlayMode-Tests gruen.
