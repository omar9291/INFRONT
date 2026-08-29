# PROGRESS.md — Projektfortschritt

Diese Datei wird nach jeder Sitzung aktualisiert und zu Beginn jeder neuen
Sitzung ZUERST gelesen.

Letzte Aktualisierung: 2026-08-29

## Aktueller Stand

Phase 1 (URP-Setup + Charakter bewegen) ist abgeschlossen und getestet.
Wartet auf Go fuer Phase 2 (Schiessen & Schaden).

## Was fertig ist

### Phase 0
- Diagnose, Engine-Wechsel Unreal -> Unity, Speicher 5 -> 15 GB frei.
- Unity-Projekt /Users/user/UnityProjects/INFRONT, 6000.5.8f1.
- Ordnerstruktur, Git-Repo, Doku (SCOPE/ARCHITECTURE/NETCODE/ASSETS/CLAUDE).

### Phase 1
- Pakete installiert: URP 17.6.0, Netcode for GameObjects 2.13.2,
  Input System 1.20.0, AI Navigation 2.0.14, Test Framework 1.7.0.
- Render-Pipeline: URP eingerichtet (PC_RenderPipeline + PC_UniversalRenderer
  in Assets/_Project/Settings/), als Standard in Graphics-/Quality-Settings.
- Assemblies: Infront.Runtime, Infront.Editor, Infront.Tests.PlayMode.
- Server-autoritativer Charakter (NetworkPlayerController):
  - Client schickt Eingabe-Kommandos, nur der Server bewegt den
    CharacterController und wendet Schwerkraft/Sprung an.
  - NetworkTransform server-autoritativ verteilt die Position.
  - Laufen, Sprinten (Shift), Springen (Space), Drehen (Maus).
- Schulterkamera (ShoulderCamera): folgt dem lokalen Charakter.
- MatchBootstrap: startet in Phase 1 automatisch den Host.
- Editor-Skripte: UrpSetup (URP), SceneBuilder (Prefab + Arena komplett
  per Code). Menue "Infront/Setup/...". Nichts von Hand gebaut.
- Arena.unity: Boden 60x60 m, 12 Kisten, Licht, NetworkManager, SpawnPoint,
  Kamera. Einzige Build-Szene.

## Tests (headless, PlayMode)

    Unity -batchmode -runTests -testPlatform PlayMode -projectPath <PROJ>

- Spieler_spawnt_im_Host_Modus: PASS
- Spieler_laeuft_auf_Vorwaerts_Eingabe_nach_vorne: PASS
  (2 von 2 gruen, Stand 2026-08-29)

Nicht automatisiert geprueft (auf diesem Mac nicht moeglich):
- Aussehen der Szene, Kamera-Gefuehl, Steuerungs-Feeling.
- Framerate. URP ist bewusst schlank aufgesetzt, aber ungemessen.

## Bekannte offene Probleme / Risiken

- Speicher: 11 GB frei (Projekt 1,7 GB). Weiter beobachten; bei Bedarf
  Gruppe B (Spiele, ~6,6 GB) nachziehen.
- 8 GB RAM: Editor-Starts dauern ~1-2 min headless. Kein Blocker.
- Auto-generierte Assets liegen in Assets/ (DefaultNetworkPrefabs,
  DefaultVolumeProfile, UniversalRenderPipelineGlobalSettings). Standard
  bei URP/NGO. Verschieben ist riskant (Referenzen), daher belassen.
- Sprung wird verworfen, wenn er eintrifft waehrend der Spieler in der
  Luft ist (kein Input-Buffering). Ok fuer Phase 1, spaeter verbessern.
- Kamera folgt der Charakter-Drehung. Fuer einen Shooter muss spaeter die
  Maus die Kamera fuehren und der Charakter sich zur Kamera drehen.

## Naechster geplanter Schritt

Phase 2: Schiessen & Schaden.
Definition of Done (Vorschlag, vor Start bestaetigen lassen):
- Sturmgewehr: Client sendet Schuss-Anfrage, Server macht den Raycast
  (server-autoritativ), Magazin + Nachladen.
- Leben als NetworkVariable, nur Server schreibt.
- Tod + Respawn nach kurzer Wartezeit am SpawnPoint.
- Ein zweites Ziel zum Draufschiessen (stehender Dummy mit Leben).
- PlayMode-Test: Server-Schuss auf Dummy senkt dessen Leben; bei 0 stirbt er.

## Sitzungsprotokoll

### 2026-08-29 (Sitzung 1)
Diagnose, Engine-Wechsel, Speicher, Projekt-Grundgeruest (Phase 0).
Danach Phase 1 komplett: Pakete, URP, server-autoritativer Charakter,
Kamera, Arena per Code, 2 PlayMode-Tests gruen.
