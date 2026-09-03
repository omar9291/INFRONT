# Morgenbericht — Nacht 10 (2026-09-03/04)

## Kurzfassung

**Etappe 1 „Realistisches Spielgefühl" ist komplett. Alle sieben Schritte
gebaut, alle Tests grün.**

- Tests: **164/164** (vorher 128)
- Sieben Commits, jeder einzeln geprüft und gepusht
- Neuer Stand: `74f7ef2`
- 60 FPS auf dem M1 gehalten, 1%-Low 57 (vorher einmal 20)

Du hattest gesagt, ich soll die ganze Nacht durcharbeiten und selbst Ideen
einbringen. Gearbeitet habe ich, solange ein Zug lief — ein Wecker, der mich
nachts von selbst aufweckt, existiert nicht. Was ging, ist drin.

---

## Was jetzt anders ist, wenn du spielst

### Du bist langsamer und schwerer

Gehen 6,0 → **4,6 m/s**, Sprinten 10,0 → **7,2 m/s**. Zehn Meter pro Sekunde
wären Weltrekord gewesen, und das ohne Ausrüstung. Die Beschleunigung fiel von
55 auf 14: du brauchst jetzt knapp eine halbe Sekunde auf Tempo statt einer
Zehntelsekunde. Beim Stehenbleiben rutschst du noch ein Stück. In der Luft
kannst du kaum noch steuern. Springen geht nur noch 0,85 m statt 1,5 m.

**Neu:** Sprinten hat einen Anlauf von 1,1 s und läuft in 0,5 s aus. Nach einer
harten Landung verlierst du 0,35 s lang Kontrolle — vorher senkte sich nur der
Blick, jetzt betrifft es wirklich die Bewegung.

### Du atmest

Das war dein ausdrücklicher Wunsch und gab es vorher gar nicht. Der Blick
schwankt in einem ruhigen Rhythmus, 14 Atemzüge pro Minute in Ruhe, bis zu 34
wenn du fertig bist. Die Waffe folgt verzögert statt starr mitzugehen — dadurch
wirkt sie schwer.

Sprinten macht müde (9 s bis ganz erschöpft, 14 s Erholung). Wenig Leben macht
den Atem ebenfalls schwer, auch ohne Anstrengung. Beim Zielen kannst du die Luft
anhalten — **aber nur 4,5 Sekunden**, danach schnappst du nach Luft und der Atem
geht stärker als vorher. Ohne diese Grenze wäre Anhalten reiner Vorteil statt
Realismus.

Ab spürbarer Anstrengung hörst du dich selbst atmen.

### Die Waffen haben Gewicht

Anlegen dauert jetzt je nach Waffe verschieden lang:

| Waffe | Anlegen | Nachschwingen |
|---|---|---|
| Pistole | 0,18 s | wenig |
| Maschinenpistole | 0,22 s | wenig |
| Sturmgewehr | 0,32 s | mittel |
| Scharfschützengewehr | 0,62 s | stark |

Vorher war das für alle gleich und mit 0,11 s praktisch sofort.

**Der Rückstoss ist kein Muster mehr.** Vorher war er exakt vorhersagbar: fester
Wert nach oben, seitlich eine Sinuskurve über die Schusszahl. Man konnte ihn
auswendig lernen. Jetzt streut er — die Form bleibt erkennbar (es geht nach
oben), aber zwei Schüsse sind nie gleich. Die leichte Pistole ist am
unruhigsten, das schwere Scharfschützengewehr am berechenbarsten.

### Treffer zählen je nach Stelle

Vorher gab es zwei Trefferflächen: Kopf und Körper. Jetzt vier — Kopf, Torso,
Arme, Beine.

- Arme und Beine schlucken einen Teil des Schadens (0,65 bzw. 0,7)
- **Beintreffer bremsen** dich, bis auf 55 % Tempo
- **Armtreffer machen die Waffe unruhig**, bis 1,4 Grad mehr Streuung
- Beide Nachteile klingen langsam ab

**Blutungen** sind neu. Ein Treffer kann eine Wunde öffnen, die über die Zeit
Leben nimmt — bis zu drei gleichzeitig. Sie hört **nicht von selbst auf**. Nur
ein Verbandspaket stoppt sie, und das heilt nur 25 Leben zurück.

Du verblutest aber nicht bis auf null: bei 12 Leben ist Schluss. In einem
Rundenspiel wäre alles andere zu hart und nimmt dir jede Chance.

### Fähigkeiten sind Ausrüstung geworden

Wie besprochen: das System bleibt, nur Inhalt und Angebot ändern sich.

Neu ist das **Verbandspaket** (Taste F, 200 $). Wirkt sofort bei dir, wird nicht
geworfen.

Der **Scan-Puls ist nicht gelöscht** — er steht weiter im Katalog, wird aber
nicht mehr im Kaufmenü angeboten, weil er Gegner durch Wände zeigt. Ein Schalter
(`Angeboten`) stellt ihn sofort wieder her. Er musste im Katalog stehen bleiben,
weil die Reihenfolge dort der Netz-Index ist und in Kaufdaten steht — hätte ich
ihn herausgenommen, wären Brandwand und Stolperdraht verrutscht.

Rauch-, Blend- und Splittergranate waren schon vorher realistische Ausrüstung
und bleiben, wie sie sind.

### Der Ton hat Entfernung und Wucht

Ferne Geräusche werden jetzt **dumpf**, nicht nur leise — jede Klangquelle hat
einen Tiefpass, der mit der Entfernung zufährt. Auf 85 m bleibt ein Grollen.

Nach einer nahen Explosion **klingeln die Ohren richtig**: alles andere fällt
auf 18 % Lautstärke und wird dumpf, für rund 2 Sekunden. Vorher gab es nur einen
Pfeifton und man hörte den Rest unverändert weiter.

Schritte richten sich nach dem Untergrund: Metall trägt weiter, Schutt schluckt.

---

## Drei echte Fehler, die dabei aufgeflogen sind

**1. Sprinten wäre lautlos geworden.** Die Schwelle für laute Schritte stand auf
7,5 m/s — der neue Sprint ist aber nur 7,2 m/s. Genau das Gegenteil der Absicht:
Sprinten soll dich verraten. Das war ein Folgefehler aus Schritt 2, den ich erst
in Schritt 7 bemerkt habe.

**2. Das Verbandspaket wäre unbegrenzt nutzbar gewesen.** Das
Fähigkeiten-System wertet „kein Objekt in der Welt erzeugt" als „nicht benutzt".
Ein Verband erzeugt nichts — also wäre er nie verbraucht worden.

**3. Die Sterbe-Animation lief in einer Schleife.** Die Leiche wäre endlos
wieder aufgestanden und umgefallen. Und mein erster Fix dagegen war selbst
falsch: er schaltete versehentlich *alle* Animationen ab, die Figur hätte
reglos dagestanden. Erst der zweite Weg stimmte.

Alle drei hat je ein Test gefangen, nicht mein Auge.

---

## Die teuerste Lektion der Nacht

Beim Ohrenklingeln habe ich **fünf komplette Testläufe verloren** — jedes Mal
kam exakt derselbe Zahlenwert heraus, obwohl ich den Code geändert hatte.

Ursache: Werte von `[SerializeField]`-Feldern stehen in der **Szenendatei**.
Ändert man den Standardwert im Code, passiert erst mal gar nichts — Unity nimmt
den gespeicherten. Ich hätte nach der ersten Änderung die Szene neu bauen müssen.

Das Erkennungszeichen war die ganze Zeit da: **dreimal derselbe Wert heisst nicht
„mein Code ist falsch", sondern „mein Code läuft gar nicht".** Statt weiter zu
raten hätte ich beim zweiten Mal einen Diagnose-Test schreiben sollen — der hat
es dann in einem Lauf gezeigt. Steht jetzt im Gedächtnis.

---

## Was ich NICHT konnte

- **Ob sich das gut anfühlt.** Kein Test misst das. Alles unten in „Was du
  prüfen musst".
- **Die Schussgeräusche.** Bleiben prozedural erzeugt und klingen nach
  Synthesizer. Dagegen hilft nur eine echte Aufnahme — was fehlt, steht unten.
- **Zielen über Kimme und Korn.** Die Waffen sind Code-Quader ohne Visierung.
  Die Zwischenlösung (Kamera näher, Blickfeld enger, Streuung sinkt) ist jetzt
  je Waffe verschieden, mehr geht ohne echte Modelle nicht.
- **Magazin am Modell ablesen.** Aus demselben Grund.
- **Die Bots.** Sie benutzen NavMesh-Bewegung, nicht deinen Bewegungscode. Sie
  laufen also weiter wie vorher, während du schwer geworden bist. Das gehört
  gerade gezogen und ist noch nicht passiert.

---

## Was du selbst prüfen musst

1. **Ist es zu langsam?** 4,6 m/s ist ein deutlicher Einschnitt. Wenn die Karte
   sich riesig anfühlt, sag es — dann gehe ich auf 5,2.
2. **Springen mit 0,85 m** — kommst du noch überall hoch, wo du hin musst? Das
   ist die riskanteste Zahl der Nacht.
3. **Das Atmen** — zu stark, zu schwach, wird dir schlecht davon?
4. **Der Rückstoss** — noch kontrollierbar oder jetzt Glückssache?
5. **Blutungen** — nervt es, oder macht es Treffer bedeutsam?
6. **Der Unterschied zu den Bots** — fällt auf, dass sie leichter sind als du?

Alle Werte sind im Unity-Inspector verstellbar, ohne Code anzufassen:
`NetworkPlayerController` (Bewegung, Trägheit, Gewicht), `Breathing` (Rhythmus,
Stärke, Anstrengung, Anhalten), `Bleeding` (Blutung, Zonenfolgen), `EarRinging`
(Auslösen, Abklingen), und die Waffenwerte in `SceneBuilder.CreateWeapons`.

---

## Wenn du echte Geräusche willst

Von freesound.org (CC0) gebraucht würden:

| Datei | Länge | Was |
|---|---|---|
| `schuss_gewehr.wav` | 0,2–0,4 s | Sturmgewehr, trocken, ohne Hall |
| `schuss_mp.wav` | 0,1–0,2 s | Maschinenpistole, heller |
| `schuss_sniper.wav` | 0,3–0,5 s | schwer, tief |
| `schuss_pistole.wav` | 0,1–0,2 s | kurz, scharf |

Mono, 44,1 kHz, nach `Assets/_Project/Audio/Resources/`. Der Rest passiert
automatisch — der Dateiname ersetzt den Platzhalter. **Das ist der grösste
verbleibende Sprung im Ton, und du kannst ihn ohne mich machen.**

---

## Commits dieser Nacht

| Commit | Inhalt |
|---|---|
| `5423400` | Schritt 1: Mixamo-Figur verdrahtet |
| `77df76f` | Schritt 2: Gewicht und Trägheit |
| `6216325` | Schritt 3: Atmung |
| `750e4f5` | Schritt 4: Waffenmasse und Rückstoss |
| `c5e4620` | Schritt 5: Trefferzonen und Bluten |
| `b08aec0` | Schritt 6: Ausrüstung statt Fähigkeiten |
| `74f7ef2` | Schritt 7: Ton |

Nichts gelöscht. Jeder neue Teil hat einen Rückfall, jeder Schritt lässt sich
einzeln mit `git revert` zurücknehmen.
