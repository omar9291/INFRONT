# INFRONT — Projekt-Arbeitsanweisungen

Third-Person Team-Deathmatch-Shooter in Unity. Solo-Projekt mit KI-Unterstuetzung.
Studio-Marke: Driftlab.

## Zu Beginn JEDER Sitzung

1. Dokumentation/PROGRESS.md lesen — das ist die Wahrheit ueber den Stand.
2. Dokumentation/SCOPE.md lesen — was gehoert zu V1, was nicht.
3. Erst dann weiterarbeiten.

## Die wichtigsten Regeln fuer dieses Projekt

- Sprache (Stand 2026-09-04): **alles, was der Spieler sieht, ist Englisch** —
  Menue, HUD, Kaufmenue, Bot-Funksprueche, Waffen- und Faehigkeitsnamen,
  Ladebildschirm, Absturzbericht. Grund: Reichweite. **Deutsch bleiben**
  Code-Kommentare, XML-Doku, Bezeichner, Enum-Werte, Testnamen und
  Test-Meldungen, Debug-Ausgaben, `[Tooltip]`/`[Header]` im Inspector und
  diese Dokumentation. Ebenfalls deutsch bleiben **Asset-Dateinamen**
  (Sturmgewehr.asset, Faehigkeit_Rauchwand.asset), **SoundId-Namen**,
  **PlayerPrefs-Schluessel** und die Dateinamen im Spielerordner
  (profil.json, statistik.json, abstuerze/) — die stehen in Speicherdaten
  und in Katalog-Verweisen; umbenennen wuerde Fortschritt zerstoeren.
  **Nie einen Anzeigetext (DisplayName) als Schalter im Code auswerten** —
  genau daran waere bei dieser Umstellung das Waffenmodell kaputtgegangen.
  Antworten im Chat: Englisch.
- **Licht backen NICHT vergessen** (Stand 2026-09-04): `SceneBuilder.Build` legt
  die Arena jedes Mal neu an und wirft dabei jede gebackene Lichtkarte weg.
  Danach fehlt der indirekte Anteil - ohne Fehlermeldung, die Halle sieht
  einfach wieder flach aus. Richtig ist **`SceneBuilder.BuildUndBacke`**
  (Bauen + `Backlicht.BackeFein`, zusammen rund eine Minute). `BacklichtTests`
  macht den Testlauf rot, wenn in der Arena keine Lichtkarte liegt.
  Reihenfolge immer: Szene bauen -> backen -> App bauen.
- **Alles Sichtbare gehoert unter `Map`** (Stand 2026-09-05): `MacheKarteBackfaehig`
  laeuft nur ueber die Karte. Was daneben in der Szene haengt, bekommt kein
  gebackenes Licht - und schlimmer: fehlt eine grosse Flaeche im Backen, ist
  sie fuer den Backer ein LOCH, durch das Himmelslicht hereinstroemt und alles
  andere falsch aufhellt. Der Boden war so gebaut; ihn hineinzunehmen kostete
  28 Punkte Helligkeit, die vorher aus dem Leck kamen. Wer etwas nach `Map`
  umhaengt, prueft ausserdem `MatchTestHarness.ClearArena` - das schaltet den
  Inhalt der Karte fuer Tests ab.
- **Helligkeit allein reicht als Mass nicht** (Stand 2026-09-05): Median,
  Streuung und schwarzer Anteil koennen alle besser werden, waehrend das Bild
  sepia wird. Immer auch den Farbstich messen (Mittelwert R minus B ueber alle
  Rundgangbilder). Neutral liegt bei etwa +3, ab +15 sieht man den Stich.
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
