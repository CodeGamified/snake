// Copyright CodeGamified 2025-2026
// MIT License — Snake
using System.Collections.Generic;
using UnityEngine;

namespace Snake.Game
{
    /// <summary>
    /// The Snake grid — a Width×Height cell grid.
    /// Tracks snake body segments, food position, and wall/self collisions.
    /// Row 0 = bottom, col 0 = left.
    /// </summary>
    public class SnakeGrid : MonoBehaviour
    {
        public int Width { get; private set; } = 20;
        public int Height { get; private set; } = 20;

        /// <summary>
        /// Grid values: 0 = empty, 1 = food, 2 = snake head, 3 = snake body.
        /// </summary>
        public int[,] Grid { get; private set; }

        // Snake body as ordered list — head at index 0, tail at end
        public List<(int row, int col)> Body { get; private set; } = new();

        // Current food position
        public (int row, int col) FoodPos { get; private set; }

        // Current direction the snake is moving
        public Direction CurrentDirection { get; set; } = Direction.Right;

        // Queued direction (set by player code, applied on next step)
        public Direction QueuedDirection { get; set; } = Direction.Right;

        // State
        public bool IsDead { get; private set; }
        public int FoodEaten { get; private set; }

        // Events
        public System.Action OnFoodEaten;
        public System.Action OnDied;
        public System.Action OnGridChanged;

        public void Initialize(int width = 20, int height = 20)
        {
            Width = width;
            Height = height;
            Grid = new int[Height, Width];
            Reset();
        }

        /// <summary>Reset the grid, snake, and food for a new game.</summary>
        public void Reset()
        {
            System.Array.Clear(Grid, 0, Grid.Length);
            Body.Clear();
            IsDead = false;
            FoodEaten = 0;
            CurrentDirection = Direction.Right;
            QueuedDirection = Direction.Right;

            // Spawn snake in center, 3 segments long, heading right
            int startRow = Height / 2;
            int startCol = Width / 2;
            Body.Add((startRow, startCol));       // head
            Body.Add((startRow, startCol - 1));   // body
            Body.Add((startRow, startCol - 2));   // tail

            foreach (var seg in Body)
                Grid[seg.row, seg.col] = 3;
            Grid[Body[0].row, Body[0].col] = 2; // mark head

            SpawnFood();
            OnGridChanged?.Invoke();
        }

        /// <summary>
        /// Advance the snake one step. Returns true if alive, false if dead.
        /// Called by MatchManager on each tick.
        /// </summary>
        public bool Step()
        {
            if (IsDead) return false;

            // Apply queued direction (prevent 180° reversal)
            if (!IsOpposite(QueuedDirection, CurrentDirection))
                CurrentDirection = QueuedDirection;

            // Calculate new head position
            var (headRow, headCol) = Body[0];
            var (dr, dc) = DirectionDelta(CurrentDirection);
            int newRow = headRow + dr;
            int newCol = headCol + dc;

            // Wall collision
            if (newRow < 0 || newRow >= Height || newCol < 0 || newCol >= Width)
            {
                IsDead = true;
                OnDied?.Invoke();
                return false;
            }

            // Self collision (check before moving — the tail will vacate unless we're growing)
            bool ateFood = (newRow == FoodPos.row && newCol == FoodPos.col);
            int cellValue = Grid[newRow, newCol];

            // If we're not eating, the tail will move — so if the new head IS the tail, that's OK
            if (!ateFood && cellValue == 3)
            {
                // Check if it's the tail (which is about to vacate)
                var tail = Body[Body.Count - 1];
                if (newRow != tail.row || newCol != tail.col)
                {
                    IsDead = true;
                    OnDied?.Invoke();
                    return false;
                }
            }

            // Move: add new head
            Body.Insert(0, (newRow, newCol));

            if (ateFood)
            {
                // Grow — don't remove tail
                FoodEaten++;
                Grid[newRow, newCol] = 2;

                // Old head becomes body
                if (Body.Count > 1)
                    Grid[Body[1].row, Body[1].col] = 3;

                SpawnFood();
                OnFoodEaten?.Invoke();
            }
            else
            {
                // Remove tail
                var tail = Body[Body.Count - 1];
                Grid[tail.row, tail.col] = 0;
                Body.RemoveAt(Body.Count - 1);

                // Update head/body markers
                Grid[newRow, newCol] = 2;
                if (Body.Count > 1)
                    Grid[Body[1].row, Body[1].col] = 3;
            }

            OnGridChanged?.Invoke();
            return true;
        }

        /// <summary>Spawn food on a random empty cell.</summary>
        private void SpawnFood()
        {
            var emptyCells = new List<(int row, int col)>();
            for (int r = 0; r < Height; r++)
                for (int c = 0; c < Width; c++)
                    if (Grid[r, c] == 0)
                        emptyCells.Add((r, c));

            if (emptyCells.Count == 0)
            {
                // Board full — win condition (extremely rare)
                return;
            }

            FoodPos = emptyCells[Random.Range(0, emptyCells.Count)];
            Grid[FoodPos.row, FoodPos.col] = 1;
        }

        /// <summary>Get the cell value at (row, col). Out-of-bounds = -1.</summary>
        public int GetCell(int row, int col)
        {
            if (row < 0 || row >= Height || col < 0 || col >= Width) return -1;
            return Grid[row, col];
        }

        /// <summary>Distance from snake head to food (Manhattan).</summary>
        public int FoodDistance()
        {
            if (Body.Count == 0) return 0;
            var head = Body[0];
            return Mathf.Abs(head.row - FoodPos.row) + Mathf.Abs(head.col - FoodPos.col);
        }

        /// <summary>
        /// Check if a direction is walkable from the head (no wall, no body).
        /// 1 = safe, 0 = blocked.
        /// </summary>
        public int IsDirectionSafe(Direction dir)
        {
            if (Body.Count == 0) return 0;
            var (dr, dc) = DirectionDelta(dir);
            int r = Body[0].row + dr;
            int c = Body[0].col + dc;
            if (r < 0 || r >= Height || c < 0 || c >= Width) return 0;
            int val = Grid[r, c];
            // Safe if empty or food; also safe if it's the tail (about to move)
            if (val == 0 || val == 1) return 1;
            if (val == 3)
            {
                var tail = Body[Body.Count - 1];
                if (r == tail.row && c == tail.col) return 1;
            }
            return 0;
        }

        // ═══════════════════════════════════════════════════════════════
        // HELPERS
        // ═══════════════════════════════════════════════════════════════

        public static (int dr, int dc) DirectionDelta(Direction dir)
        {
            return dir switch
            {
                Direction.Up    => ( 1,  0),
                Direction.Right => ( 0,  1),
                Direction.Down  => (-1,  0),
                Direction.Left  => ( 0, -1),
                _ => (0, 0)
            };
        }

        public static bool IsOpposite(Direction a, Direction b)
        {
            return (a == Direction.Up && b == Direction.Down) ||
                   (a == Direction.Down && b == Direction.Up) ||
                   (a == Direction.Left && b == Direction.Right) ||
                   (a == Direction.Right && b == Direction.Left);
        }
    }
}
