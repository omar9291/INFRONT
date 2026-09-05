# INFRONT – Fertigstellung

Stand: 2026-09-05. Ergänzt den Auftrag vom 2026-09-04 anhand des tatsächlichen
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

## Verifizierter Ausgangspunkt

- Original: `/Users/user/UnityProjects/INFRONT`, Commit `886fe7d`.
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
| 0 – Diagnose | Rampenzugänge durch sichtbare Geländer versperrt; Rampenoberseiten falsch ausgerichtet; Trainingsziel erscheint schwebend im Match. Das genaue vom Nutzer beobachtete Symptom ist nicht beschrieben. | Belegt; Korrekturen werden getestet |
| 1 – Englisch | Gemeinsamer Textkatalog und Tests für echte UI-Zustände/Anzeigen; keine Kennungen umbenennen. | In Arbeit |
| 2 – Credits | Einzelne Assets, Autoren, präzise Quellen, gespeicherte Lizenzbelege; gemeinsames Verzeichnis für Spiel und CREDITS.md; neue ungenannte Dateien müssen Tests scheitern lassen. | In Arbeit |
| 3 – Materialien/Licht | Fehlende AO-Verknüpfung, geeignete AO-Dateien, kontrollierte Bild-/Leistungsvergleiche; Material- und Lichtbasis existiert bereits. | In Arbeit |
| 4 – Details | Vorhandene Trim-/Rohr-/Dachbibliothek ergänzen, auffällige kahle Flächen und unplausible Übergänge prüfen. | Offen |
| 5 – Ton/Musik | Aufnahmen für Schritte und Oberflächen, Einschläge, Mechanik, Atmung/Raumton; Menü- und Spannungsebene; Herkunft oder begründete Synthese je SoundId. | Quellenprüfung |
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
