using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using EloBuddy;
using EloBuddy.Sandbox;
using EloBuddy.SDK;
using EloBuddy.SDK.Events;
using EloBuddy.SDK.Menu;
using EloBuddy.SDK.Rendering;
using EloBuddy.SDK.Utils;
using MasterMind.Components;
using SharpDX;
using Utility = MasterMind.Components.Utility;

namespace MasterMind
{
    public static class MasterMind
    {
        public static readonly string ConfigFolderPath = Path.Combine(SandboxConfig.DataDirectory, "MasterMind");
        public static readonly TextureLoader TextureLoader = new TextureLoader();

        public static bool IsSpectatorMode { get; private set; }

        public static Menu Menu { get; private set; }

        private static readonly IComponent[] Components =
        {
            new CooldownTracker(),
            new WardTracker(),
            new MapHack(),
            new Utility()
        };

        public static void Main(string[] args)
        {
            // Load the addon in a real match and when spectating games
            Loading.OnLoadingComplete += OnLoadingComplete;
            Loading.OnLoadingCompleteSpectatorMode += OnLoadingComplete;
        }

        private static void OnLoadingComplete(EventArgs args)
        {
            // Create the config folder
            Directory.CreateDirectory(ConfigFolderPath);

            // Initialize menu
            Menu = MainMenu.AddMenu("MasterMind", "MasterMind", "MasterMind - Improve Yourself!");

            Menu.AddGroupLabel("Welcome to MasterMind, your solution for quality game assistance.");
            Menu.AddLabel("This addon offers some neat features which will improve your gameplay");
            Menu.AddLabel("without dropping FPS or gameplay fun.");
            Menu.AddSeparator();
            Menu.AddLabel("Take a look at the various sub menus this addon has to offer, have fun!");

            // Initialize properties
            IsSpectatorMode = Bootstrap.IsSpectatorMode;

            // Initialize components
            foreach (var component in Components.Where(component => component.ShouldLoad(IsSpectatorMode)))
            {
                component.InitializeComponent();
            }

            return;

            // TODO: Remove debug
            Task.Run(() =>
            {
                // Get all brushes on the map
                Logger.Debug("[Brushes] NavMesh.Width {0} | Height {1} | CellWith {2} | CellHeight {3}", NavMesh.Width, NavMesh.Height, NavMesh.CellWidth, NavMesh.CellHeight);

                var brushes = new Dictionary<int, List<Geometry.Polygon>>();
                var offset = NavMesh.GridToWorld(0, 0).To2D();
                var cellSize = NavMesh.CellHeight;
                Logger.Debug("[Brushes] Cell size: " + cellSize);
                for (var cellX = 0; cellX < NavMesh.Width; cellX++)
                {
                    for (var cellY = 165; cellY < 400; cellY++)
                    {
                        // Get grid and cell
                        var cell = NavMesh.GetCell(cellX, cellY);
                        var worldPos = offset + new Vector2(cellX * cellSize, cellY * cellSize);

                        // Check for brush
                        if (cell.CollFlags.HasFlag(CollisionFlags.Grass))
                        {
                            // Check if already existing brush
                            var collection = brushes.Values.FirstOrDefault(o => o.Any(p => p.CenterOfPolygon().Distance(worldPos, true) < (cellSize * 3).Pow()));
                            if (collection == null)
                            {
                                // Create a new brush pair
                                Logger.Debug("[Brushes] Creating new pair of brush points, total so far: " + (brushes.Count + 1));
                                collection = new List<Geometry.Polygon>();
                                brushes.Add(brushes.Count, collection);
                            }

                            // Add the point to the collection
                            var cellPolygon = new Geometry.Polygon();
                            cellPolygon.Add(worldPos);
                            cellPolygon.Add(worldPos + new Vector2(0, cellSize));
                            cellPolygon.Add(worldPos + cellSize);
                            cellPolygon.Add(worldPos + new Vector2(cellSize, 0));
                            collection.Add(cellPolygon);
                        }
                    }
                }

                Logger.Debug("[Brushes] The result:\n" + string.Join("\n", brushes.Values.Select(o => o.Count)));

                // Convert brush points to polygons
                var polyBrushes = new Dictionary<int, Geometry.Polygon>();
                foreach (var brushEntry in brushes)
                {
                    var brushPoly = brushEntry.Value.JoinPolygons().FirstOrDefault();
                    if (brushPoly != null)
                    {
                        polyBrushes.Add(brushEntry.Key, brushPoly);
                    }
                }

                // Draw all brushes
                Logger.Debug("[Brushes] Ready to draw {0} brush polygons!", polyBrushes.Count);
                Core.DelayAction(() =>
                {
                    Drawing.OnDraw += delegate
                    {
                        foreach (var polyList in brushes)
                        {
                            foreach (var poly in polyList.Value)
                            {
                                //poly.Draw(System.Drawing.Color.LawnGreen, 2);
                            }
                        }

                        foreach (var polyBrush in polyBrushes)
                        {
                            //Circle.Draw(Color.LawnGreen, NavMesh.CellHeight, polyBrush.Value.Points.Select(o => o.To3DWorld()).ToArray());
                            polyBrush.Value.Draw(System.Drawing.Color.Red, 2);
                            Drawing.DrawText(polyBrush.Value.CenterOfPolygon().To3DWorld().WorldToScreen(), System.Drawing.Color.GreenYellow, polyBrush.Key.ToString(), 10);
                        }
                    };
                }, 0);
            });
        }
    }
}
