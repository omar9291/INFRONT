# INFRONT – Fertigstellung

Stand: 2026-09-06. Ergänzt den Auftrag vom 2026-09-04 anhand des tatsächlichen
Repositories; ältere Etappenlisten in PROGRESS.md bleiben als Geschichte erhalten.

## Auftrag und Zusammenarbeit

Der Nutzer hat die Fertigstellung des gesamten Plans beauftragt und ausdrücklich
bestätigt, dass dafür kein neues „Go“ nach jeder Phase nötig ist. Routinemäßige
Entscheidungen innerhalb dieses Auftrags selbst treffen. Wenn eine andere Denkstufe
nötig wird und nicht selbst eingestellt werden kann, anhalten und die gewünschte
Stufe nennen. Nicht behaupten, die aktuelle Einstellung verändert zu haben.

Die bestehenden Entscheidungen gelten weiter: Codelizenz „Alle Rechte vorbehalten“;
Mixamo-Dateien lokal erhalten, künftig nicht nach Git übernehmen, veröffentlichte
Git-Geschichte nicht umschreiben. Kostenlose Assets mit passender Lizenz und Credit
sind erlaubt. Kommentare und technische Kennungen bleiben bestehen; alle Texte
für Spieler sind Englisch. Spielstände, Kennungen und Dateinamen nicht umbenennen.

Codex arbeitet isoliert auf `codex/finish-infront-20260905`. Vor einer Übernahme in
das Hauptprojekt dessen aktuellen Commit und ungesicherte Änderungen prüfen, damit
gleichzeitige Arbeit mit Claude erhalten bleibt. Änderungen je abgeschlossener
Einheit prüfen und getrennt committen.

## Abgleich mit Claude am 2026-09-05

Claudes Hauptzweig `4f29c2e` enthält die vorherige Codex-Arbeit seit `ec150a6`:
Kartenmitte, Sprachkatalog, Quellenkatalog, AO-Anbindung und Leistungsmessung.
Danach kamen SSAO, äußere Wanddetails und die Korrektur schwarzer Metalldeckungen.
Die isolierte Arbeitskopie wurde verlustfrei auf diesen Stand vorgezogen;
die ursprünglichen Änderungen bleiben zusätzlich im Git-Stash gesichert.

Der gespeicherte M1-Lauf von 18:10 UTC misst bei 1600 × 782 Pixeln, Full,
5 gegen 5, klarem Wetter und Ausscheiden insgesamt 59,94 FPS und 51,80 FPS
beim 1-%-Tiefpunkt. Drei einzelne Blickfenster liegen unter 50 FPS.
Das ist ein belastbarer Ausgangswert, noch keine Abnahme aller Situationen.
Die von Claude gemeldeten 291 Tests sind im Projektprotokoll dokumentiert;
der frühere eigene Ausgangslauf mit 274 Tests bleibt unten als Verlauf erhalten.

Die Credits-Seite zeigte weiterhin die alte Anbieter-Kurzliste. Sie liest nun
alle 47 Einträge des gemeinsamen Katalogs mit Name, Autor, Lizenz und anklickbarer
Quelladresse. 9/9 EditMode-Prüfungen für Import und Quellen sowie 14/14 Menü- und
Sprachprüfungen bestehen, einschließlich Erreichbarkeit des letzten Credits-Eintrags.
Offene Herkunftsdetails in einzelnen Bestandsassets werden dadurch nicht als
geklärt ausgegeben; die Hinweise im Katalog bleiben erhalten.

Seit 2026-09-06 sind 61 neue Audiodateien mit Varianten, Materialwahl, Raumton
und drei Musikbearbeitungen eingebunden; der Katalog umfasst 57 Credits.
296/296 im vollständigen Spieltest nach der Hörerkorrektur und 11/11 Import-/
Quellenprüfungen bestanden. Die gewünschte rauere Fassung nutzt jetzt aufgenommene
Waffenmechanik, Explosion und Strukturgeräusche; ihre 3/3 EditMode- und 3/3
PlayMode-Zielprüfungen bestehen. Hör-Abnahme und ergänzende Außenaufnahmen bleiben offen.
Der fertige Mac-Build erreicht auf dem M1 bei Full, klarem Wetter und 5 gegen 5
59,94 FPS im Mittel sowie 51,44 FPS beim 1-%-Tiefpunkt. Klangvarianten werden
beim Start vorgeladen, damit ihre erste Benutzung das Gefecht nicht ausbremst.

Größte Restarbeit: ergänzende Aufnahmen und Hör-Abnahme, zweite Karte,
Spielbalance und abschließende Bild-/Leistungs-/Hör- und Einsteigertests.
Wanddetails der inneren Trennwände, Rauch und Leistungsreserve bleiben offen.

## Verifizierter Ausgangspunkt

- Original: `/Users/user/UnityProjects/INFRONT`, zuletzt geprüft bei Commit `5d8747c`.
- Unity: 6000.5.8f1; nicht aktualisieren.
- Frischer Ausgangstest: 274/274 PlayMode-Tests bestanden, 2026-09-05,
  16:17–16:23 UTC. Ausgeführt in einer Kopie mit eigener Company-ID, weil
  Profil-/Datentests echte gespeicherte Daten löschen.
- Der Versuch mit `-nographics` stürzte in Unitys Renderer ab. Der reguläre
  Metal-Testlauf bestand. Das ist keine Aussage über die Renderleistung im Spiel.
- Bereits vorhanden: Englisch-Umstellung, Lizenz, Credits-Zusammenfassung,
  PBR-Texturen, gebackenes indirektes Licht, Reflection Probes, umfangreiche
  Hallen-/Dachdetails und vier aufgenommene Schussgeräusche.
- Noch kein Nachweis für 60 FPS / mindestens 50 FPS beim 1%-Tiefpunkt.
  Alte Screenshots zeigen etwa 60 FPS und Tiefpunkte um 30 FPS; Screenshot-
  Aufnahme und das bisher nur 120 Frames lange Statistikfenster beeinflussen dies.

## Arbeitsstand und Abnahme

| Phase | Tatsächlicher Restumfang | Stand |
|---|---|---|
| 0 – Diagnose | Rampenzugänge durch sichtbare Geländer versperrt; Rampenoberseiten falsch ausgerichtet; Trainingsziel erscheint schwebend im Match. Das genaue vom Nutzer beobachtete Symptom ist nicht beschrieben. | Korrekturen getestet und im Hauptzweig |
| 1 – Englisch | Gemeinsamer Textkatalog und Tests für echte UI-Zustände/Anzeigen; keine Kennungen umbenennen. | Im Hauptzweig; Menü-/Sprachtests erneut grün |
| 2 – Credits | Einzelne Assets, Autoren, präzise Quellen, gespeicherte Lizenzbelege; gemeinsames Verzeichnis für Spiel und CREDITS.md; neue ungenannte Dateien müssen Tests scheitern lassen. | Menü angebunden und Tests grün; offene Bestandsherkunft separat dokumentiert |
| 3 – Materialien/Licht | Fehlende AO-Verknüpfung, geeignete AO-Dateien, kontrollierte Bild-/Leistungsvergleiche; Material- und Lichtbasis existiert bereits. | In Arbeit |
| 4 – Details | Vorhandene Trim-/Rohr-/Dachbibliothek ergänzen, auffällige kahle Flächen und unplausible Übergänge prüfen. | Offen |
| 5 – Ton/Musik | Aufnahmen, Varianten, Raumton, leisere Musik und getrennte Lautstärke eingebunden. Ergänzende Außenaufnahmen und Hör-Abnahme bleiben offen. | Technische Schlussprüfung |
| 6 – Zweite Karte | Nach Abnahme der ersten Grafikbasis: eigenständiges Layout, Auswahl im Menü, Spawns/Bombenplätze/NavMesh, Durchlauf beider Modi. | Offen |
| 7 – Abschluss | Balance, Schwierigkeit, drei Fenstergrößen und Farbmodi, vollständige Tests, Startbarkeit, Release-Unterlagen. | Offen |

Fertig heißt: zwei spielbare, beleuchtete und detaillierte Karten; Englisch und
Credits überall; geeignete aufgenommene Geräusche plus Musik; reproduzierbare
M1-Leistungsmessung; grüne Tests; eine unvorbereitete Person kann ein vollständiges
Match spielen. Hörprobe und unbegleiteter Spieltest benötigen menschliche Rückmeldung
und werden nicht durch einen automatischen Test als erledigt markiert.

## Prüfablauf

1. Änderungen in isolierter Testkopie zusammenführen.
2. Nach Szenenänderungen `SceneBuilder.BuildUndBacke` ausführen, erst dann Tests
   und App-Build. Ein bloßer Build ohne Backen entfernt die indirekte Beleuchtung.
3. Neue relevante Regressionen und bestehende betroffene Tests ausführen.
4. Gerenderten Build prüfen: gleiche Kamerapositionen für Bildvergleiche;
   längere Leistungsmessung ohne Screenshot-Aufnahmen, Rohbildzeiten speichern.
5. Einheiten committen; vor Übernahme Original erneut auf konkurrierende Änderungen
   prüfen. Keine ungetesteten Änderungen als fertigen Spieler-Build ausgeben.
