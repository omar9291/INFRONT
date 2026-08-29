# INFRONT — Projekt-Arbeitsanweisungen

Third-Person Team-Deathmatch-Shooter in Unity. Solo-Projekt mit KI-Unterstuetzung.
Studio-Marke: Driftlab.

## Zu Beginn JEDER Sitzung

1. Dokumentation/PROGRESS.md lesen — das ist die Wahrheit ueber den Stand.
2. Dokumentation/SCOPE.md lesen — was gehoert zu V1, was nicht.
3. Erst dann weiterarbeiten.

## Die wichtigsten Regeln fuer dieses Projekt

- Deutsch: alle Antworten, Code-Kommentare und Oberflaechentexte auf Deutsch.
- Modellwechsel: denkende Arbeit auf Opus/hoher Effort, umsetzende auf
  Sonnet/Auto. STOPP-Zeilen vor Diagnose/Planung und nach bestaetigtem Plan.
- Rueckfragen immer mit Auswahlmoeglichkeiten stellen (AskUserQuestion),
  nie als offene Textfrage.
- Grundschleife vor Deko: erst die Kernschleife spielbar und geprueft,
  dann Tiefe. Wunschliste in der Reihenfolge aus SCOPE.md.
- Nichts loeschen, nur ergaenzen — solange nichts kaputtgeht. Vor jeder
  ersetzenden Operation die zerstoerungsfreie Alternative suchen und
  ansprechen, wenn es keine gibt.
- Phasen mit Stopps: nach jeder Phase anhalten, berichten, Pflicht-Pruefliste
  abarbeiten, auf Go warten. Naechste Phase vorschlagen, nicht starten.
- Git-Commit vor jeder inhaltlichen Aenderung, klare Beschreibung, kein
  Feature-Batching ohne Zwischen-Commits.
- Risiko-Transparenz: technische Risiken (Performance, Lizenz, Architektur-
  Sackgasse) sofort melden, nicht stillschweigend umgehen.
- Neue Ideen gegen SCOPE.md pruefen. Passt nicht rein -> "Spaeter"-Liste.

## Technische Fakten

- Unity 6000.5.8f1, C#, festgelegt — nicht wechseln.
- Netzwerk: server-autoritativ von Anfang an, V1 im Host-Modus. Details
  in Dokumentation/NETCODE.md.
- Alles per Editor-Code erzeugen (Szenen, Prefabs, Platzhalter-Geometrie),
  nichts von Hand in der Unity-Oberflaeche. Tests headless im PlayMode.
- Eigener Code in Assets/_Project/, importierte Assets in Assets/ThirdParty/,
  strikt getrennt.
- Projektpfad /Users/user/UnityProjects/INFRONT — NICHT nach ~/Documents
  (iCloud-Falle).

## Was auf diesem Mac nicht geht

- Keine Screenshots, keine synthetischen Tastatureingaben. Das Spiel kann
  nicht selbst angesehen oder bedient werden.
- Nie behaupten, etwas sei optisch geprueft. Stattdessen automatisierte
  Tests und ehrlich sagen, was ungeprueft bleibt.
- Framerate nicht messbar auf diesem Rechner — nur durch sparsame Bauweise
  anstreben.

## Pflicht-Pruefliste am Ende jeder Phase

- [ ] Alle Aenderungen committed?
- [ ] Projekt startet ohne Fehler (headless geprueft)?
- [ ] Neue Assets in ASSETS.md dokumentiert, Lizenz geprueft?
- [ ] PROGRESS.md aktualisiert?
- [ ] Framerate-Ziel plausibel eingehalten (soweit ohne Messung beurteilbar)?
- [ ] Kurze Zusammenfassung geschrieben, was fertig ist?
- [ ] Offene Fragen klar aufgelistet?
- [ ] Naechste Phase vorgeschlagen, aber nicht gestartet ohne Go?
