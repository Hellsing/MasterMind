using System;
using System.Collections.Generic;
using System.Linq;
using EloBuddy;
using EloBuddy.SDK;
using EloBuddy.SDK.Rendering;
using SharpDX;
using Color = System.Drawing.Color;

namespace MasterMind
{
    public static class Utilities
    {
        // ReSharper disable once InconsistentNaming
        public const float PI2 = (float) Math.PI * 2;

        public static Rectangle MinimapRectangle { get; private set; }
        public static float MinimapMultiplicator { get; private set; }

        static Utilities()
        {
            Initialize();
        }

        private static void Initialize()
        {
            Core.DelayAction(() =>
            {
                var multiplicator = new Vector3(1000, 1000, 0).WorldToMinimap().Distance(new Vector3(2000, 1000, 0).WorldToMinimap()) / 1000f;
                if (multiplicator <= float.Epsilon)
                {
                    Initialize();
                }
                else
                {
                    MinimapMultiplicator = multiplicator;
                    Vector2 leftUpper;
                    Vector2 rightLower;
                    if (Game.MapId == GameMapId.CrystalScar)
                    {
                        leftUpper = new Vector3(0, 13800, 0).WorldToMinimap();
                        rightLower = new Vector3(13800, 0, 0).WorldToMinimap();
                    }
                    else
                    {
                        leftUpper = new Vector3(0, 14800, 0).WorldToMinimap();
                        rightLower = new Vector3(14800, 0, 0).WorldToMinimap();
                    }

                    MinimapRectangle = new Rectangle((int) leftUpper.X, (int) leftUpper.Y, (int) (rightLower.X - leftUpper.X), (int) (rightLower.Y - leftUpper.Y));
                }
            }, 1000);
        }

        public static void DrawArc(Vector2 position, float radius, Color color, float startDegree, float length, float width = 0.6F, int quality = -1)
        {
            if (quality == -1)
            {
                quality = (int) (radius / 7 + 11);
            }
            var points = new Vector2[(int) (Math.Abs(quality * length / PI2) + 1)];
            var rad = new Vector2(0, radius);

            for (var i = 0; i <= (int) (Math.Abs(quality * length / PI2)); i++)
            {
                points[i] = (position + rad).RotateAroundPoint(position, startDegree + PI2 * i / quality * (length > 0 ? 1 : -1));
            }
            Line.DrawLine(color, width, points);
        }

        public static void DrawCricleMinimap(Vector2 screenPosition, float radius, Color color, float width = 2F, int quality = -1)
        {
            if (quality == -1)
            {
                quality = (int) (radius / 3 + 15);
            }

            var rad = new Vector2(0, radius);
            var segments = new List<MinimapCircleSegment>();
            var full = true;
            for (var i = 0; i <= quality; i++)
            {
                var pos = (screenPosition + rad).RotateAroundPoint(screenPosition, PI2 * i / quality);
                var contains = MinimapRectangle.Contains(pos);
                if (!contains)
                {
                    full = false;
                }
                segments.Add(new MinimapCircleSegment(pos, contains));
            }

            foreach (var ar in FindArcs(segments, full))
            {
                Line.DrawLine(color, width, ar);
            }
        }

        private static IEnumerable<Vector2[]> FindArcs(IReadOnlyList<MinimapCircleSegment> points, bool full)
        {
            var ret = new List<Vector2[]>();
            if (full)
            {
                ret.Add(points.Select(segment => segment.Position).ToArray());
                return ret;
            }
            var pos = 0;
            for (var c = 0; c < 3; c++)
            {
                int start = -1, stop = -1;
                for (var i = pos; i < points.Count; i++)
                {
                    if (points[i].IsValid)
                    {
                        if (start == -1)
                        {
                            start = i;
                        }
                    }
                    else
                    {
                        if (stop == -1 && start != -1)
                        {
                            stop = i;
                            pos = i;
                            if (start != 0)
                            {
                                break;
                            }
                            for (var j = points.Count - 1; j > 0; j--)
                            {
                                if (points[j].IsValid)
                                {
                                    start = j;
                                }
                                else
                                {
                                    break;
                                }
                            }
                        }
                        if (i == points.Count - 1)
                        {
                            pos = points.Count;
                        }
                    }
                }
                var arc = new List<Vector2>();

                if (start == -1 || stop == -1)
                {
                    continue;
                }
                var pointer = start;

                while (true)
                {
                    if (pointer == stop)
                    {
                        break;
                    }
                    arc.Add(points[pointer].Position);
                    pointer++;
                    if (pointer == points.Count)
                    {
                        pointer = 0;
                    }
                }

                ret.Add(arc.ToArray());
            }

            return ret;
        }

        public static RectangleF GetScreenBoudingRectangle(GameObject obj)
        {
            int minX = 0, maxX = 0, minY = 0, maxY = 0;

            foreach (var corner in obj.BBox.GetCorners())
            {
                var pos = corner.WorldToScreen();
                var x = (int) Math.Round(pos.X);
                var y = (int) Math.Round(pos.Y);

                // Compare current with existing X
                if (minX == 0 || x < minX)
                {
                    minX = x;
                }
                else if (maxX == 0 || x > maxX)
                {
                    maxX = x;
                }

                // Compare current with existing Y
                if (minY == 0 || y < minY)
                {
                    minY = y;
                }
                else if (maxY == 0 || y > maxY)
                {
                    maxY = y;
                }
            }

            return new RectangleF(minX, minY, maxX - minX, maxY - minY);
        }

        public static Vector3 GetValidCastSpot(this Vector3 castPosition)
        {
            // If the position is not a wall then return the input position
            var flags = castPosition.ToNavMeshCell().CollFlags;
            if (!flags.HasFlag(CollisionFlags.Wall) && !flags.HasFlag(CollisionFlags.Building))
            {
                return castPosition;
            }

            const int maxRange = 20;
            const double step = 2 * Math.PI / 20;

            var start = new Vector2(castPosition.ToNavMeshCell().GridX, castPosition.ToNavMeshCell().GridY);
            var checkedCells = new HashSet<Vector2>();

            // Get all directions
            var directions = new List<Vector2>();
            for (var theta = 0d; theta <= 2 * Math.PI + step; theta += step)
            {
                directions.Add((new Vector2((short) (start.X + Math.Cos(theta)), (short) (start.Y - Math.Sin(theta))) - start).Normalized());
            }

            var validPositions = new HashSet<Vector3>();
            for (var range = 1; range < maxRange; range++)
            {
                // ReSharper disable once LoopCanBePartlyConvertedToQuery
                foreach (var direction in directions)
                {
                    // Get the position to check
                    var end = start + range * direction;
                    var testPoint = new Vector2((short) end.X, (short) end.Y);
                    if (checkedCells.Contains(testPoint))
                    {
                        continue;
                    }
                    checkedCells.Add(testPoint);

                    // Check the position for wall flags
                    flags = new NavMeshCell((short) testPoint.X, (short) testPoint.Y).CollFlags;
                    if (!flags.HasFlag(CollisionFlags.Wall) && !flags.HasFlag(CollisionFlags.Building))
                    {
                        // Add the position to the valid positions set
                        validPositions.Add(NavMesh.GridToWorld((short) testPoint.X, (short) testPoint.Y));
                    }
                }

                if (validPositions.Count > 0)
                {
                    // Return the closest position to the initial wall position
                    return validPositions.OrderBy(o => o.Distance(start, true)).First();
                }
            }

            // Nothing found return input
            return castPosition;
        }
    }

    internal class MinimapCircleSegment
    {
        internal readonly Vector2 Position;
        internal readonly bool IsValid;

        internal MinimapCircleSegment(Vector2 position, bool valid)
        {
            Position = position;
            IsValid = valid;
        }
    }
}
