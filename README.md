# INFRONT

Rundenbasierter First-Person-Team-Shooter, gebaut mit Unity 6000.5.8f1.

Zwei Teams treten in Runden gegeneinander an, ein Team gewinnt eine Runde durch
Ausschalten des Gegners. Vorbilder: Counter-Strike, Valorant.

Solo-Entwicklungsprojekt. Studio: Driftlab.

## Stand

In Entwicklung. Aktueller Fortschritt: siehe `Dokumentation/PROGRESS.md`.

## Dokumentation

- `Dokumentation/SCOPE.md` — was Version 1 enthält und was nicht
- `Dokumentation/ARCHITECTURE.md` — technische Grundentscheidungen
- `Dokumentation/NETCODE.md` — Netzwerk-Architektur
- `Dokumentation/ASSETS.md` — verwendete Asset-Pakete und Lizenzen
- `Dokumentation/PROGRESS.md` — Fortschritt, nach jeder Sitzung aktualisiert

## Projekt öffnen

Unity Hub -> Add -> diesen Ordner wählen. Unity 6000.5.8f1 nötig.

## Netzwerk

Server-autoritativ über Netcode for GameObjects, vorerst nur Host-Modus
(ein Spieler hostet, andere verbinden sich). Details in `Dokumentation/NETCODE.md`.
