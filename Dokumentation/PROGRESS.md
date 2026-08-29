# PROGRESS.md — Projektfortschritt

Diese Datei wird nach jeder Sitzung aktualisiert und zu Beginn jeder neuen
Sitzung ZUERST gelesen.

Letzte Aktualisierung: 2026-08-29

## Aktueller Stand

Phase 0 (Speicher & Grundgeruest) ist abgeschlossen, bis auf zwei
Entscheidungen des Nutzers (Render-Pipeline, Git-LFS-Installation).
Wartet auf Go fuer Phase 1.

## Was fertig ist

- Diagnose: Unreal auf diesem Mac nicht machbar (5 GB frei, 8 GB RAM,
  kein Xcode). Wechsel zu Unity beschlossen, Spielkonzept unveraendert.
- Speicher freigeraeumt: 5 GB -> 15 GB frei (Installer-DMGs, Android-APKs,
  doppelter Mod-Ordner, Update-Reste, Browser-/App-Caches).
- Unity-Projekt: /Users/user/UnityProjects/INFRONT, Version 6000.5.8f1.
  Startet headless fehlerfrei (Exit 0, keine Compiler-Fehler).
- Ordnerstruktur: Assets/_Project/{Art,Audio,Code/{Runtime,Editor,Tests},
  Prefabs,Scenes,Settings}, Assets/ThirdParty, Dokumentation.
- Git-Repo initialisiert, erster Commit (da36b15).
- .gitignore (Unity), .gitattributes (LFS-Muster fuer Binaerdateien),
  .metadata_never_index gesetzt.
- Doku: SCOPE, ARCHITECTURE, NETCODE, ASSETS, PROGRESS, CLAUDE.md, README.

## Bekannte offene Probleme / Risiken

- Speicher knapp: 15 GB frei. Unity-Library, Builds und Shader-Cache
  wachsen mit der Zeit. Bei Bedarf Gruppe B (Spiele, ~6,6 GB) nachziehen.
- 8 GB RAM: Editor wird bei 3D-Netzwerkprojekt spuerbar langsam
  (lange Starts, langes Kompilieren). Kein Blocker, Geduld noetig.
- Framerate-Ziel "60 FPS Mittelklasse-PC" auf diesem Mac nicht messbar.
  Nur durch sparsame Bauweise anzustreben.
- Git LFS noch nicht installiert (kein Homebrew). .gitattributes ist
  vorbereitet; Binaer-Assets kommen erst in "Spaeter-Stufe 4".

## Offene Entscheidungen (Nutzer)

1. Render-Pipeline: Built-in oder URP? (Details in ARCHITECTURE.md)
   Empfehlung: URP, jetzt umstellen solange das Projekt leer ist.
2. Git LFS: jetzt installieren (Homebrew noetig) oder verschieben bis
   Grafik-Assets dazukommen? Empfehlung: verschieben.

## Naechster geplanter Schritt

Phase 1: Charakter bewegen.
Definition of Done: Host startet, Third-Person-Charakter laeuft/sprintet/
springt server-autoritativ, Schulterkamera folgt, ein PlayMode-Test gruen.

## Sitzungsprotokoll

### 2026-08-29 (Sitzung 1)
Diagnose, Engine-Wechsel Unreal->Unity, Speicher 5->15 GB, Unity-Projekt
angelegt, Ordnerstruktur + komplette Doku, Git-Repo mit erstem Commit.
Phase 0 abgeschlossen bis auf zwei offene Entscheidungen.
