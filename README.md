# INFRONT

**Rundenbasierter First-Person-Team-Shooter**, gebaut mit Unity 6000.5.8f1.
Vorbilder: Counter-Strike, Valorant. Solo-Entwicklungsprojekt vom Studio **Driftlab**.

Zwei Teams treten in kurzen Runden gegeneinander an. Eine Runde ist gewonnen,
wenn das gegnerische Team ausgeschaltet ist (Modus *Ausscheiden*) oder die Bombe
gelegt / entschärft wurde (Modus *Bombe*). Aktuell: ein Spieler gegen Bots im
Host-Modus.

## Spielen (Download)

Fertige, startbare Versionen liegen unter **[Releases](../../releases)**.

| Plattform | Datei | Start |
|-----------|-------|-------|
| macOS     | `INFRONT-mac-vX.Y.zip`     | entpacken, **Rechtsklick auf `INFRONT.app` → Öffnen** (nicht doppelklicken — die App ist nicht signiert) |
| Windows   | `INFRONT-windows-vX.Y.zip` | entpacken, `INFRONT.exe` starten, bei der SmartScreen-Meldung *Weitere Informationen → Trotzdem ausführen* |

Es ist keine Installation nötig, die App läuft direkt aus dem entpackten Ordner.

## Steuerung

| Taste | Aktion |
|-------|--------|
| W A S D | Bewegen |
| Maus | Umsehen / Zielen |
| Linke Maustaste | Schießen |
| R | Nachladen |
| Leertaste | Springen |
| Umschalt (halten) | Sprinten |
| 1 / 2 | Waffe wechseln |
| E (halten) | Bombe legen / entschärfen |
| B | Kaufmenü |
| Tab (halten) | Punktetabelle |
| Esc | Pause |

## Stand

In Entwicklung. Aktueller Fortschritt: siehe [`Dokumentation/PROGRESS.md`](Dokumentation/PROGRESS.md).

## Für Entwickler

- **Projekt öffnen:** Unity Hub → Add → diesen Ordner. Unity 6000.5.8f1 nötig.
- **Version bauen:** `Werkzeuge/veroeffentlichen.sh` (Editor vorher schließen) baut
  Mac + Windows und packt fertige ZIPs nach `Builds/dist/`.
- **Netzwerk:** server-autoritativ über Netcode for GameObjects, vorerst nur
  Host-Modus.
- **Leistungsanzeige:** im laufenden Spiel **F3** — blendet FPS (inkl. Minimum,
  Maximum, 1%-Tiefpunkt), Frame-Zeit, RAM, aktive Tonquellen und einen kurzen
  Verlaufsbalken ein. Standardmäßig aus, reines Entwickler-Hilfsmittel.

## Dokumentation

- [`Dokumentation/SCOPE.md`](Dokumentation/SCOPE.md) — was Version 1 enthält und was nicht
- [`Dokumentation/ARCHITECTURE.md`](Dokumentation/ARCHITECTURE.md) — technische Grundentscheidungen
- [`Dokumentation/NETCODE.md`](Dokumentation/NETCODE.md) — Netzwerk-Architektur
- [`Dokumentation/ASSETS.md`](Dokumentation/ASSETS.md) — verwendete Asset-Pakete und Lizenzen
- [`Dokumentation/VEROEFFENTLICHEN.md`](Dokumentation/VEROEFFENTLICHEN.md) — wie neue Versionen veröffentlicht werden
- [`Dokumentation/PROGRESS.md`](Dokumentation/PROGRESS.md) — Fortschritt, nach jeder Sitzung aktualisiert
