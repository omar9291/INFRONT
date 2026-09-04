# Lizenzen — Stand 2026-09-04

Diese Datei beantwortet eine Frage: **darf INFRONT so, wie es jetzt ist,
veröffentlicht werden?** Kurz: fast. Ein Punkt ist offen, und den kann nur
der Nutzer entscheiden.

## Der Code

Der gesamte Code in `Assets/_Project/Code/` stammt aus diesem Projekt
(Driftlab). Es gibt bisher **keine Lizenzdatei im Repo**. Das heisst
rechtlich: alle Rechte vorbehalten, niemand darf den Code benutzen. Für ein
öffentliches Repo ist das ungewöhnlich, aber nicht falsch — es ist einfach der
Standard, wenn nichts dabeisteht.

**Zu entscheiden:** soll der Code unter eine Lizenz (z. B. MIT), oder bewusst
„alle Rechte vorbehalten" bleiben? Bei einem Portfolio-Stück, das Leute
anschauen aber nicht kopieren sollen, ist Letzteres völlig in Ordnung.

## Die Assets

| Quelle | Lizenz | Namensnennung | Weitergabe der Dateien |
|---|---|---|---|
| Poly Haven | CC0 | nicht nötig | erlaubt |
| ambientCG | CC0 | nicht nötig | erlaubt |
| The Free Firearm Sound Library | CC0 | nicht nötig | erlaubt |
| **Mixamo (Adobe)** | **Mixamo-Bedingungen** | — | **NICHT erlaubt** |

Alle bis auf Mixamo sind unproblematisch. Genannt werden sie trotzdem, im
Hauptmenü unter **QUELLEN**.

## Der offene Punkt: Mixamo im öffentlichen Repo

**Das Problem.** Mixamo erlaubt, Figuren und Animationen in einem Spiel zu
benutzen — auch geschäftlich, ohne Gebühr. Was nicht erlaubt ist: die Dateien
**als Dateien** weiterzugeben. Sie dürfen nur *eingebaut in ein Produkt*
verbreitet werden.

Im Repo `github.com/omar9291/INFRONT` liegen aber genau diese fünf Dateien
offen herum:

```
Assets/_Project/Art/Figures/basis.fbx
Assets/_Project/Art/Figures/idle.fbx
Assets/_Project/Art/Figures/walk.fbx
Assets/_Project/Art/Figures/run.fbx
Assets/_Project/Art/Figures/death.fbx
```

Jeder kann sie einzeln herunterladen, ohne das Spiel zu benutzen. Das ist
näher an „Weitergabe der Dateien" als an „eingebaut in ein Produkt".

**Wie schlimm ist das wirklich?** Es ist eine Grauzone, kein eindeutiger
Verstoss. Adobe geht erfahrungsgemäss nicht gegen kleine Hobbyprojekte vor, und
in unzähligen öffentlichen Repos liegen Mixamo-Dateien. Trotzdem: die fertig
gebaute App weiterzugeben ist eindeutig erlaubt, das Repo mit den Rohdateien
nicht eindeutig. Wenn man es sauber haben will, gehören sie da nicht hin.

**Drei Möglichkeiten:**

1. **So lassen.** Risiko sehr klein, Aufwand null. Ehrlich gesagt das, was die
   meisten machen.
2. **Ab jetzt nicht mehr mitschicken.** Die Dateien bleiben auf der Platte und
   das Spiel funktioniert weiter, aber Git verfolgt sie nicht mehr
   (`git rm --cached` plus `.gitignore`). Sie bleiben allerdings in der alten
   Versionsgeschichte auffindbar. Aufwand: klein. Rückgängig machbar.
3. **Ganz aus der Geschichte tilgen.** Wirklich sauber, aber es schreibt die
   öffentliche Versionsgeschichte um. Alle vorhandenen Kopien des Repos passen
   danach nicht mehr dazu. **Das sollte nur passieren, wenn der Nutzer es
   ausdrücklich will.**

Was sich **nicht** ändert: die veröffentlichte Spieldatei (`INFRONT.app`,
später der itch.io-Download) ist in jedem Fall in Ordnung. Dort sind die
Figuren eingebaut, genau so wie Mixamo es vorsieht.

## Veröffentlichen auf itch.io

Erlaubt, mit einer Einschränkung, die nichts mit Lizenzen zu tun hat: itch.io
verlangt für ein eigenes Konto **18 Jahre oder die Zustimmung der Eltern**.
Die liegt vor (das Konto „Driftlab" besteht bereits und hat Snake und
Platformer veröffentlicht).

## Regel für die Zukunft

Bei jeder neuen Quelle:

1. **Die Lizenzdatei IM Paket lesen**, nicht nur die Angabe auf der Webseite.
   Am 2026-09-04 stand auf einer opengameart-Seite „CC0", im ZIP aber
   `Copyright (c) 2009 Vincent Sevedge, CC-BY 3.0` — mit einem anderen Namen
   als dem des Hochladenden. Das Paket wurde deshalb verworfen.
2. Zeile in `Dokumentation/ASSETS.md` eintragen, mit der echten Lizenz.
3. Eintrag in `MainMenuUi.BuildQuellen` ergänzen, damit es im Spiel steht.
4. Bei irgendeinem Zweifel: hier vermerken statt hoffen.
