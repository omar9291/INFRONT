# Bericht — HUD-Umbau (2026-09-01)

Du hattest nach dem Spielen gesagt: *„es ist irgendwie noch nicht so schön
und cool. mach es erstens übersichtlicher und zweitens coolere Sachen, z. B.
Animation."* Hier steht, was passiert ist.

---

## Kurz gesagt

Das ganze Spiel-HUD lief vorher aus **12 einzelnen alten IMGUI-Zeichnern**, die
jeder für sich irgendwo auf den Bildschirm gemalt haben — 10 verschiedene
Schriftgrößen, keine Kästen, fünf Sachen die sich in der Bildmitte
überlagern konnten. Das war der „Debug-Overlay"-Look.

Jetzt läuft **das komplette HUD in einem einzigen UI-Toolkit-Dokument** mit
demselben Design wie dein Menü (Dark Tactical, Orange-Akzent), einem festen
Zonen-Raster und **einer** Schrift-Hierarchie. Dazu Animationen an allen vier
Stellen, die du wolltest.

**98 von 98 Tests grün** (5 neue fürs HUD). Mac-Build ist neu, das Spiel läuft.

**Wichtig:** Ich kann das HUD **nicht sehen**. Ob die Abstände stimmen, ob
etwas verrutscht ist, ob eine Animation ruckelt — das musst du beurteilen. Die
Tests prüfen nur: existiert das Element, trägt es den richtigen Wert, reagiert
es aufs richtige Ereignis, fängt es keine Mausklicks ab.

---

## Übersichtlicher

- **Punktestand oben** in einer Leiste: `ALPHA 5` — Uhr — `3 BRAVO`, dein Team
  farbig hervorgehoben. Uhr wird rot unter 10 Sekunden.
- **Lebende-Anzeige (neu):** unter dem Punktestand eine Reihe Rauten pro Team,
  die erlöschen, wenn jemand stirbt. Auf einen Blick sichtbar, wie viele noch
  stehen — die größte Übersichts-Verbesserung.
- **Eine Statuszeile** statt fünf gestapelter Texte: zeigt nach Priorität
  Kaufzeit / Rolle / „BOMBE GELEGT 32".
- **Unten links** ein zusammenhängender Kasten: Geld, Weste, Lebensbalken mit
  Zahl.
- **Unten rechts** die Munition — groß, mit Waffenname und zwei
  Slot-Kästchen `[1] [2]`. Trennt „mein Zustand" (links) von „meine Waffe"
  (rechts).
- **Fähigkeiten unten Mitte** als echte Kacheln: Taste im Eck, Ladungen als
  Punkte, Abklingzeit als dunkler Balken der leerläuft.
- **Kill-Feed oben rechts** mit Zeilenhintergrund statt nacktem Text.
- **Kaufmenü neu:** zweispaltig (links Waffen, rechts Ausrüstung +
  Fähigkeiten). Jede Zeile mit Tastenkürzel im Kästchen, Name, Preis bzw.
  grünes „gekauft". Nicht Leistbares ist ausgegraut und nicht anklickbar.
- **Punktetabelle (Tab), Pause, Rundenende** im gleichen Panel-Stil.

Noch als altes IMGUI (liegt bewusst *über* dem HUD und kollidiert mit nichts):
Fadenkreuz, Verbündeten-Schilder über den Köpfen, der weiße Blend-Blitz und
die gelben Scan-Kästen.

---

## Coolere Sachen / Animation

**HUD lebt jetzt:**
- Geld zählt hoch/runter statt zu springen.
- Lebensbalken hat einen „Geisterbalken", der dem echten Wert langsam
  nachläuft; bei Schaden blitzt er weiß, der Kasten ruckelt kurz, der Rand
  blitzt rot.
- Munitionszahl pulst bei jedem Schuss, wird rot unter 25 %.
- Fähigkeitskachel schrumpft kurz und blitzt orange, wenn du sie einsetzt.
- Kill-Feed-Zeilen rutschen von rechts rein und blenden weich aus.
- Ereignis-Banner (Doppelkill / Ace …) rutscht von oben rein.

**Kampf-Wumms:**
- **Zeitlupe** bei Ace, Clutch und Matchgewinn (kurz auf ~30 % Tempo, dann
  weich zurück). Nur im Einzelspiel gegen Bots — im Online-Spiel würde das
  die anderen mit einfrieren.
- Tracer (Schusslinien) dicker, etwas länger sichtbar, werden beim Verblassen
  dünner.

**Figur und Waffe:**
- **Sprinthaltung:** beim Sprinten kippt die Waffe schräg nach unten zur
  Seite — man kann in der Haltung nicht nachladen.
- **Landung:** nach einem Sprung sackt die Waffe beim Aufkommen kurz ab.
- **Sterben:** die Figur kippt jetzt in die Richtung um, in die der Schuss
  sie schiebt — nicht mehr immer stur nach vorne.

**Menü:**
- Beim Seitenwechsel blenden die Inhalte nacheinander von unten ein.

---

## Spieltest-Auftrag

Bitte starten und darauf achten:

**Übersichtlichkeit**
- Findest du Leben / Geld / Munition / Fähigkeiten auf einen Blick?
- Überlagert sich noch irgendwas in der Bildmitte?
- Lebende-Rauten oben: zählen sie richtig runter?
- Kaufmenü: klarer als vorher? Kannst du mit Maus **und** mit Zahlen kaufen?
  (Wichtig: geht Schießen noch normal, oder klaut das HUD die Klicks?)

**Animation**
- Blitzt/ruckelt der Lebensbalken bei Schaden — zu viel, zu wenig?
- Zeitlupe bei einem Ace / beim Matchgewinn — fühlt sie sich gut an oder
  nervt sie?
- Waffe beim Sprinten schräg nach unten — sieht das gut aus?
- Kippt die Figur beim Tod glaubwürdig weg vom Schützen?

**Was kaputt sein könnte, bitte melden**
- Element sitzt an der falschen Stelle / ragt aus dem Bild.
- Nach der Zeitlupe bleibt das Spiel langsam (sollte NICHT passieren — es
  setzt sich mehrfach abgesichert zurück).
- Klick im Kaufmenü tut nichts, oder Schießen geht während der Kaufzeit nicht.
- Menü-Übergang hängt oder flackert.

---

## Rückweg, falls etwas nicht gefällt

- Voller Code-Stand vor dem Umbau liegt im Scratchpad-Backup.
- Das alte IMGUI-Menü ist mit **F10** weiter erreichbar (unverändert).
- Bei den Streifen auf dem M1: Menü → **BILD → SCHLICHT** (unverändert).
