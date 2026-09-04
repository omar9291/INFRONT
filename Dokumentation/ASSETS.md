# ASSETS.md — Verwendete Asset-Pakete und Lizenzen

Letzte Aktualisierung: 2026-08-29

Jedes importierte Asset-Paket wird hier eingetragen, BEVOR es benutzt wird:
Name, Quelle, Lizenztyp, ob kommerzielle Nutzung erlaubt ist.

## Regel

Vor Nutzung pruefen: Erlaubt die Lizenz kommerzielle Nutzung? Das Spiel
koennte spaeter unter der Marke "Driftlab" auf itch.io veroeffentlicht
werden. Wenn ein Asset das nicht abdeckt: hier vermerken UND den Nutzer
warnen, bevor es eingebaut wird.

Die meisten Unity-Asset-Store-Pakete laufen unter der "Unity Extension
Asset EULA" oder "Standard Unity Asset Store EULA" — beide erlauben die
Nutzung im fertigen Spiel, auch kommerziell. Was NICHT geht: das Asset
selbst weiterverkaufen oder einzeln weitergeben.

## Version 2 (2026-09-01) — erste echte CC0-Pakete

Nacht-8-Entscheidung ("nur Code") wurde auf Wunsch des Nutzers zurueckgenommen.
Stil-Richtung: realistisch. Alles laeuft ueber `AssetLibrary` mit Rueckfall auf
die Code-Geometrie - fehlt eine Datei, sieht das Spiel aus wie vorher.

Alle folgenden Pakete: **CC0 (Public Domain), kommerzielle Nutzung erlaubt,
keine Namensnennung noetig.** Damit sauber fuer eine spaetere Veroeffentlichung
unter "Driftlab" auf itch.io.

| Paket | Quelle | Lizenz | Wofuer | Eingetragen |
|-------|--------|--------|--------|-------------|
| Concrete034, Concrete016, Asphalt031, Metal046A, PavingStones128 (1K JPG) | ambientcg.com | CC0 | Wand-/Boden-/Deckungs-/Platz-Texturen (P2) | 2026-09-01 |
| industrial_sunset_02 (2K HDRI) | polyhaven.com | CC0 | HDRI-Himmel + Umgebungslicht (P3) | 2026-09-01 |
| Barrel_01, ammo_box, wooden_military_crate, metal_jerrycan_green, modular_industrial_pipes_01, hanging_industrial_lamp, cement_bag (1K FBX) | polyhaven.com | CC0 | Deko-Modelle statt Grundkoerper (P4) | 2026-09-01 |
| concrete_road_barrier, concrete_road_barrier_02, metal_trash_can, old_tyre, hand_truck, industrial_storage_cart (1K FBX + diff/nor) | polyhaven.com | CC0 | Betonbarrieren als echte Deckung + Deko, "realistischer Look" | 2026-09-04 |
| caged_hanging_light, security_light, mounted_fluorescent_lights, rollershutter_door, overhead_crane (1K FBX + diff/nor) | polyhaven.com | CC0 | Runde 2 "realistischer Look": Kaefiglampen ersetzen die Code-Wuerfel, Rolltore an den Aussenwaenden, Hallenkran ueber der Mittelachse, Wand- und Deckenleuchten. Alles rein optisch, ohne Collider. | 2026-09-03 |
| schuss_gewehr.wav (AK-47), schuss_mp.wav (Carl Gustav M45), schuss_sniper.wav (Mosin Nagant), schuss_pistole.wav (1911) | opengameart.org "The Free Firearm Sound Library" | CC0 | Echte Schussaufnahmen statt der prozeduralen Platzhalter. Aus 96 kHz/24 bit Stereo auf 44.1 kHz/16 bit Mono gewandelt, auf einen Einzelschuss zugeschnitten, ausgeblendet und auf gleiche Lautstaerke gebracht. Aufgenommen von Ben Jaszczak, Brian Nelson, Kevin Heras, Matthew Nanney - CC0, keine Namensnennung noetig. | 2026-09-04 |

Ablage:
- Roh-Downloads: `Assets/_Project/Art/Textures/`, `Art/Sky/`, `Art/Models/`
- Erzeugte Materialien/Prefabs: `Assets/_Project/Art/Resources/Materials|Models/`
  (das laedt `AssetLibrary` per `Resources.Load`)
- Erzeugung: `AssetImporterTools` (Menue "Infront/Assets/...") - laeuft auch
  automatisch in `SceneBuilder.Build`.

Rueckweg: Datei unter `Art/Textures|Sky|Models` loeschen, `SceneBuilder.Build`
neu laufen - dann ist die Code-Geometrie zurueck.

### Noch offen
- P5 Waffen: nur `service_pistol` + `bolt_action_rifle_7_62` bei Poly Haven -
  Gewehr/MP bleiben Code.
- P6 Sounds: "The Free Firearm Sound Library" (OpenGameArt, CC0) - liegt nur als
  .7z vor, Entpacker fehlt auf diesem Rechner.
- P7 Figuren: CC0-ohne-Login gibt es keine animierten Figuren. Weg: Mixamo
  (gratis, kommerziell ok) - braucht Adobe-Login, den nur der Nutzer machen kann.

### Nacht 8 (2026-09-01) — Entscheidung zu CC0-Paketen

Der Nutzer hatte CC0-Direktdownloads ohne Login erlaubt. Im autonomen
Nacht-Lauf wurde bewusst DARAUF VERZICHTET: das Einbinden externer
3D-/Textur-Pakete headless (Download, Entpacken, Import, Material-Zuordnung)
ist fehleranfaellig und schlecht automatisiert pruefbar. Stattdessen wurde
die Deko komplett per Code gebaut (SceneBuilder.BuildDecoration:
Faesser, Haengelampen, Rohre, Sandsaecke, Boden-Flecken, Masten) plus ein
dunkler prozeduraler Himmel (Skybox/Procedural, ArenaSky.mat).

Wenn spaeter echte CC0-Pakete rein sollen: hier eintragen (Name, Quelle,
Lizenz, kommerziell ja/nein), Modelle nach Assets/_Project/Art/ legen,
und die Deko-/Figur-Bauteile lesen sie per Resources/Pfad statt der
Code-Geometrie.

## Unity-Pakete (Package Manager, keine Store-Assets)

| Paket | Version | Zweck | Lizenz |
|-------|---------|-------|--------|
| com.unity.render-pipelines.universal | 17.6.0 | Render-Pipeline (URP) | Unity Companion License |
| com.unity.netcode.gameobjects | 2.13.2 | Netzwerk (server-autoritativ) | Unity Companion License |
| com.unity.inputsystem | 1.20.0 | Eingabe (Tastatur/Maus/Gamepad) | Unity Companion License |
| com.unity.ai.navigation | 2.0.14 | NavMesh fuer Bots (ab Phase 3) | Unity Companion License |
| com.unity.test-framework | 1.7.0 | PlayMode-Tests | Unity Companion License |

Alle unter Unity Companion License: im fertigen Spiel frei nutzbar, auch
kommerziell. Stand: installiert in Phase 1 (2026-08-29).

## Spaeter geplante Asset-Kategorien

Wenn es an Grafik geht (Spaeter-Stufe 4), werden hier Pakete fuer
Charaktermodelle, Waffen, Umgebung und Animationen eingetragen. Jedes
einzeln mit Lizenzpruefung.
