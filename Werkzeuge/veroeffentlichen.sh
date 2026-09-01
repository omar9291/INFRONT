#!/bin/bash
#
# veroeffentlichen.sh — baut INFRONT fuer Mac und Windows und packt fertige ZIPs,
# die man auf itch.io hochladen oder direkt verschicken kann.
#
# Aufruf (im Projekt-Ordner):   Werkzeuge/veroeffentlichen.sh
#
# Der Unity-Editor MUSS vorher geschlossen sein — sonst ist das Projekt gesperrt.
# Windows wird nur gebaut, wenn das Modul "Windows Build Support (Mono)" im
# Unity Hub installiert ist. Fehlt es, laeuft der Rest trotzdem durch.

set -e

# --- Pfade ---
PROJECT="$(cd "$(dirname "$0")/.." && pwd)"
UNITY="/Applications/Unity/Hub/Editor/6000.5.8f1/Unity.app/Contents/MacOS/Unity"
DIST="$PROJECT/Builds/dist"

cd "$PROJECT"

# --- Editor offen? ---
if [ -f "$PROJECT/Temp/UnityLockfile" ] && pgrep -f "Unity.app/Contents/MacOS/Unity .*INFRONT" > /dev/null; then
  echo "FEHLER: Der Unity-Editor hat das Projekt noch offen."
  echo "        Bitte den Editor schliessen (Cmd+Q) und nochmal starten."
  exit 1
fi

if [ ! -x "$UNITY" ]; then
  echo "FEHLER: Unity-Editor nicht gefunden unter:"
  echo "        $UNITY"
  exit 1
fi

# --- Versionsnummer aus den Projekt-Einstellungen lesen ---
VERSION="$(grep -m1 'bundleVersion:' ProjectSettings/ProjectSettings.asset | sed 's/.*bundleVersion: *//' | tr -d '[:space:]')"
[ -z "$VERSION" ] && VERSION="0"
echo "==> INFRONT v$VERSION wird gebaut"
echo ""

mkdir -p "$DIST"
mkdir -p "$PROJECT/Logs"

# --- Mac bauen ---
echo "==> macOS-App bauen (dauert ein paar Minuten) ..."
"$UNITY" -batchmode -quit \
  -projectPath "$PROJECT" \
  -executeMethod Infront.EditorTools.GameBuilder.BuildMac \
  -logFile "$PROJECT/Logs/build-mac.log"

if [ -d "$PROJECT/Builds/INFRONT.app" ]; then
  MAC_ZIP="$DIST/INFRONT-mac-v$VERSION.zip"
  rm -f "$MAC_ZIP"
  # ditto statt zip: behaelt die App-Struktur und die Ausfuehr-Rechte
  ditto -c -k --keepParent "$PROJECT/Builds/INFRONT.app" "$MAC_ZIP"
  echo "    fertig:  $MAC_ZIP  ($(du -h "$MAC_ZIP" | cut -f1))"
else
  echo "    FEHLER: Mac-App wurde nicht gebaut. Siehe Logs/build-mac.log"
  exit 1
fi
echo ""

# --- Windows bauen (optional) ---
echo "==> Windows-App bauen ..."
set +e
"$UNITY" -batchmode -quit \
  -projectPath "$PROJECT" \
  -executeMethod Infront.EditorTools.GameBuilder.BuildWindows \
  -logFile "$PROJECT/Logs/build-win.log"
set -e

if [ -f "$PROJECT/Builds/INFRONT-win/INFRONT.exe" ]; then
  WIN_ZIP="$DIST/INFRONT-windows-v$VERSION.zip"
  rm -f "$WIN_ZIP"
  ( cd "$PROJECT/Builds" && zip -r -q "$WIN_ZIP" "INFRONT-win" )
  echo "    fertig:  $WIN_ZIP  ($(du -h "$WIN_ZIP" | cut -f1))"
else
  echo "    uebersprungen: Windows-Modul fehlt im Unity Hub."
  echo "                   (Unity Hub -> Installs -> 6000.5.8f1 -> Add Modules"
  echo "                    -> 'Windows Build Support (Mono)')"
fi
echo ""

echo "========================================================"
echo " Fertig. Die ZIPs liegen in:"
echo "   $DIST"
echo ""
echo " Naechster Schritt: auf itch.io hochladen"
echo "   1. https://driftlab.itch.io/dashboard  ->  INFRONT bearbeiten"
echo "   2. Alte Dateien loeschen, neue ZIPs hochladen"
echo "   3. Bei Mac-ZIP 'macOS' ankreuzen, bei Windows-ZIP 'Windows'"
echo "   4. 'Save & view page'"
echo "========================================================"
