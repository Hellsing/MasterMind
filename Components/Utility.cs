using System;
using System.Collections.Generic;
using System.Linq;
using EloBuddy;
using EloBuddy.SDK;
using EloBuddy.SDK.Menu;
using EloBuddy.SDK.Menu.Values;
using EloBuddy.SDK.Rendering;
using SharpDX;
using Color = System.Drawing.Color;

namespace MasterMind.Components
{
    public sealed class Utility : IComponent
    {
        private static readonly Color CloneLineColor = Color.OrangeRed;
        private static readonly Color OriginalLineColor = Color.LimeGreen;
        private const int CloneRevealerLineWidth = 5;

        private static readonly Champion[] CloneChampions =
        {
            Champion.Leblanc,
            Champion.Shaco,
            Champion.MonkeyKing
        };
        private HashSet<AIHeroClient> CurrentCloneChampions { get; set; }
        private HashSet<Obj_AI_Base> CurrentClones { get; set; }

        private static readonly SharpDX.Color FlashColor = SharpDX.Color.Yellow;
        private static readonly SharpDX.Color ShacoColor = SharpDX.Color.Tomato;
        private const int JukeRevealerLineWidth = 5;
        private const int JukeRevealerCircleRadius = 50;
        private const int FlashRange = 425;
        private const int ShacoQRange = 400;

        private List<AIHeroClient> EnemyShaco { get; set; }

        public Menu Menu { get; private set; }
        public CheckBox ShowClones { get; private set; }
        public CheckBox JukeRevealer { get; private set; }
        public Slider JukeTimer { get; private set; }

        public bool ShouldLoad(bool isSpectatorMode = false)
        {
            // Only load when not in spectator mode
            return !isSpectatorMode;
        }

        public void InitializeComponent()
        {
            // Initialize properties
            CurrentCloneChampions = new HashSet<AIHeroClient>();
            CurrentClones = new HashSet<Obj_AI_Base>();
            EnemyShaco = EntityManager.Heroes.Enemies.FindAll(o => o.Hero == Champion.Shaco);

            #region Setup Menu

            Menu = MasterMind.Menu.AddSubMenu("Utility");

            Menu.AddGroupLabel("Information");
            Menu.AddLabel("In here you will find some usefull and simple utility functions which you can configure.");
            Menu.AddSeparator();

            Menu.AddGroupLabel("1. Clone Revealer");
            Menu.AddLabel("Reveals the fake enemy champions with a cross, like Shaco clone.");
            if (EntityManager.Heroes.Enemies.Any(o => CloneChampions.Contains(o.Hero)))
            {
                ShowClones = Menu.Add("crossClone", new CheckBox("Enabled"));

                foreach (var cloneChamp in EntityManager.Heroes.Enemies.Where(o => CloneChampions.Contains(o.Hero)))
                {
                    // Add clone champ to the current clone champs
                    CurrentCloneChampions.Add(cloneChamp);
                }
            }
            else
            {
                Menu.AddLabel(string.Format(" - No clone champions in this match! ({0})", string.Join(" or ", CloneChampions)));
            }
            Menu.AddSeparator();

            Menu.AddGroupLabel("2. Juke Revealer");
            Menu.AddLabel("Reveals jukes, like flashing into brushes or Shaco Q");
            JukeRevealer = Menu.Add("juke", new CheckBox("Enabled"));
            JukeTimer = Menu.Add("jukeTimer", new Slider("Show juke direction for {0} seconds", 3, 1, 10));
            Menu.AddLabel("Note: Once I'm able to check if the team has vision on the end position");
            Menu.AddLabel("I will avoid always drawing the spell and instead only draw when there is no vision.");

            #endregion

            // Listen to required events
            Game.OnTick += OnTick;
            GameObject.OnCreate += OnCreate;
            Obj_AI_Base.OnProcessSpellCast += OnProcessSpellCast;
            Drawing.OnEndScene += OnDraw;
        }

        private void OnTick(EventArgs args)
        {
            // Validate clones
            if (CurrentClones.Count > 0)
            {
                CurrentClones.RemoveWhere(o => !o.IsValid || o.IsDead);
            }
        }

        private void OnCreate(GameObject sender, EventArgs args)
        {
            // Check if there are clone champs
            if (CurrentCloneChampions.Count > 0)
            {
                // Check if the created object is an enemy
                if (sender.IsEnemy)
                {
                    // Check if the object is a least a base object
                    var baseObject = sender as Obj_AI_Base;
                    if (baseObject != null)
                    {
                        // Check if the base object could be one of the enemy clones
                        if (CurrentCloneChampions.Any(cloneChamp => baseObject.Name == cloneChamp.Name))
                        {
                            // Add the revealed clone to the current clones
                            CurrentClones.Add(baseObject);
                        }
                    }
                }
            }
        }

        private void OnProcessSpellCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            if (sender.Type == GameObjectType.AIHeroClient)
            {
                var hero = (AIHeroClient) sender;
                if (hero.IsEnemy)
                {
                    // Juke Revealer
                    if (JukeRevealer.CurrentValue)
                    {
                        switch (args.SData.Name)
                        {
                            case "SummonerFlash":
                            case "Deceive":

                                // Assume the spell casted was Flash
                                var color = FlashColor;
                                var start = args.Start;
                                var end = start.IsInRange(args.End, FlashRange) ? args.End : start.Extend(args.End, FlashRange).To3DWorld();

                                if (args.SData.Name == "Deceive")
                                {
                                    if (EnemyShaco.Any(o => o.IdEquals(hero)))
                                    {
                                        // Apply Shaco data
                                        color = ShacoColor;
                                        end = start.IsInRange(args.End, ShacoQRange) ? args.End : start.Extend(args.End, ShacoQRange).To3DWorld();
                                    }
                                    else
                                    {
                                        // No valid Shaco
                                        break;
                                    }
                                }

                                // Validate and adjust end location if needed
                                end = end.GetValidCastSpot();

                                // Check vision on end location
                                // TODO

                                // Initialize the drawing
                                DrawingDraw drawJuke = bla =>
                                {
                                    // End position
                                    Circle.Draw(color, JukeRevealerCircleRadius, JukeRevealerLineWidth, end);

                                    // Line from start to end
                                    Line.DrawLine(Color.FromArgb(color.A, color.R, color.G, color.B), JukeRevealerLineWidth, start,
                                        start.Extend(end, start.Distance(end) - JukeRevealerCircleRadius).To3D((int) end.Z));
                                };

                                // Add draw listener
                                Drawing.OnDraw += drawJuke;

                                // Remove drawing if the enemy is visible after 1 second
                                // TODO: Remove once I can check the vision on the end location for the player
                                if (JukeTimer.CurrentValue > 1)
                                {
                                    Core.DelayAction(() =>
                                    {
                                        if (hero.IsHPBarRendered)
                                        {
                                            Drawing.OnDraw -= drawJuke;
                                        }
                                    }, 1000);
                                }

                                // Remove drawing after 5 seconds
                                Core.DelayAction(() => { Drawing.OnDraw -= drawJuke; }, JukeTimer.CurrentValue * 1000);

                                break;
                        }
                    }
                }
            }
        }

        private void OnDraw(EventArgs args)
        {
            if (ShowClones != null && ShowClones.CurrentValue)
            {
                // Get all clones which are visible on screen
                // ReSharper disable once LoopCanBePartlyConvertedToQuery
                foreach (var clone in CurrentClones.Where(o => o.IsVisible && o.IsHPBarRendered))
                {
                    // Get the screen bounding of the clone
                    var cloneBounding = Utilities.GetScreenBoudingRectangle(clone);

                    // Cross through the clone
                    var size = Math.Min(cloneBounding.Width, cloneBounding.Height);
                    var halfSize = size / 2;

                    // Draw the cross lines
                    Line.DrawLine(CloneLineColor, CloneRevealerLineWidth,
                        cloneBounding.Center - halfSize,
                        cloneBounding.Center + halfSize);
                    Line.DrawLine(CloneLineColor, CloneRevealerLineWidth,
                        cloneBounding.Center + new Vector2(-halfSize, halfSize),
                        cloneBounding.Center + new Vector2(halfSize, -halfSize));

                    // Check if the real champ is visible aswell
                    var realChamp = EntityManager.Heroes.Enemies.Find(o => o.Name == clone.Name);
                    if (realChamp.IsVisible && realChamp.IsHPBarRendered)
                    {
                        // Get the screen bounding of the real champ
                        var champBounding = Utilities.GetScreenBoudingRectangle(realChamp);

                        // Target the real champ
                        size = Math.Min(champBounding.Width, champBounding.Height) / 2;
                        halfSize = size / 2;

                        // Top left
                        Line.DrawLine(OriginalLineColor, CloneRevealerLineWidth,
                            champBounding.TopLeft + new Vector2(0, halfSize),
                            champBounding.TopLeft,
                            champBounding.TopLeft + new Vector2(halfSize, 0));

                        // Top right
                        Line.DrawLine(OriginalLineColor, CloneRevealerLineWidth,
                            champBounding.TopRight + new Vector2(-halfSize, 0),
                            champBounding.TopRight,
                            champBounding.TopRight + new Vector2(0, halfSize));

                        // Bottom right
                        Line.DrawLine(OriginalLineColor, CloneRevealerLineWidth,
                            champBounding.BottomRight + new Vector2(0, -halfSize),
                            champBounding.BottomRight,
                            champBounding.BottomRight + new Vector2(-halfSize, 0));

                        // Bottom left
                        Line.DrawLine(OriginalLineColor, CloneRevealerLineWidth,
                            champBounding.BottomLeft + new Vector2(halfSize, 0),
                            champBounding.BottomLeft,
                            champBounding.BottomLeft + new Vector2(0, -halfSize));
                    }
                }
            }
        }
    }
}
