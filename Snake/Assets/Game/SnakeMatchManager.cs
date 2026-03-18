// Copyright CodeGamified 2025-2026
// MIT License — Snake
using UnityEngine;
using CodeGamified.Time;

namespace Snake.Game
{
    /// <summary>
    /// Match manager — tick-based snake stepping, scoring, speed ramp, game over.
    /// The player's CODE sets the direction. This drives the clock.
    ///
    /// Tick model: snake moves one cell every `stepInterval` sim-seconds.
    /// Speed increases as the snake eats food.
    /// </summary>
    public class SnakeMatchManager : MonoBehaviour
    {
        private SnakeGrid _grid;

        // Config
        private float _baseStepInterval;

        // State
        public int Score { get; private set; }
        public int HighScore { get; private set; }
        public bool GameOver { get; private set; }
        public bool MatchInProgress { get; private set; }
        public int MatchesPlayed { get; private set; }

        // Timing
        private float _stepTimer;

        // Events
        public System.Action OnMatchStarted;
        public System.Action OnGameOver;
        public System.Action<int> OnFoodEaten;     // new score
        public System.Action OnSnakeStepped;

        public SnakeGrid Grid => _grid;

        public void Initialize(SnakeGrid grid, float baseStepInterval = 0.15f)
        {
            _grid = grid;
            _baseStepInterval = baseStepInterval;
        }

        public void StartMatch()
        {
            _grid.Reset();
            Score = 0;
            GameOver = false;
            MatchInProgress = true;
            _stepTimer = CurrentStepInterval;

            // Wire grid events
            _grid.OnFoodEaten -= OnGridFoodEaten;
            _grid.OnDied -= OnGridDied;
            _grid.OnFoodEaten += OnGridFoodEaten;
            _grid.OnDied += OnGridDied;

            OnMatchStarted?.Invoke();
        }

        private void Update()
        {
            if (!MatchInProgress || GameOver) return;
            if (SimulationTime.Instance == null || SimulationTime.Instance.isPaused) return;

            float dt = Time.deltaTime * (SimulationTime.Instance?.timeScale ?? 1f);
            _stepTimer -= dt;

            if (_stepTimer <= 0f)
            {
                _stepTimer = CurrentStepInterval;
                _grid.Step();
                OnSnakeStepped?.Invoke();
            }
        }

        /// <summary>Step interval decreases as snake grows. Min 0.05s.</summary>
        public float CurrentStepInterval =>
            Mathf.Max(0.05f, _baseStepInterval - (_grid.FoodEaten * 0.005f));

        // ═══════════════════════════════════════════════════════════════
        // PLAYER ACTIONS (called by IOHandler)
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Set the snake's next direction. 180° reversal is blocked by SnakeGrid.</summary>
        public void SetDirection(Direction dir)
        {
            _grid.QueuedDirection = dir;
        }

        /// <summary>Queue a turn relative to current direction. -1 = left, 1 = right.</summary>
        public void Turn(int delta)
        {
            int cur = (int)_grid.CurrentDirection;
            int next = ((cur + delta) % 4 + 4) % 4;
            _grid.QueuedDirection = (Direction)next;
        }

        // ═══════════════════════════════════════════════════════════════
        // GRID EVENT HANDLERS
        // ═══════════════════════════════════════════════════════════════

        private void OnGridFoodEaten()
        {
            Score += 10 + (_grid.FoodEaten * 2); // increasing reward
            OnFoodEaten?.Invoke(Score);
        }

        private void OnGridDied()
        {
            GameOver = true;
            MatchInProgress = false;
            MatchesPlayed++;
            if (Score > HighScore) HighScore = Score;
            OnGameOver?.Invoke();
        }

        private void OnDestroy()
        {
            if (_grid != null)
            {
                _grid.OnFoodEaten -= OnGridFoodEaten;
                _grid.OnDied -= OnGridDied;
            }
        }
    }
}
