using System;
using System.Collections.Generic;

namespace TeaLeaves
{
    public enum SlideDirection { Up, Down, Left, Right }
    public enum PaintColor { None, Cyan, Magenta, Yellow, Green }

    /// <summary>
    /// Pure C# game logic for PIKUI slide-painting puzzle.
    /// No Godot dependencies - fully testable with dotnet test.
    /// Each level has a max slide distance controlling how far the orb travels per move.
    /// Level 1: max 2 (all tiles reachable, tutorial)
    /// Level 2: max 3 + walls (medium challenge)
    /// Level 3: max 4 + walls (hard challenge)
    /// </summary>
    public class PikuiLogic
    {
        public const int MaxLevels = 3;

        private readonly int _size;
        private readonly bool[,] _walls;
        private readonly PaintColor[,] _tiles;
        private readonly int _maxSlide;
        private int _px, _py;

        public int GridSize => _size;
        public int MaxSlideDistance => _maxSlide;
        public (int x, int y) PlayerPos => (_px, _py);
        public int Score { get; private set; }
        public int Combo { get; private set; }
        public int MaxCombo { get; private set; }
        public int Moves { get; private set; }
        public int PaintedCount { get; private set; }
        public int TotalPaintable { get; private set; }
        public int Par { get; private set; }
        public bool IsComplete => PaintedCount >= TotalPaintable;
        public float Progress => TotalPaintable > 0 ? (float)PaintedCount / TotalPaintable : 0f;

        public PikuiLogic(int level)
        {
            var (size, walls, par, maxSlide) = GetLevelData(level);
            _size = size;
            _walls = walls;
            Par = par;
            _maxSlide = maxSlide;
            _tiles = new PaintColor[_size, _size];

            for (int x = 0; x < _size; x++)
                for (int y = 0; y < _size; y++)
                    if (!_walls[x, y]) TotalPaintable++;

            _px = _size / 2;
            _py = _size / 2;
            _tiles[_px, _py] = PaintColor.Cyan;
            PaintedCount = 1;
        }

        public static PaintColor DirectionToColor(SlideDirection dir) => dir switch
        {
            SlideDirection.Up => PaintColor.Cyan,
            SlideDirection.Right => PaintColor.Magenta,
            SlideDirection.Down => PaintColor.Yellow,
            SlideDirection.Left => PaintColor.Green,
            _ => PaintColor.None
        };

        public static (int dx, int dy) DirectionToVector(SlideDirection dir) => dir switch
        {
            SlideDirection.Up => (0, -1),
            SlideDirection.Down => (0, 1),
            SlideDirection.Left => (-1, 0),
            SlideDirection.Right => (1, 0),
            _ => (0, 0)
        };

        public bool IsCorner(int x, int y) =>
            (x == 0 || x == _size - 1) && (y == 0 || y == _size - 1);

        public List<(int x, int y, bool wasNew)> Slide(SlideDirection dir)
        {
            var (dx, dy) = DirectionToVector(dir);
            var color = DirectionToColor(dir);
            var path = new List<(int x, int y, bool wasNew)>();
            int newTiles = 0;
            int distance = 0;

            int nx = _px + dx;
            int ny = _py + dy;

            while (nx >= 0 && nx < _size && ny >= 0 && ny < _size
                   && !_walls[nx, ny] && distance < _maxSlide)
            {
                bool wasNew = _tiles[nx, ny] == PaintColor.None;
                _tiles[nx, ny] = color;
                path.Add((nx, ny, wasNew));

                if (wasNew)
                {
                    newTiles++;
                    PaintedCount++;
                    Score += IsCorner(nx, ny) ? 200 : 100;
                }
                else
                {
                    Score += 25;
                }

                _px = nx;
                _py = ny;
                nx += dx;
                ny += dy;
                distance++;
            }

            Moves++;

            if (newTiles > 0)
            {
                Combo++;
                if (Combo > MaxCombo) MaxCombo = Combo;
                Score += newTiles * Combo * 10;
            }
            else if (path.Count == 0)
            {
                Combo = 0;
            }

            return path;
        }

        public PaintColor GetTile(int x, int y) => _tiles[x, y];

        public bool IsWall(int x, int y) =>
            x < 0 || x >= _size || y < 0 || y >= _size || _walls[x, y];

        public int CompletionBonus()
        {
            if (!IsComplete) return 0;
            int moveBonus = Math.Max(0, Par * 2 - Moves) * 50;
            int comboBonus = MaxCombo * 100;
            return moveBonus + comboBonus;
        }

        private static (int size, bool[,] walls, int par, int maxSlide) GetLevelData(int level)
            => level switch
        {
            1 => GetLevel1(),
            2 => GetLevel2(),
            3 => GetLevel3(),
            _ => GetLevel1()
        };

        private static (int, bool[,], int, int) GetLevel1()
        {
            // Max slide 2: all tiles reachable on 7x7 without walls
            return (7, new bool[7, 7], 26, 2);
        }

        private static (int, bool[,], int, int) GetLevel2()
        {
            // Max slide 3 + diamond walls: creates interesting corridors
            var w = new bool[7, 7];
            w[2, 1] = true; w[4, 1] = true;
            w[1, 3] = true; w[5, 3] = true;
            w[2, 5] = true; w[4, 5] = true;
            return (7, w, 20, 3);
        }

        private static (int, bool[,], int, int) GetLevel3()
        {
            // Max slide 4 + corner blocks: complex paths required
            var w = new bool[7, 7];
            w[1, 1] = true; w[2, 1] = true; w[4, 1] = true; w[5, 1] = true;
            w[1, 5] = true; w[2, 5] = true; w[4, 5] = true; w[5, 5] = true;
            return (7, w, 18, 4);
        }
    }
}
