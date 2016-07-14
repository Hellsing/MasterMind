using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using EloBuddy;
using EloBuddy.SDK;
using EloBuddy.SDK.Enumerations;
using EloBuddy.SDK.Events;
using EloBuddy.SDK.Menu;
using EloBuddy.SDK.Menu.Values;
using EloBuddy.SDK.Rendering;
using EloBuddy.SDK.Utils;
using MasterMind.Properties;
using Newtonsoft.Json;
using SharpDX;
using SharpDX.Direct3D9;
using Color = System.Drawing.Color;
using Font = System.Drawing.Font;
using ObjectManager = EloBuddy.ObjectManager;
using Rectangle = System.Drawing.Rectangle;
using Sprite = EloBuddy.SDK.Rendering.Sprite;
using Version = System.Version;

namespace MasterMind.Components
{
    public sealed class MapHack : IComponent
    {
        public static readonly string ConfigFile = Path.Combine(MasterMind.ConfigFolderPath, "MapHack.json");
        public static readonly string ChampionImagesFolderPath = Path.Combine(MasterMind.ConfigFolderPath, "ChampionImages");

        private static readonly Version ForceUpdateIconsVersion = new Version(0, 0);

        private const string VersionUrl = "https://ddragon.leagueoflegends.com/api/versions.json";
        private const string ChampSquareUrl = "http://ddragon.leagueoflegends.com/cdn/{0}/img/champion/{1}.png";
        private const string ChampSquarePrefix = "MasterMindMapHack";
        private const string ChampSquareSuffix = ".png";
        private const string ChampSquareMinimapSuffix = "_minimap" + ChampSquareSuffix;

        private const int MinimapIconSize = 25;
        private static readonly Vector2 MinimapIconOffset = new Vector2((int) -Math.Round(MinimapIconSize / 2f));

        private WebClient WebClient { get; set; }
        private string LiveVersionString { get; set; }
        private Version LiveVersion { get; set; }

        private Dictionary<Champion, Func<Texture>> LoadedChampionTextures { get; set; }
        private Dictionary<Champion, Sprite> ChampionSprites { get; set; }
        private Text TimerText { get; set; }

        public Menu Menu { get; private set; }
        public CheckBox DrawGlobal { get; private set; }
        public CheckBox DrawRecallCircle { get; private set; }
        public CheckBox DrawMovementCircle { get; private set; }
        public CheckBox DrawInvisibleTime { get; private set; }
        public Slider DelayInvisibleTime { get; private set; }
        public Slider RangeCircleDisableRange { get; private set; }

        private Vector3 EnemySpawnPoint { get; set; }

        private HashSet<int> DeadHeroes { get; set; } 
        private Dictionary<int, int> LastSeen { get; set; }
        private Dictionary<int, Vector3> LastSeenPosition { get; set; }
        private Dictionary<int, float> LastSeenRange { get; set; }
        private Dictionary<int, Tuple<int, int>> RecallingHeroes { get; set; }

        private int LastUpdate { get; set; }

        public bool ShouldLoad(bool isSpectatorMode = false)
        {
            // Only load when not in spectator mode
            return !isSpectatorMode;
        }

        public void InitializeComponent()
        {
            // Initialize properties
            LoadedChampionTextures = new Dictionary<Champion, Func<Texture>>();
            ChampionSprites = new Dictionary<Champion, Sprite>();
            DeadHeroes = new HashSet<int>();
            LastSeen = new Dictionary<int, int>();
            LastSeenPosition = new Dictionary<int, Vector3>();
            LastSeenRange = new Dictionary<int, float>();
            EnemySpawnPoint = ObjectManager.Get<Obj_SpawnPoint>().First(o => o.IsEnemy).Position;
            RecallingHeroes = new Dictionary<int, Tuple<int, int>>();
            TimerText = new Text("30", new Font(FontFamily.GenericMonospace, 9, FontStyle.Regular)) { Color = Color.FromArgb(150, Color.Red) };
            LastUpdate = Core.GameTickCount;

            #region Menu Creation

            Menu = MasterMind.Menu.AddSubMenu("Map Hack");

            Menu.AddGroupLabel("Information");
            Menu.AddLabel("Enabling the Map Hack will allow you to see the last position of the enemy.");
            Menu.AddLabel("You can also see where the enemy could be with their current movement speed,");
            Menu.AddLabel("aswell as recalling and the time they are invisible already.");
            Menu.AddLabel("As always, everything is highly configureable.");
            Menu.AddSeparator();

            Menu.AddGroupLabel("Options");
            DrawGlobal = Menu.Add("global", new CheckBox("Drawing enabled"));
            DrawRecallCircle = Menu.Add("recall", new CheckBox("Draw recall circle"));
            DrawMovementCircle = Menu.Add("movement", new CheckBox("Draw movement circle"));
            DrawInvisibleTime = Menu.Add("time", new CheckBox("Draw time since being invisile"));
            Menu.AddSeparator();

            Menu.AddGroupLabel("Adjustments");
            DelayInvisibleTime = Menu.Add("timeDelay", new Slider("Show timer after enemy being invisible for {0} second(s)", 10, 0, 30));
            RangeCircleDisableRange = Menu.Add("disableRange", new Slider("Disable range circle after {0}0 range", 800, 200, 2000));

            #endregion

            // Load local champion images
            LoadChampionImages();

            // Create sprite objects from the images
            CreateSprites();

            // Listen to required events
            Game.OnTick += OnTick;
            Drawing.OnEndScene += OnDraw;
            Teleport.OnTeleport += OnTeleport;
            GameObject.OnCreate += OnCreate;

            // Initialize version download
            WebClient = new WebClient();
            WebClient.DownloadStringCompleted += DownloadVersionCompleted;

            try
            {
                // Download the version from Rito
                WebClient.DownloadStringAsync(new Uri(VersionUrl, UriKind.Absolute));
            }
            catch (Exception)
            {
                Logger.Info("[MasterMind] Failed to download most recent version.");
                ContinueInitialization();
            }
        }

        private void OnTick(EventArgs args)
        {
            // Time elapsed since last update
            var elapsed = Core.GameTickCount - LastUpdate;
            LastUpdate = Core.GameTickCount;

            foreach (var enemy in EntityManager.Heroes.Enemies)
            {
                // Check if hero is dead
                if (enemy.IsDead && !DeadHeroes.Contains(enemy.NetworkId))
                {
                    DeadHeroes.Add(enemy.NetworkId);
                }

                // Check if hero was dead but respawned
                if (!enemy.IsDead && DeadHeroes.Contains(enemy.NetworkId))
                {
                    DeadHeroes.Remove(enemy.NetworkId);

                    LastSeen[enemy.NetworkId] = Core.GameTickCount;
                    LastSeenPosition[enemy.NetworkId] = EnemySpawnPoint;
                    LastSeenRange[enemy.NetworkId] = 0;
                }

                // Update last seen range
                if (elapsed > 0 && LastSeenRange.ContainsKey(enemy.NetworkId) && !RecallingHeroes.ContainsKey(enemy.NetworkId))
                {
                    LastSeenRange[enemy.NetworkId] = LastSeenRange[enemy.NetworkId] + (enemy.MoveSpeed > 1 ? enemy.MoveSpeed : 540) * elapsed / 1000f;
                }

                if (enemy.IsInRange(EnemySpawnPoint, 250))
                {
                    LastSeenPosition[enemy.NetworkId] = EnemySpawnPoint;
                }

                if (enemy.IsHPBarRendered)
                {
                    // Remove from last seen
                    LastSeen.Remove(enemy.NetworkId);
                    LastSeenPosition.Remove(enemy.NetworkId);
                }
                else
                {
                    if (!LastSeen.ContainsKey(enemy.NetworkId))
                    {
                        // Add to last seen
                        LastSeen.Add(enemy.NetworkId, Core.GameTickCount);
                        LastSeenPosition[enemy.NetworkId] = enemy.ServerPosition;
                        LastSeenRange[enemy.NetworkId] = 0;
                    }
                }
            }
        }

        private void OnDraw(EventArgs args)
        {
            if (!DrawGlobal.CurrentValue)
            {
                // Complete drawing turned off
                return;
            }

            foreach (var enemy in EntityManager.Heroes.Enemies.Where(o => !o.IsDead || o.IsInRange(EnemySpawnPoint, 250)))
            {
                // Get the minimap position
                var pos = enemy.ServerPosition.WorldToMinimap();

                if (LastSeen.ContainsKey(enemy.NetworkId))
                {
                    // Update the position
                    pos = LastSeenPosition[enemy.NetworkId].WorldToMinimap();

                    // Get the time being invisible in seconds
                    var invisibleTime = (Core.GameTickCount - LastSeen[enemy.NetworkId]) / 1000f;

                    // Predicted movement circle
                    if (DrawMovementCircle.CurrentValue)
                    {
                        // Get the radius the champ could have walked
                        var radius = LastSeenRange.ContainsKey(enemy.NetworkId) ? LastSeenRange[enemy.NetworkId] : (enemy.MoveSpeed > 1 ? enemy.MoveSpeed : 540) * invisibleTime;

                        // Don't roast toasters
                        if (radius < RangeCircleDisableRange.CurrentValue * 10)
                        {
                            Utilities.DrawCricleMinimap(pos, radius * Utilities.MinimapMultiplicator, Color.Red, 1, 500);
                        }
                    }

                    // Draw the minimap icon
                    ChampionSprites[enemy.Hero].Draw(pos + MinimapIconOffset);

                    // Draw the time being invisible
                    if (DrawInvisibleTime.CurrentValue && invisibleTime >= DelayInvisibleTime.CurrentValue)
                    {
                        var text = Math.Floor(invisibleTime).ToString(CultureInfo.InvariantCulture);
                        var bounding = TimerText.MeasureBounding(text);
                        TimerText.Draw(text, TimerText.Color, pos - (new Vector2(bounding.Width, bounding.Height) / 2) + 1);
                    }
                }

                // Draw recall circle
                if (DrawRecallCircle.CurrentValue && RecallingHeroes.ContainsKey(enemy.NetworkId))
                {
                    var startTime = RecallingHeroes[enemy.NetworkId].Item1;
                    var duration = RecallingHeroes[enemy.NetworkId].Item2;

                    Utilities.DrawArc(pos, (MinimapIconSize + 4) / 2f, Color.Aqua, 3.1415f, Utilities.PI2 * ((Core.GameTickCount - startTime) / (float) duration), 2f, 100);
                }
            }
        }

        private void OnTeleport(Obj_AI_Base sender, Teleport.TeleportEventArgs args)
        {
            // Only check for enemy Heroes and recall teleports
            if (sender.Type == GameObjectType.AIHeroClient && sender.IsEnemy && args.Type == TeleportType.Recall)
            {
                switch (args.Status)
                {
                    case TeleportStatus.Start:
                        RecallingHeroes[sender.NetworkId] = new Tuple<int, int>(Core.GameTickCount, args.Duration);
                        break;

                    case TeleportStatus.Abort:
                        RecallingHeroes.Remove(sender.NetworkId);
                        break;

                    case TeleportStatus.Finish:
                        LastSeen[sender.NetworkId] = Core.GameTickCount;
                        LastSeenPosition[sender.NetworkId] = EnemySpawnPoint;
                        LastSeenRange[sender.NetworkId] = 0;
                        RecallingHeroes.Remove(sender.NetworkId);
                        break;
                }
            }
        }

        private void OnCreate(GameObject sender, EventArgs args)
        {
            // Check only enemy MissileClient
            if (sender.Type == GameObjectType.MissileClient)
            {
                // Validate missile
                var missile = (MissileClient) sender;
                if (missile.SpellCaster != null && missile.SpellCaster.Type == GameObjectType.AIHeroClient && missile.SpellCaster.IsEnemy && !missile.StartPosition.IsZero)
                {
                    // Set last seen position
                    LastSeen[sender.NetworkId] = Core.GameTickCount;
                    LastSeenPosition[sender.NetworkId] = missile.StartPosition;
                    LastSeenRange[sender.NetworkId] = 0;
                }
            }
        }

        private void LoadChampionImages()
        {
            // Load the unknown champ icon
            string unknownTextureKey;
            MasterMind.TextureLoader.Load(TransformToMinimapIcon(Resources.UnknownChamp), out unknownTextureKey);

            // Load the current champions
            if (Directory.Exists(ChampionImagesFolderPath))
            {
                foreach (var champion in EntityManager.Heroes.Enemies.Select(o => o.Hero).Where(champion => !LoadedChampionTextures.ContainsKey(champion)))
                {
                    var championName = champion.ToString();

                    // Check if file for champ exists
                    var filePath = Path.Combine(ChampionImagesFolderPath, championName + ChampSquareMinimapSuffix);
                    if (!File.Exists(filePath))
                    {
                        // Use unknown champ image
                        LoadedChampionTextures.Add(champion, () => MasterMind.TextureLoader[unknownTextureKey]);
                        continue;
                    }

                    // Load local image
                    Bitmap champIcon;
                    try
                    {
                        using (var bmpTemp = new Bitmap(filePath))
                        {
                            champIcon = new Bitmap(bmpTemp);
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.Error("[MasterMind] Failed to load champion image from file!");
                        Logger.Error(e.ToString());
                        File.Delete(filePath);

                        // Use unknown champ image
                        LoadedChampionTextures.Add(champion, () => MasterMind.TextureLoader[unknownTextureKey]);
                        continue;
                    }

                    MasterMind.TextureLoader.Load(ChampSquarePrefix + championName, champIcon);
                    LoadedChampionTextures.Add(champion, () => MasterMind.TextureLoader[ChampSquarePrefix + championName]);
                }
            }
            else
            {
                // No champion images exist, use unknown image
                foreach (var champion in EntityManager.Heroes.Enemies.Select(o => o.Hero).Where(champion => !LoadedChampionTextures.ContainsKey(champion)))
                {
                    LoadedChampionTextures.Add(champion, () => MasterMind.TextureLoader[unknownTextureKey]);
                }
            }
        }

        private void CreateSprites()
        {
            // Create a sprite object for each champion loaded
            foreach (var textureEntry in LoadedChampionTextures)
            {
                var key = textureEntry.Key;
                ChampionSprites[textureEntry.Key] = new Sprite(() => LoadedChampionTextures[key]());
            }
        }

        private async void ContinueInitialization()
        {
            await Task.Run(() =>
            {
                // Dispose the WebClient
                WebClient.Dispose();
                WebClient = null;

                // Create config file if not existing
                if (!File.Exists(ConfigFile))
                {
                    File.Create(ConfigFile).Close();
                }

                // Open the json file
                var config = JsonConvert.DeserializeObject<MapHackConfig>(File.ReadAllText(ConfigFile)) ?? new MapHackConfig();

                #region Checking Version

                // Helpers
                var downloadImages = false;
                var updateMinimapIcons = false;

                // Check for the version
                if (config.Version == null && LiveVersion == null)
                {
                    Logger.Error("[MasterMind] Can't continue initialization of MapHack due to failed version download and no local files.");
                }
                else if (LiveVersion == null)
                {
                    Logger.Info("[MasterMind] Version check failed, using local cached images.");
                }
                else if (config.Version != null)
                {
                    // Compare versions
                    if (config.Version < LiveVersion)
                    {
                        // Update the version
                        config.Version = LiveVersion;

                        Logger.Info("[MasterMind] Redownloading champion images due to League update.");
                        downloadImages = true;
                    }
                }
                else
                {
                    // Update the version
                    config.Version = LiveVersion;

                    Logger.Info("[MasterMind] Downloading champion images for the first time.");
                    downloadImages = true;
                }

                // Check if a forced update of the minimap icon is needed
                if (config.ForceUpdateIconsVersion != null && config.ForceUpdateIconsVersion < ForceUpdateIconsVersion)
                {
                    Logger.Info("[MasterMind] Updating minimap icons.");
                    updateMinimapIcons = true;
                }

                #endregion

                // Update config values
                config.ForceUpdateIconsVersion = ForceUpdateIconsVersion;

                // Save the json file
                File.WriteAllText(ConfigFile, JsonConvert.SerializeObject(config));

                // Download images and update icons if needed
                if (downloadImages || updateMinimapIcons)
                {
                    if (downloadImages)
                    {
                        // Wait till all images have been downloaded
                        DownloadChampionImages().Wait();
                        Logger.Info("[MasterMind] Download of champion images completed.");
                    }

                    // Update minimap icons
                    UpdateMinimapIcons();
                }
            });
        }

        private async Task DownloadChampionImages()
        {
            // Create the champion images folder
            Directory.CreateDirectory(ChampionImagesFolderPath);

            await Task.Run(() =>
            {
                // Redownload all images
                using (WebClient = new WebClient())
                {
                    foreach (var champion in Enum.GetValues(typeof (Champion)).Cast<Champion>().Where(o => o != Champion.Unknown))
                    {
                        try
                        {
                            var filePath = Path.Combine(ChampionImagesFolderPath, champion + ChampSquareSuffix);

                            // Download the image of the champion
                            WebClient.DownloadFile(new Uri(string.Format(ChampSquareUrl, LiveVersionString, champion), UriKind.Absolute), filePath);
                        }
                        catch (Exception)
                        {
                            Logger.Info("[MasterMind] Failed to download champion image of {0}!", champion);
                        }
                    }
                }
            });
        }

        private async void UpdateMinimapIcons()
        {
            // Create the champion images folder
            Directory.CreateDirectory(ChampionImagesFolderPath);

            await Task.Run(() =>
            {
                foreach (var champion in Enum.GetValues(typeof (Champion)).Cast<Champion>().Where(o => o != Champion.Unknown))
                {
                    try
                    {
                        var filePath = Path.Combine(ChampionImagesFolderPath, champion + ChampSquareSuffix);

                        // Load the image as bitmap
                        using (var bitmap = (Bitmap) Image.FromFile(filePath))
                        {
                            // Transform the image into a minimap icon
                            var minimapIcon = TransformToMinimapIcon(bitmap);

                            // Save the icon to file
                            minimapIcon.Save(Path.Combine(ChampionImagesFolderPath, champion + ChampSquareMinimapSuffix), ImageFormat.Png);

                            // Replace the current icon
                            ReplaceChampionImage(champion, minimapIcon);
                        }
                    }
                    catch (Exception)
                    {
                        Logger.Info("[MasterMind] Failed to update minimap icon for {0}!", champion);
                    }
                }
            });
        }

        private void ReplaceChampionImage(Champion champion, Bitmap minimapIcon)
        {
            // Replace images in sync
            Core.DelayAction(() =>
            {
                if (LoadedChampionTextures.ContainsKey(champion))
                {
                    // Unload the current texture
                    MasterMind.TextureLoader.Unload(ChampSquarePrefix + champion);

                    // Load the new texture
                    MasterMind.TextureLoader.Load(ChampSquarePrefix + champion, minimapIcon);

                    // Replace the texture
                    LoadedChampionTextures[champion] = () => MasterMind.TextureLoader[ChampSquarePrefix + champion];
                }
            }, 0);
        }

        private void DownloadVersionCompleted(object sender, DownloadStringCompletedEventArgs args)
        {
            if (args.Cancelled || args.Error != null)
            {
                Logger.Warn("[MasterMind] Error while downloading the versions, cancelled.");
                if (args.Error != null)
                {
                    Logger.Warn(args.Error.ToString());
                    ContinueInitialization();
                    return;
                }
            }

            try
            {
                // Get the current version as string
                LiveVersionString = JsonConvert.DeserializeObject<List<string>>(args.Result)[0];

                // Parse the current version
                LiveVersion = new Version(LiveVersionString);
            }
            catch (Exception e)
            {
                Logger.Warn("[MasterMind] Error while parsing the downloaded version string.");
                Logger.Warn(e.ToString());
            }

            ContinueInitialization();
        }

        // Credits to "Christian Brutal Sniper" (https://www.elobuddy.net/user/16502-/)
        // Adjusted to fit my needs
        public static Bitmap TransformToMinimapIcon(Bitmap source, int iconSize = MinimapIconSize)
        {
            var tempBtm = new Bitmap(source.Width + 4, source.Height + 4);
            var finalBitmap = new Bitmap(iconSize, iconSize);

            using (var g = Graphics.FromImage(source))
            {
                using (Brush brsh = new SolidBrush(Color.FromArgb(120, 0, 0, 0)))
                {
                    g.FillRectangle(brsh, new Rectangle(0, 0, source.Width, source.Height));
                }
            }
            using (var g = Graphics.FromImage(tempBtm))
            {
                using (Brush brsh = new SolidBrush(Color.Red))
                {
                    g.FillEllipse(brsh, 2, 2, source.Width - 4, source.Height - 4);
                }
                using (Brush brsh = new TextureBrush(source))
                {
                    g.FillEllipse(brsh, 6, 6, source.Width - 12, source.Height - 12);
                }
            }
            using (var g = Graphics.FromImage(finalBitmap))
            {
                g.InterpolationMode = InterpolationMode.High;
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.DrawImage(tempBtm, new Rectangle(0, 0, iconSize, iconSize));
            }
            tempBtm.Dispose();

            return finalBitmap;
        }
    }

    [DataContract]
    public class MapHackConfig
    {
        [DataMember]
        public string VersionString { get; set; }
        [DataMember]
        public string ForceUpdateIconsString { get; set; }

        private Version _version;
        public Version Version
        {
            get { return VersionString == null ? null : _version ?? (_version = new Version(VersionString)); }
            set
            {
                VersionString = value.ToString();
                _version = value;
            }
        }

        private Version _forceUpdateIconsVersion;
        public Version ForceUpdateIconsVersion
        {
            get { return ForceUpdateIconsString == null ? null : _forceUpdateIconsVersion ?? (_forceUpdateIconsVersion = new Version(ForceUpdateIconsString)); }
            set
            {
                ForceUpdateIconsString = value.ToString();
                _forceUpdateIconsVersion = value;
            }
        }
    }
}
