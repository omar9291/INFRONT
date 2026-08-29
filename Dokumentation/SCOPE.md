# SCOPE.md — Was ist Version 1?

Letzte Aktualisierung: 2026-08-29

Dieses Dokument legt fest, was Version 1 (der spielbare Kern / MVP) enthaelt
und was bewusst NICHT. Jede neue Idee waehrend der Entwicklung wird gegen
diese Liste geprueft. Passt sie nicht rein, kommt sie auf die "Spaeter"-Liste
unten — nicht sofort ins Spiel.

## Projektrahmen

- Engine: Unity 6000.5.8f1 (festgelegt, wird nicht mehr gewechselt)
- Sprache: C#
- Genre: Third-Person Team-Deathmatch-Shooter
- Plattform-Ziel: PC (Windows/Mac/Linux)
- V1-Modus: Einzelspieler gegen Bots, im Host-Modus (dein Rechner ist
  Server und Spieler zugleich)
- Netzwerk: server-autoritativ von Anfang an (der Server entscheidet Treffer)

## Version 1 ENTHAELT

Die Kernschleife: bewegen -> Gegner sehen -> schiessen -> treffen -> sterben -> respawnen.

- [ ] Third-Person-Charakter: Laufen, Sprinten, Springen, Schulterkamera
- [ ] Eine Waffe: Sturmgewehr mit Magazin und Nachladen
- [ ] Trefferabfrage (Hitscan), Schaden, Tod
- [ ] Respawn nach kurzer Wartezeit an einem Spawnpunkt
- [ ] Ein Bot-Typ: sucht den Spieler ueber NavMesh, schiesst, ist besiegbar
- [ ] Eine kleine Testkarte: graues Blockout, keine Kunst
- [ ] Team Deathmatch: 2 Teams, Punkte bis zum Limit, Rundenende
- [x] Minimal-HUD: Leben, Munition, Punktestand, Restzeit
      ACHTUNG: bewusst ein PLATZHALTER (reiner IMGUI-Text, keine Grafik).
      Das richtige HUD kommt in "Spaeter - Stufe 4" mit der Grafik.
- [ ] Einfaches Menue: Start -> Runde -> Endbildschirm -> zurueck ins Menue
- [ ] Netzwerk-Fundament server-autoritativ (Host-Modus)

## Version 1 ENTHAELT NICHT (bewusst weggelassen)

Diese Dinge sind NICHT gestrichen — sie stehen auf der "Spaeter"-Liste
und werden in dieser Reihenfolge angegangen, nachdem V1 spielbar und
geprueft ist.

### Spaeter — Stufe 1 (Bewegung & Kampf vertiefen)
- Sliden
- Klettern
- Granaten und Gadgets (Fortnite-Stil)
- Mehrere Waffen + Waffenauswahl
- Mehrere Bot-Schwierigkeitsstufen, vom Spieler waehlbar

### Spaeter — Stufe 2 (Welt & Fahrzeuge)
- Fahrbare Fahrzeuge (Autos/Buggys)
- Grosse, offene Karte fuer Fahrzeugnutzung
- Zerstoerbare Umgebungsobjekte (Baeume, Zaeune)
- Mehrere Karten

### Spaeter — Stufe 3 (Online & Live-Betrieb)
- Echtes Online-Multiplayer ueber das Internet
- Dedizierte Server (siehe NETCODE.md)
- Variable Teamgroesse bis 10 pro Team
- Matchmaking / Serverliste

### Spaeter — Stufe 4 (Fortschritt & Politur)
- Battle Pass mit freischaltbaren Kosmetik-Items
- Skin-System fuer Waffen und Charakter-Outfits (rein kosmetisch)
- Realistische Grafik: Modelle, Texturen, Umgebung aus dem Asset Store
- Realistischer, ernster Sound und Musik (Battlefield-artig)
- Feinschliff Schiessgefuehl (schnell, arcadig)

## Regel fuer neue Ideen

1. Idee aufschreiben.
2. Gehoert sie zur V1-Kernschleife oben? Wenn nein:
3. Auf die passende "Spaeter"-Stufe setzen. Fertig. Nicht jetzt bauen.
