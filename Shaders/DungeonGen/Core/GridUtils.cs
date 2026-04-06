using UnityEngine;
using System.Collections.Generic;

namespace DungeonSystem.Core
{
    public static class GridUtils
    {
        public static readonly Direction[] CardinalDirections = { Direction.North, Direction.East, Direction.South, Direction.West };

        public static Vector2Int GetOffset(Direction direction)
        {
            return direction switch
            {
                Direction.North => new Vector2Int(0, 1),
                Direction.East  => new Vector2Int(1, 0),
                Direction.South => new Vector2Int(0, -1),
                Direction.West  => new Vector2Int(-1, 0),
                _ => Vector2Int.zero
            };
        }

        public static Direction GetOpposite(Direction direction)
        {
            return direction switch
            {
                Direction.North => Direction.South,
                Direction.East  => Direction.West,
                Direction.South => Direction.North,
                Direction.West  => Direction.East,
                _ => Direction.None
            };
        }

        public static Direction GetDirection(Vector2Int from, Vector2Int to)
        {
            Vector2Int delta = to - from;
            if (delta == Vector2Int.up)    return Direction.North;
            if (delta == Vector2Int.right) return Direction.East;
            if (delta == Vector2Int.down)  return Direction.South;
            if (delta == Vector2Int.left)  return Direction.West;
            return Direction.None;
        }

        /// <summary>
        /// Returns 90° clockwise rotation of a direction.
        /// </summary>
        public static Direction RotateCW(Direction dir)
        {
            return dir switch
            {
                Direction.North => Direction.East,
                Direction.East  => Direction.South,
                Direction.South => Direction.West,
                Direction.West  => Direction.North,
                _ => dir
            };
        }

        public static List<Vector2Int> GetOccupiedCells(Vector2Int origin, int width, int height)
        {
            var cells = new List<Vector2Int>(width * height);
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                    cells.Add(new Vector2Int(origin.x + x, origin.y + y));
            return cells;
        }

        /// <summary>
        /// Manhattan distance between two grid positions.
        /// </summary>
        public static int ManhattanDistance(Vector2Int a, Vector2Int b)
        {
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
        }

        /// <summary>
        /// Checks if a rect (origin, w, h) overlaps any cell in occupiedSet.
        /// Includes optional padding around the rect.
        /// </summary>
        public static bool RectOverlaps(Vector2Int origin, int w, int h, HashSet<Vector2Int> occupiedSet, int padding = 0)
        {
            for (int x = -padding; x < w + padding; x++)
                for (int y = -padding; y < h + padding; y++)
                    if (occupiedSet.Contains(new Vector2Int(origin.x + x, origin.y + y)))
                        return true;
            return false;
        }
    }
}
