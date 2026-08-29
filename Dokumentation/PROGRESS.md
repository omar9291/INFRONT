# PROGRESS.md — Projektfortschritt

Diese Datei wird nach jeder Sitzung aktualisiert und zu Beginn jeder neuen
Sitzung ZUERST gelesen.

Letzte Aktualisierung: 2026-08-29

## Aktueller Stand

Phase 0 (Speicher & Grundgeruest) laeuft. Noch nicht abgeschlossen —
wartet auf zwei Entscheidungen des Nutzers (Render-Pipeline, Git-LFS).

## Was fertig ist

- Diagnose abgeschlossen: Unreal auf diesem Mac nicht machbar (Speicher/RAM),
  Wechsel zu Unity beschlossen, Spielkonzept unveraendert.
- Speicher freigeraeumt: von 5 GB auf 15 GB frei (Gruppe A: Installer-DMGs,
  Android-APKs, doppelter Mod-Ordner, Update-Reste, Browser-Caches).
- Unity-Projekt erstellt: /Users/user/UnityProjects/INFRONT, Version 6000.5.8f1.
- Ordnerstruktur angelegt (Assets/_Project/..., Assets/ThirdParty, Dokumentation).
- Doku-Dateien geschrieben: SCOPE.md, ARCHITECTURE.md, NETCODE.md, ASSETS.md,
  PROGRESS.md, CLAUDE.md.
- .metadata_never_index gesetzt (Spotlight raus).

## Woran gerade gearbeitet wird

Abschluss Phase 0: Git-Repo + .gitignore + .gitattributes, dann warten auf
Nutzer-Entscheidungen.

## Bekannte offene Probleme / Risiken

- Speicher bleibt knapp: 15 GB frei. Unity-Library + Builds + Shader-Cache
  wachsen mit der Zeit. Bei Bedarf Gruppe B (Spiele, ~6,6 GB) nachziehen.
- 8 GB RAM: der Editor wird bei einem 3D-Netzwerkprojekt spuerbar langsam
  werden (lange Starts, langes Kompilieren). Kein Blocker, aber Geduld noetig.
- Framerate-Ziel "60 FPS auf Mittelklasse-PC" ist auf diesem Mac nicht
  messbar. Wird durch sparsame Bauweise angestrebt, nicht durch Messung.
- Git LFS ist noch nicht installiert (kein Homebrew auf dem Rechner).

## Offene Entscheidungen (Nutzer)

1. Render-Pipeline: Built-in oder URP? (siehe ARCHITECTURE.md)
2. Git LFS: wie installieren? (siehe unten)

## Naechster geplanter Schritt

Nach Nutzer-Go und den zwei Entscheidungen: Phase 1 (Charakter bewegen).
Definition of Done Phase 1: Host startet, Third-Person-Charakter laeuft und
springt server-autoritativ, ein PlayMode-Test ist gruen.

## Sitzungsprotokoll

### 2026-08-29 (Sitzung 1)
Diagnose, Engine-Wechsel Unreal->Unity, Speicher freigeraeumt, Projekt
angelegt, Doku aufgesetzt, Phase 0 fast fertig.
