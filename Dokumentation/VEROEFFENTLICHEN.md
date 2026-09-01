# INFRONT veröffentlichen — für Freunde zum Mittesten

Ziel: nach jeder Arbeitseinheit die neuste spielbare Version online, **nur über einen
Link erreichbar** (nicht öffentlich gelistet), damit Freunde sie direkt testen können.

## Zielplattform

**Windows-PC** ist die Hauptplattform. Begründung:

- INFRONT ist ein Ego-Shooter mit Maus-Zielen — das funktioniert nur mit Maus + Tastatur.
- Auf itch.io laden die meisten Leute die Windows-Version herunter.
- **Mac** fällt gratis ab, weil hier auf dem Mac entwickelt wird (der iMac-Freund kann sofort testen).
- **Tablet / Handy** geht nicht: kein Download möglich, keine Touch-Steuerung im Spiel.
  Wer nur ein Tablet hat, kann diese Runde leider nicht mittesten.

## Einmal einrichten

### 1. Windows-Modul im Unity Hub nachinstallieren

Ohne dieses Modul kann nur die Mac-Version gebaut werden.

1. Unity Hub öffnen → **Installs**
2. Bei Version **6000.5.8f1** auf das Zahnrad → **Add modules**
3. **Windows Build Support (Mono)** ankreuzen → Install (ca. 2 GB, einmalig)

### 2. itch.io-Seite anlegen

1. Auf https://itch.io einloggen (Driftlab-Account)
2. Oben rechts → **Upload new project**
3. Ausfüllen:
   - **Title:** `INFRONT`
   - **Project URL:** `infront` → wird zu `https://driftlab.itch.io/infront`
   - **Classification:** Game
   - **Kind of project:** Downloadable
   - **Pricing:** `No payments` (kostenlos)
4. Ganz unten **Visibility & access:** auf **Draft** lassen.
   - Oben auf der Seite steht dann ein geheimer Link:
     *„Anyone with this link can view the page"*
   - **Diesen Link** kopieren und den Freunden schicken. Nur wer den Link hat, kommt rein.
   - Solange die Seite auf *Draft* steht, findet sie niemand über die Suche.
5. **Save**

Später, wenn INFRONT wirklich fertig ist und öffentlich soll: Visibility auf **Public**
stellen. (Bis dahin bleibt alles privat.)

## Nach jeder Arbeitseinheit — neue Version hochladen

### Schritt 1: bauen (macht Claude oder du selbst)

Unity-Editor **schließen** (Cmd+Q), dann im Projektordner:

```bash
Werkzeuge/veroeffentlichen.sh
```

Das baut Mac + Windows und legt fertige ZIPs in `Builds/dist/`:

- `INFRONT-mac-v1.0.zip`
- `INFRONT-windows-v1.0.zip`

(Die Versionsnummer kommt aus den Projekt-Einstellungen. Zum Hochzählen:
Unity → *Edit → Project Settings → Player → Version*.)

### Schritt 2: hochladen (musst du machen)

1. https://driftlab.itch.io/dashboard → **INFRONT** → **Edit**
2. Runter zu **Uploads**
3. Alte ZIPs löschen (Mülleimer-Symbol), neue hochziehen
4. Bei jedem Upload die Plattform ankreuzen:
   - `INFRONT-mac-...zip` → **macOS** ✔
   - `INFRONT-windows-...zip` → **Windows** ✔
5. **Save**

Der geheime Link bleibt gleich — die Freunde müssen ihn nur einmal bekommen.

## Was die Freunde wissen müssen

**Windows:** ZIP herunterladen, entpacken, `INFRONT.exe` starten.
Beim ersten Start meldet Windows „Der Computer wurde geschützt" → *Weitere Informationen*
→ *Trotzdem ausführen*. (Normal bei nicht-signierten Spielen.)

**Mac:** ZIP herunterladen, entpacken, dann **Rechtsklick auf `INFRONT.app` → Öffnen**
(nicht doppelklicken!). Einmal „Öffnen" bestätigen. Danach startet sie normal.
Falls macOS ganz blockt, hilft im Terminal:

```bash
xattr -dr com.apple.quarantine ~/Downloads/INFRONT.app
```

## Später: Hochladen automatisieren (butler)

itch.io hat ein Kommandozeilen-Werkzeug `butler`, mit dem der Upload ein einziger Befehl
wird. Das braucht einen API-Key aus dem itch.io-Account. Wenn das eingerichtet ist, kann
Claude auch das Hochladen übernehmen. Bis dahin: von Hand über die Website (dauert ~1 Minute).
