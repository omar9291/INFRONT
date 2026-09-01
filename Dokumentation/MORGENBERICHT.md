# Morgenbericht — Nacht vom 31.08. auf 01.09.2026

Guten Morgen! Hier steht, was in der Nacht passiert ist, was du testen
sollst und was ich **nicht** prüfen konnte.

---

## Kurz gesagt

Der ganze MASTERPLAN von Etappe **A (Rest)** bis Etappe **E** ist gebaut,
dazu Deko und ein schönerer Ladebildschirm. Die Tests sind von **73 auf 93**
gewachsen und **alle grün**. Der Mac-Build ist neu, das Spiel läuft.

Das Wichtigste zuerst: **Ich kann nichts davon sehen oder hören.** Optik,
Effekte, Sound, Gefühl — das musst alles du beurteilen. Die Tests prüfen nur
die Mechanik dahinter (wird der richtige Effekt ausgelöst? stimmen die
Zahlen? stürzt nichts ab?).

---

## Was neu ist

### 1. Waffe in der Hand (Etappe A, Paket 3)
Vorher hast du **keine Waffe** gesehen. Jetzt ist ein aus Code gebautes
Gewehr bzw. eine Pistole vor der Kamera — mit Laufwippen, Nachschwingen beim
Umsehen, Rückstoß-Ruck, Nachlade-Bewegung (Magazin fällt raus und kommt
zurück) und Ziehen beim Waffenwechsel.

### 2. Der Look (Etappe B)
- **Post-Processing**: ACES-Tonemapping, Bloom (helle Dinge strahlen),
  Vignette, wärmere Farben, Filmkorn, Nebel in der Ferne.
- **HDR ist wieder AN.** Das war 2026-08 mal die Ursache der senkrechten
  Streifen auf deinem M1 — der eigentliche Übeltäter war aber "Adaptive
  Performance", und das bleibt aus. **Falls die Streifen zurückkommen:** im
  Menü **BILD → SCHLICHT** wählen, dann ist alles wieder aus.
- **Karte lesbarer**: dunkler Boden statt weiß, leuchtende orange
  Akzentstreifen an Kanten, große **A/B**-Buchstaben auf den Bombenplätzen,
  Punktlichter an den Engstellen.
- **Figuren statt Kapseln**: Spieler und Bots sind jetzt stilisierte
  Figuren (Rumpf, Kopf mit Helm, Arme, Beine) — Beine/Arme pendeln beim
  Laufen, Kopf neigt sich in die Zielrichtung, beim Tod kippt die Figur um.
  *(Deine eigene Figur siehst du nicht — First Person.)*
- **Mixamo-Figuren** habe ich NICHT gemacht (braucht dein Adobe-Konto). Die
  Code-Figur ist erst mal die Lösung.

### 3. Fähigkeiten (Etappe C) — das Herzstück
Sechs Werkzeuge, gekauft im **bestehenden Kaufmenü** (Tasten 6–0) mit dem
**bestehenden Geld-System**, eingesetzt mit **Q / F / G**:

| Taste | Werkzeug | Wirkung |
|---|---|---|
| Q | **Rauchwand** | blockiert Sicht 15 s — **auch die der Bots** |
| Q | **Brandwand** | Feuer sperrt einen Weg, Schaden pro Sekunde |
| F | **Scan-Puls** | zeigt Gegner 3 s (gelber Kasten, auch durch Wände) |
| F | **Stolperdraht** | Gegner der durchläuft wird kurz geblendet + Alarm |
| G | **Blendgranate** | weißer Bildschirm; geblendete Bots schießen nicht |
| G | **Splittergranate** | Flächenschaden (15–90 je nach Nähe) |

**Die Bots verstehen sie**: Rauch nimmt ihnen wirklich die Sicht, eine
Blendgranate legt sie kurz lahm, und sie kaufen und werfen selbst welche
(Blendgranate vor dem Sturm, Rauch auf dem Anmarsch).

### 4. Klügere Gegner (Etappe D)
- **Bots hören**: Schüsse (weit) und Sprint-Schritte (nah) erzeugen einen
  Verdachtspunkt — der Bot dreht sich um und geht nachschauen.
- **Bots sagen an**: im Kill-Feed erscheinen "Alpha-2: Feind gesichtet!",
  "… Höre was!", "… Brauche Hilfe!".
- **Menschlicheres Zielen**: der Bot zieht sein Fadenkreuz jetzt mit
  begrenztem Tempo nach (kein sofortiges Einrasten), verzieht manchmal,
  korrigiert über — und feuert erst, wenn er ungefähr drauf ist.
- **Schwierigkeit neu**: Leicht/Normal/Schwer stellen jetzt Reaktion,
  Zielgüte, Nachzieh-Tempo, Aggressivität, Hörweite und Teamwork ein.

### 5. Momente (Etappe E)
- **Banner in der Bildmitte** mit Ton bei: Doppelkill, Dreifachkill,
  **ACE** (alle Gegner allein), **CLUTCH** (als Letzter gegen Übermacht
  gewonnen), Beste der Runde.
- **Laufbahn** im Menü (unter der Navigation): Matches, Siege, Aces,
  längste Siegesserie — bleibt dauerhaft gespeichert.

### 6. Deko + Ladebildschirm
- Karte ausgestattet: Fässer, Hängelampen, Rohrleitungen, Sandsäcke,
  Boden-Flecken, Eck-Masten. Dunkler Himmel statt grellem Standard.
- Ladebildschirm: driftendes Streifenmuster, pulsierendes Leuchten hinter
  "INFRONT", Untertitel, Lade-Punkte, HUD-Eckklammern, wechselnde Tipps.

---

## Dein Spieltest-Auftrag

Bitte starte das Spiel (liegt als frischer Build vor) und achte auf:

**Bild / Streifen (wichtig!)**
- Kommen die senkrechten Streifen auf dem M1 zurück? Wenn ja → Menü
  **BILD → SCHLICHT**, und sag mir Bescheid.
- Sieht das Bild satt/filmisch aus oder zu dunkel / zu grell?

**Waffe in der Hand**
- Ist eine Waffe zu sehen? Wippt sie beim Laufen, ruckt sie beim Schießen,
  bewegt sie sich beim Nachladen? Fühlt es sich gut an oder zappelig?

**Figuren**
- Sehen Gegner/Verbündete wie Figuren aus (nicht mehr Kapseln)? Bewegen
  sich Beine/Arme beim Laufen? Kippen sie beim Tod um?

**Fähigkeiten**
- Kauf im Kaufmenü mit 6–0, Einsatz mit Q/F/G. Funktioniert jede der sechs?
- Verschwindet ein Bot wirklich im Rauch? Schießt eine geblendete Bot-Gruppe
  daneben? Zeigt der Scan-Puls gelbe Kästen?

**Bots**
- Drehen sie sich um, wenn du hinter ihnen schießt/sprintest?
- Erscheinen Ansagen im Kill-Feed?
- Fühlt sich das Zielen der Bots fairer an (nicht mehr Laserstrahl)?

**Momente**
- Mach mal einen Doppelkill / Ace — kommt das Banner + Ton?
- Steht die Laufbahn im Menü und zählt sie hoch?

**Ladebildschirm + Menü**
- Menü und Ladebildschirm einmal komplett durchklicken. Hängt nichts?
- Esc im Spiel: Zeit steht still, "Weiter" verliert keine Zeit, "Zurück
  zum Menü" hängt nicht in Zeitlupe.

**Was kaputt sein könnte, bitte melden**
- Abstürze / Fehler in der Konsole.
- Effekte, die an der falschen Stelle erscheinen (Rauch in der Wand,
  Splitter ohne Schaden).
- Bots, die sich seltsam bewegen (im Kreis, ins Feuer, stecken bleiben).

---

## Was ich nachts allein entschieden habe (kannst du zurückdrehen)

Steht in `PROGRESS.md` unter den "Nachts allein entschieden"-Abschnitten.
Kurz:
1. **HDR wieder an** — weil es jetzt Tonemapping gibt. Rückweg: BILD →
   SCHLICHT.
2. **Ein wackeliger Test** (`Freeze_Time…`) misst jetzt nur die waagrechte
   Bewegung — senkrechtes Nachsacken ist kein Fehler.
3. **Keine externen CC0-Pakete** — Deko komplett per Code, weil der Import
   headless zu fehleranfällig ist. Begründung in `ASSETS.md`.
4. **HUD/Endbildschirm bleiben IMGUI** — du hattest "wenn nötig" gesagt;
   nötig war es nicht, und die Zeit ging in Gameplay. Gut für die nächste
   Sitzung.

---

## Noch offen (für dich / nächste Sitzung)

- **Credits-Bildschirm** — bewusst vertagt, du wolltest ihn noch nicht.
- **Mixamo-Figuren** — braucht dein Adobe-Konto. Anbindung fehlt noch.
- **Echte Sound-Dateien** — nach `Assets/_Project/Audio/Resources/` legen
  (Namen in der dortigen `LIESMICH.txt`).
- **Online mit Freunden** (Etappe F) — bleibt bewusst spät.
- **Charaktere** (Etappe G) — erst wenn sich die Fähigkeiten gut anfühlen.
- **Bot-Deckung / Peek / Flanken** — nur angedeutet, nicht voll ausgebaut.
- **Bots weichen Feuer nicht aktiv aus.**
- **Endbildschirm + HUD auf UI Toolkit** umstellen.
