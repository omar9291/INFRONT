using System.Globalization;

namespace Infront
{
    /// <summary>
    /// Die englischen Anzeigetexte des Spiels an einer Stelle. Die Member-Namen
    /// sind stabile Text-Schluessel; sie koennen spaeter weitere Sprachkataloge
    /// aufloesen, ohne UI, Speicherdateien oder Asset-IDs umzubenennen.
    ///
    /// Nur Text fuer Spieler gehoert hierher: keine Selektoren, Resources-Pfade,
    /// PlayerPrefs-Schluessel, Debug-Ausgaben oder frei gewaehlten Spielernamen.
    /// Ganze Saetze verwenden Format-Platzhalter, damit eine andere Sprache
    /// die Wortfolge aendern kann. Quellenangaben bleiben im Credits-Katalog.
    /// </summary>
    public static class GameText
    {
        public const string LanguageCode = "en";

        // Die haeufigen HUD-Faelle brauchen kein neues params-Array pro Frame.
        public static string Format(string template, object value) =>
            string.Format(CultureInfo.CurrentCulture, template, value);
        public static string Format(string template, object first, object second) =>
            string.Format(CultureInfo.CurrentCulture, template, first, second);
        public static string Format(string template, object first, object second, object third) =>
            string.Format(CultureInfo.CurrentCulture, template, first, second, third);

        public static class Menu
        {
            public const string HeadshotTip = "Headshots do double damage.";
            public const string BombInteractionTip = "Hold E to plant or defuse the bomb.";
            public const string RoundDeathTip = "If you die, you stay dead for the round - no respawn.";
            public const string SurvivorGearTip = "Survivors keep their weapon and armor.";
            public const string BuyMenuTip = "Press B at the start of a round to open the buy menu.";
            public const string ArmorTip = "Body armor absorbs half of all body damage.";
            public const string HalftimeTip = "Sides are swapped after 15 rounds.";
            public const string You = "YOU";
            public const string Tagline = "TACTICAL SHOOTER   ·   ROUND-BASED   ·   HOST MODE";
            public const string StudioPrefix = "DRIFTLAB   ·   ";
            public const string Career = "CAREER";
            public const string Matches = "Matches";
            public const string Wins = "Wins";
            public const string Aces = "Aces";
            public const string BestStreak = "Best Streak";
            public const string Play = "PLAY";
            public const string Settings = "SETTINGS";
            public const string Accessibility = "ACCESSIBILITY";
            public const string YourData = "YOUR DATA";
            public const string Controls = "CONTROLS";
            public const string Credits = "CREDITS";
            public const string Quit = "Quit";
            public const string Tip = "TIP";
            public const string HostReady = "SYSTEM READY   ·   HOST";
            public const string LegacyMenuHint = "F10  –  LEGACY MENU";
            public const string HostModeHint = "HOST MODE  ·  SINGLE PLAYER VS BOTS";
            public const string GameMode = "GAME MODE";
            public const string Elimination = "ELIMINATION";
            public const string EliminationDescription = "Wipe out the enemy team and the round is yours.";
            public const string Bomb = "BOMB";
            public const string BombDescription = "Plant the bomb and hold the site - or stop the plant and defuse.";
            public const string TeamSize = "TEAM SIZE";
            public const string BotSkill = "BOT SKILL";
            public const string Easy = "EASY";
            public const string Normal = "NORMAL";
            public const string Hard = "HARD";
            public const string StartRound = "▶   START ROUND";
            public const string Lineup = "LINEUP";
            public const string Netcode = "NETCODE";
            public const string HostAuthoritative = "HOST-AUTHORITATIVE";
            public const string Tickrate = "TICKRATE";
            public const string Region = "REGION";
            public const string Local = "LOCAL";
            public const string YourTeam = "YOUR TEAM";
            public const string Enemy = "ENEMY";
            public const string Briefing = "{0}   ·   BOTS: {1}";
            public const string MatchSummary = "{0}   ·   {1} VS {1}   ·   BOTS {2}";
            public const string DataDescription = "INFRONT sends nothing anywhere. No account, no server, no sign-in. Everything the game knows about you sits in files on this computer - and you can look at them and delete them.";
            public const string WhatIsSaved = "WHAT IS SAVED";
            public const string ProfileDescription = "Your name and whether the intro is done.";
            public const string StatisticsDescription = "Totals: matches, shots, hits, time played.";
            public const string CrashReportsDescription = "Crash reports, if the game ever crashes. Currently: {0}.";
            public const string SettingsLabel = "Settings";
            public const string SettingsDescription = "Volume, sensitivity, accessibility.";
            public const string OpenFolder = "OPEN FOLDER";
            public const string WhatIsNotSaved = "WHAT IS NOT SAVED";
            public const string NoEmailAddress = "No email address";
            public const string ThereIsNoSignIn = "There is no sign-in.";
            public const string NoPassword = "No password";
            public const string NotEvenOneToReset = "Not even one to reset.";
            public const string NoTimestamps = "No timestamps";
            public const string TotalsOnlyNoRecordOfSingleMatches = "Totals only, no record of single matches.";
            public const string NoTransmission = "No transmission";
            public const string TheGameOpensNoOutboundConnection = "The game opens no outbound connection.";
            public const string YourNumbers = "YOUR NUMBERS";
            public const string MatchesHeading = "MATCHES";
            public const string OfThoseWon = "OF THOSE WON";
            public const string Rounds = "ROUNDS";
            public const string Shots = "SHOTS";
            public const string Accuracy = "ACCURACY";
            public const string OfThoseHeadshots = "OF THOSE HEADSHOTS";
            public const string KillsPerDeath = "KILLS PER DEATH";
            public const string TimePlayed = "TIME PLAYED";
            public const string Delete = "DELETE";
            public const string DeleteCrashReports = "DELETE CRASH REPORTS";
            public const string ReallyDeleteEverything = "REALLY DELETE EVERYTHING?";
            public const string DeleteEverything = "DELETE EVERYTHING";
            public const string DeleteConfirmation = "Press again to delete profile, numbers, career and reports. This cannot be undone.";
            public const string DeleteDescription = "Deletes profile, numbers, career and crash reports.";
            public const string AccessibilityDescription = "These settings do not change the difficulty. They only change how the game looks and how it is operated.";
            public const string InterfaceSize = "INTERFACE SIZE";
            public const string InterfaceScaleDescription = "Scales the menu and the in-game display together.";
            public const string Crosshair = "CROSSHAIR";
            public const string CrosshairDescription = "Bigger and thicker. A crosshair you cannot see makes the whole game unplayable.";
            public const string Colors = "COLORS";
            public const string Default = "DEFAULT";
            public const string RedGreen = "RED-GREEN";
            public const string BlueYellow = "BLUE-YELLOW";
            public const string Contrast = "CONTRAST";
            public const string ColorsDescription = "Mainly affects the health bar. Green-yellow-red runs together with red-green color blindness; it then becomes blue-yellow-magenta.";
            public const string ReduceMotion = "REDUCE MOTION";
            public const string Off = "OFF";
            public const string On = "ON";
            public const string MotionDescription = "Strongly damps breathing sway and weapon bob. If the picture makes you feel sick, that is not on you - switch this on.";
            public const string HoldOrToggle = "HOLD OR TOGGLE";
            public const string Aim = "AIM";
            public const string Crouch = "CROUCH";
            public const string Sprint = "SPRINT";
            public const string ToggleDescription = "Holding a key down forever hurts after a while, and with one hand it does not work at all. The keys stay the same.";
            public const string Hold = "HOLD";
            public const string Toggle = "TOGGLE";
            public const string Display = "DISPLAY";
            public const string Fullscreen = "FULLSCREEN";
            public const string Windowed = "WINDOWED";
            public const string DisplayDescription = "Fullscreen: borderless window at screen size. Windowed: 1280×720, in case you want to switch to the desktop quickly.";
            public const string Graphics = "GRAPHICS";
            public const string Full = "FULL";
            public const string Plain = "PLAIN";
            public const string GraphicsDescription = "Full: with depth of field, bloom, vignette and fog. Plain: everything off, in case it stutters or smears.";
            public const string MouseSensitivity = "MOUSE SENSITIVITY";
            public const string Volume = "VOLUME";
            public const string KeyBindings = "KEY BINDINGS";
            public const string Move = "Move";
            public const string Look = "Look";
            public const string Mouse = "Mouse";
            public const string Fire = "Fire";
            public const string LeftMouseButton = "Left mouse button";
            public const string AimHold = "Aim (hold)";
            public const string RightMouseButton = "Right mouse button";
            public const string CrouchHold = "Crouch (hold)";
            public const string WalkQuietlyHold = "Walk quietly (hold)";
            public const string Reload = "Reload";
            public const string Jump = "Jump";
            public const string SprintHold = "Sprint (hold)";
            public const string HoldBreathWhileScoped = "Hold breath (while scoped)";
            public const string SwitchWeapon = "Switch weapon";
            public const string PlantDefuseBombHold = "Plant / defuse bomb (hold)";
            public const string BuyMenu = "Buy menu";
            public const string ScoreboardHold = "Scoreboard (hold)";
            public const string Pause = "Pause";
            public const string SwitchSpectatorTargetDead = "Switch spectator target (dead)";
            public const string LeftClick = "Left click";
            public const string RightClick = "Right click";
            public const string ReallyQuitTheGame = "Really quit the game?";
            public const string YesQuit = "YES, QUIT";
            public const string Back = "BACK";
        }

        public static class Loading
        {
            public const string RoundDeathTip = "If you die, you stay dead - there is no respawn mid-round.";
            public const string SurvivorGearTip = "Survivors carry their weapon and armor into the next round.";
            public const string SpectatorTip = "When dead, left and right click switch which teammate you watch.";
            public const string Arena = "ARENA";
            public const string Preparing = "PREPARING";
            public const string Tagline = "TACTICAL TEAM SHOOTER";
            public const string Label = "LOADING";
            public const string Driftlab = "DRIFTLAB";
            public const string EmptyContext = "ARENA   ·   -";
            public const string MainMenu = "MAIN MENU";
            public const string Disconnecting = "DISCONNECTING";
            public const string LoadingMap = "LOADING MAP";
            public const string PlacingEnemies = "PLACING ENEMIES";
            public const string Ready = "READY";
            public const string Start = "START";
            public const string ReadingProfile = "READING PROFILE";
            public const string BuildingMenu = "BUILDING MENU";
            public const string PreparingAudio = "PREPARING AUDIO";
        }

        public static class Hud
        {
            public const string Alpha = "ALPHA";
            public const string Bravo = "BRAVO";
            public const string Versus = "VS";
            public const string ContinueNow = "CONTINUE NOW";
            public const string BackToMenu = "BACK TO MENU";
            public const string KillsDeaths = "K  /  D";
            public const string BuyMenuHeading = "BUY MENU";
            public const string Weapons = "WEAPONS";
            public const string GearAbilities = "GEAR & ABILITIES";
            public const string ReadyEndBuyTime = "READY  ·  END BUY TIME";
            public const string BuyMenuHint = "BUY TIME {0}s   ·   [B] FOR BUY MENU";
            public const string BuyMenuTitle = "BUY MENU      $ {0}      {1}s";
            public const string Owned = "owned";
            public const string BodyArmor = "Body Armor";
            public const string DefuseKit = "Defuse Kit";
            public const string PauseHeading = "PAUSE";
            public const string Resume = "RESUME";
            public const string QuitGame = "QUIT GAME";
            public const string ScoreboardScore = "ALPHA  {0}   :   {1}  BRAVO";
            public const string Scoreboard = "SCOREBOARD";
            public const string DeadSuffix = "  (dead)";
            public const string FirstTo = "FIRST TO {0}";
            public const string Attack = "ATTACK";
            public const string Defense = "DEFENSE";
            public const string BombTimer = "BOMB PLANTED   {0}";
            public const string BuyTimeAttack = "BUY TIME {0} — YOU ATTACK";
            public const string BuyTimeDefend = "BUY TIME {0} — YOU DEFEND";
            public const string BuyTime = "BUY TIME {0}";
            public const string MatchWon = "{0} WINS THE MATCH";
            public const string RoundDraw = "ROUND DRAW";
            public const string RoundWon = "{0} WINS THE ROUND";
            public const string HalftimeMessage = "HALFTIME — SIDES SWAPPED, MONEY RESET";
            public const string CooldownSeconds = "{0:0.0}s";
            public const string Smoke = "SMOKE";
            public const string Flash = "FLASH";
            public const string Frag = "FRAG";
            public const string Scan = "SCAN";
            public const string Fire = "FIRE";
            public const string Wire = "WIRE";
            public const string MedKit = "MED";
        }

        public static class Onboarding
        {
            public const string YouAreHeavy = "YOU ARE HEAVY";
            public const string MovementDescription = "You walk and turn slower than in most shooters. That is on purpose. Accelerating, braking and landing all cost time - plan your route instead of twitching around.";
            public const string YourBreathingMatters = "YOUR BREATHING MATTERS";
            public const string BreathingDescription = "After sprinting your breathing gets heavy and the view drifts. While aiming you can hold your breath with Shift - but only for a few seconds, after that it gets worse than before.";
            public const string NotAllHitsAreEqual = "NOT ALL HITS ARE EQUAL";
            public const string HitZonesDescription = "Head, torso, arms and legs count separately. Leg hits slow you down, arm hits make your aim shaky, and wounds keep bleeding until you use a med kit.";
            public const string Skip = "SKIP";
            public const string LetsGo = "LET'S GO";
            public const string Next = "NEXT";
        }

        public static class Bomb
        {
            public const string PlantingBomb = "Planting bomb...";
            public const string HoldEToPlant = "Hold [E] to plant";
            public const string CarryBomb = "You are carrying the bomb — head to site A or B";
            public const string DroppedBomb = "Bomb is on the ground — pick it up!";
            public const string TeammateHasBomb = "A teammate is carrying the bomb";
            public const string Defusing = "Defusing...";
            public const string HoldEToDefuse = "Hold [E] to defuse";
            public const string ProtectBomb = "Bomb is planted — protect it!";
        }

        public static class Messages
        {
            public const string PlantedTheBomb = "{0} planted the bomb";
            public const string TheBombWasPlanted = "The bomb was planted";
            public const string DefusedTheBomb = "{0} defused the bomb";
            public const string TheBombWasDefused = "The bomb was defused";
            public const string TheBombExploded = "The bomb exploded!";
            public const string DoubleKill = "DOUBLE KILL";
            public const string TripleKill = "TRIPLE KILL";
            public const string Ace = "ACE!";
            public const string Clutch = "CLUTCH!";
            public const string RoundMvp = "ROUND MVP";
            public const string Someone = "Someone";
            public const string KilledBy = "Killed by {0}";
            public const string Eliminated = "Eliminated";
            public const string Spectating = "Spectating  {0}";
            public const string SwitchSpectatorHint = "Left click / right click  switches";
            public const string PersonalBest = "New personal best: {0} wins in a row";
            public const string Enemy = "Enemy {0}";
        }

        public static class States
        {
            public const string NoRoundsYet = "NO ROUNDS YET";
            public const string CareerDescription = "Your wins, streaks and aces show up here once you have played.";
            public const string StartYourFirstRound = "START YOUR FIRST ROUND";
            public const string ThatDidNotWork = "THAT DID NOT WORK";
            public const string RetryDescription = "{0}\n\nThis is not on you. Give it another try - if it happens again, the reason is in the crash report.";
            public const string TryAgain = "TRY AGAIN";
            public const string ConnectionLost = "CONNECTION LOST";
            public const string ConnectionLostDescription = "The game runs as its own host on this computer. If the connection drops, the running round cannot be saved - but your career progress stays saved.";
        }

        public static class Legacy
        {
            public const string TeamSize = "Team size: {0} vs {1}";
            public const string BotDifficulty = "Bot difficulty";
            public const string Easy = "Easy";
            public const string Normal = "Normal";
            public const string Hard = "Hard";
            public const string GameMode = "Game mode";
            public const string Elimination = "Elimination";
            public const string Bomb = "Bomb";
            public const string Sensitivity = "Mouse sensitivity: {0:0.00}";
            public const string StartRound = "Start round";
        }

        public static class Equipment
        {
            public const string AssaultRifle = "Assault Rifle";
            public const string Smg = "SMG";
            public const string SniperRifle = "Sniper Rifle";
            public const string Pistol = "Pistol";
            public const string SmokeWall = "Smoke Wall";
            public const string Flashbang = "Flashbang";
            public const string FragGrenade = "Frag Grenade";
            public const string ScanPulse = "Scan Pulse";
            public const string IncendiaryWall = "Incendiary Wall";
            public const string Tripwire = "Tripwire";
            public const string MedKit = "Med Kit";

            // Vorhandene Asset-IDs bleiben erhalten. Niemals den Anzeigetext
            // auswerten: er darf ohne Aenderung der Waffenlogik uebersetzt werden.
            public static string WeaponName(WeaponStats weapon)
            {
                if (weapon == null) return "-";
                return weapon.name switch
                {
                    "Sturmgewehr" or "Bot_Sturmgewehr" => AssaultRifle,
                    "Maschinenpistole" or "Bot_Maschinenpistole" => Smg,
                    "Scharfschuetzengewehr" or "Bot_Scharfschuetzengewehr" => SniperRifle,
                    "Pistole" => Pistol,
                    _ => weapon.DisplayName,
                };
            }

            public static string AbilityName(AbilityStats ability)
            {
                if (ability == null) return "-";
                return ability.Kind switch
                {
                    AbilityKind.Rauchwand => SmokeWall,
                    AbilityKind.Blendgranate => Flashbang,
                    AbilityKind.Splittergranate => FragGrenade,
                    AbilityKind.ScanPuls => ScanPulse,
                    AbilityKind.Brandwand => IncendiaryWall,
                    AbilityKind.Stolperdraht => Tripwire,
                    AbilityKind.Verbandspaket => MedKit,
                    _ => ability.DisplayName,
                };
            }

            public static string BuyEntryName(WeaponCatalog catalog, WeaponCatalog.BuyEntry entry)
            {
                var weapon = catalog != null ? catalog.Get(entry.PlayerWeaponIndex) : null;
                return weapon != null ? WeaponName(weapon) : entry.DisplayName;
            }
        }

        public static class Common
        {
            public const string TeamAlpha = "Team Alpha";
            public const string TeamBravo = "Team Bravo";
            public const string NoTeam = "No team";
            public const string Player = "PLAYER";
        }

        public static class Radio
        {
            public const string TakingFire = "Taking fire!";
            public const string EnemySpotted = "Enemy spotted!";
            public const string IHearSomething = "I hear something!";
            public const string Bot = "Bot";
            public const string NeedHelp = "Need help!";
        }

        public static class Crash
        {
            public const string Title = "INFRONT - crash report";
            public const string LocalOnlyDescription = "This file stays on this computer. Nothing is sent anywhere.";
            public const string SharingDescription = "If you want to, you can pass it on yourself.";
            public const string Time = "Time:        ";
            public const string Kind = "Kind:        ";
            public const string Build = "Build:       ";
            public const string Unity = "Unity:       ";
            public const string System = "System:      ";
            public const string Cpu = "CPU:         ";
            public const string Graphics = "Graphics:    ";
            public const string Memory = "Memory:      ";
            public const string Scene = "Scene:       ";
            public const string Message = "Message:";
            public const string CallStack = "Call stack:";
            public const string None = "(none)";
            public const string ReportWritten = "Crash report written - see YOUR DATA";
        }
    }
}
