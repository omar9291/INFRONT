# PROGRESS.md — Projektfortschritt

Diese Datei wird nach jeder Sitzung aktualisiert und zu Beginn jeder neuen
Sitzung ZUERST gelesen.

Letzte Aktualisierung: 2026-08-29

## Aktueller Stand

Phase 2 (Schiessen & Schaden) ist abgeschlossen und getestet.
Wartet auf Go fuer Phase 3 (Bot-Gegner).

## Was fertig ist

### Phase 0 - Grundgeruest
Diagnose, Engine-Wechsel Unreal->Unity, Speicher 5->15 GB, Unity-Projekt,
Ordnerstruktur, Git-Repo, Doku.

### Phase 1 - Bewegung
Pakete (URP, Netcode 2.13.2, Input System). Server-autoritativer Charakter:
Laufen/Sprinten/Springen. Schulterkamera. Arena per Code. 2 Tests gruen.

### Phase 2 - Schiessen & Schaden
- Zielen hoch/runter: Maus Y neigt den Ziel-Drehpunkt, server-autoritativ,
  Pitch als NetworkVariable an andere Clients. Kamera neigt mit.
- Health-Bauteil: Leben + Lebendig-Status als server-geschriebene
  NetworkVariables. IDamageable-Schnittstelle (Spieler, Dummy, spaeter Bots).
- Sturmgewehr (NetworkWeapon): Hitscan. Client fragt an, Server prueft
  Feuerrate/Munition/Nachladen und macht den Raycast. Munition als
  NetworkVariable. Schussspur (TracerEffect) per ClientRpc.
  Kennwerte in Sturmgewehr.asset (Balance ohne Code).
- Tod + Respawn: ausblenden/stillstellen statt loeschen; Server teleportiert
  nach Wartezeit zum SpawnPoint und setzt Leben zurueck. Gleicher Ablauf
  fuer den stehenden Trainings-Dummy.
- SpawnService kennt alle SpawnPoints (4 in der Arena). DummySpawner
  erzeugt 3 Dummies beim Host-Start.

## Tests (headless PlayMode) - 8 von 8 gruen (2026-08-29)

    Unity -batchmode -runTests -testPlatform PlayMode -projectPath <PROJ>

- Spieler_spawnt_im_Host_Modus
- Spieler_laeuft_auf_Vorwaerts_Eingabe_nach_vorne
- Server_Schaden_senkt_Leben_des_Dummys
- Dummy_stirbt_bei_null_Leben_und_respawnt
- Schuss_auf_Dummy_macht_Schaden
- Feuerrate_begrenzt_die_Schussanzahl
- Spieler_stirbt_und_respawnt_mit_vollem_Leben
- Waffe_startet_mit_vollem_Magazin

Nicht automatisiert geprueft (auf diesem Mac nicht moeglich):
Aussehen, Kamera-Gefuehl, Schiessgefuehl, Framerate.

## Bekannte offene Probleme / Risiken

- Lag-Kompensation fehlt (bewusst). Vorbereitet ueber clientRenderTime-Feld
  in FireRpc. Details + Plan in NETCODE.md. Erst Stufe 3 noetig.
- CharacterController auf Nicht-Server-Instanzen noch aktiv. Muss in Phase 3
  abgeschaltet werden, sobald echte Remote-Clients dazukommen.
- Waffe uebernimmt die Zielrichtung des Servers (aus dem Eingabe-Strom),
  nicht eine mitgesendete Richtung. Sauber server-autoritativ, aber bei
  Paketverlust koennte ein Schuss "schief" wirken. Mit Lag-Kompensation
  spaeter zu haerten.
- Treffer-Kollision ist die CharacterController-Kapsel, keine echten
  Hitboxen (Kopf/Koerper). Reicht fuer jetzt.
- Speicher: 11 GB frei, Projekt 1,7 GB. Weiter beobachten.

## Naechster geplanter Schritt

Phase 3: Bot-Gegner.
Definition of Done (Vorschlag, vor Start bestaetigen):
- NavMesh wird per Editor-Code aus der Arena gebacken.
- Ein Bot-Typ: patrouilliert, entdeckt den Spieler per Sichtlinie, verfolgt,
  schiesst mit derselben NetworkWeapon-Logik, nutzt Deckung grob.
- Bots existieren nur auf dem Server; NGO verteilt sie.
- Bots benutzen Health/IDamageable/PlayerLifecycle-Muster (ausblenden +
  Respawn), damit Team Deathmatch spaeter zaehlen kann.
- Schwierigkeit ueber WeaponStats + Reaktionszeit + Zielgenauigkeit
  einstellbar (mehrere Stufen kommen aber erst spaeter).
- PlayMode-Tests: Bot findet und verfolgt den Spieler; Bot-Schuss trifft;
  Bot stirbt und respawnt.

## Sitzungsprotokoll

### 2026-08-29 (Sitzung 1)
Phase 0, 1 und 2 komplett. Grundgeruest, server-autoritative Bewegung,
Zielen, Sturmgewehr mit Hitscan, Schaden/Tod/Respawn, Trainings-Dummy.
8 PlayMode-Tests gruen.
