# NETCODE.md — Netzwerk-Architektur

Letzte Aktualisierung: 2026-08-29

## Grundprinzip: server-autoritativ von Anfang an

Der Server entscheidet, was im Spiel wirklich passiert. Clients senden nur
Eingaben ("ich schiesse in diese Richtung"), nie Ergebnisse ("ich habe
getroffen"). Der Client-Bildschirm zeigt an, was der Server beschlossen hat.

Grund: Bei einem Shooter ist client-autoritativ nicht gegen Cheater zu
schuetzen. Wer seinen Client manipuliert, koennte sonst immer treffen, nie
sterben, durch Waende laufen. Server-autoritativ von Beginn an erspart
einen kompletten Umbau, wenn spaeter echtes Online kommt.

Kosten: hoehere Komplexitaet. Jede Aktion muss zweimal gedacht werden
(Client-Seite / Server-Seite). Phasen 1-2 werden dadurch grob ein Drittel
bis die Haelfte aufwendiger. Das ist bewusst so gewaehlt.

## V1: Host-Modus

In Version 1 gibt es keinen Server im Internet. Der eigene Rechner ist
Server UND Client zugleich (Host). Der Code ist identisch mit spaeterem
echten Multiplayer — nur laeuft der Server lokal. Der Wechsel zu echtem
Online ist dann ein Schalter, kein Umbau.

Einzelspieler gegen Bots laeuft ebenfalls ueber diesen Host: die Bots sind
serverseitige Figuren, der Spieler ist der lokale Client.

## Bibliothek: Netcode for GameObjects (NGO)

Unitys offizielle High-Level-Netzwerkbibliothek. Gewaehlt weil:
- offiziell von Unity gepflegt, gute Doku
- server-autoritatives Modell ist der Standard-Weg
- NetworkTransform, NetworkVariable, ServerRpc/ClientRpc decken alles ab,
  was V1 braucht
- kleinere Lernkurve als eine Custom-Loesung

Wird in Phase 1 als Paket hinzugefuegt (com.unity.netcode.gameobjects).

## Spaeter: Peer-to-Peer vs dedizierte Server

ENTSCHEIDUNG (dokumentiert 2026-08-29, Umsetzung erst in "Spaeter"-Stufe 3):

Dedizierte Server, nicht Peer-to-Peer.

| Kriterium            | Peer-to-Peer          | Dedizierte Server        |
|---------------------|-----------------------|--------------------------|
| Wer ist Server      | ein Mitspieler        | neutraler Rechner        |
| Kosten              | keine                 | monatlich Geld           |
| Fairness            | Host hat 0ms Vorteil  | alle gleich              |
| Host verlaesst Spiel| Runde vorbei          | laeuft weiter            |
| Cheat-Sicherheit    | Host koennte cheaten  | sicher                   |
| Latenz              | zum Host, schwankt    | zum Rechenzentrum, stabil|

Fuer einen kompetitiven Shooter ist Fairness und Cheat-Sicherheit
entscheidend. Peer-to-Peer gibt dem Host einen Latenzvorteil und macht ihn
zur Schwachstelle. Deshalb dedizierte Server, sobald es soweit ist.

Realitaets-Hinweis: dedizierte Server kosten Geld (Hosting) und brauchen
Betrieb (Updates, Ueberwachung). Das ist ein Thema fuer spaeter und nur
sinnvoll, wenn es echte Spieler gibt.

## Was in V1 server-autoritativ laeuft

- Bewegung: Client sagt Eingabe, Server bewegt, NetworkTransform verteilt
- Schuss: Client sendet ServerRpc mit Blickrichtung, Server macht Raycast
- Treffer/Schaden: nur Server, per NetworkVariable an alle
- Tod/Respawn: nur Server
- Punktestand: NetworkVariable, nur Server schreibt
- Bots: existieren nur auf dem Server

## Spaeter: Lag-Kompensation (Hit Registration)

Im Host-Modus rechnet der Server Schuesse mit seinem aktuellen Weltzustand.
Das ist dort exakt richtig, weil Server und Spieler derselbe Rechner sind.

Sobald echtes Online kommt, entsteht das bekannte Shooter-Problem: der
Spieler sieht den Gegner ~50-100 ms in der Vergangenheit, zielt genau,
trifft aber laut Server nicht, weil der Gegner inzwischen weiter ist.

Loesung (spaeter, Stufe 3): der Server fuehrt eine kurze Historie der
Positionen aller Figuren. Beim Schuss spult er die Welt auf den Zeitpunkt
zurueck, den der Schuetze gesehen hat (aus dem Zeitstempel in der
Schuss-Anfrage), rechnet dort und stellt die Welt wieder her.

Vorbereitet: die Schuss-Anfrage (FireRpc in NetworkWeapon) traegt bereits
ein Feld clientRenderTime. Es wird jetzt noch nicht ausgewertet, aber die
Lag-Kompensation kann spaeter eingehaengt werden, ohne die Signatur zu
aendern.

## Was in V1 der Client lokal macht (nur Anzeige)

- Kamera
- HUD-Darstellung (Werte kommen vom Server)
- Vorlaeufige Mündungseffekte / Sounds (kosmetisch, kein Gameplay)
