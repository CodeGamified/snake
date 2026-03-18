// Copyright CodeGamified 2025-2026
// MIT License — Snake
using UnityEngine;
using CodeGamified.Engine;
using CodeGamified.Engine.Compiler;
using CodeGamified.Engine.Runtime;
using CodeGamified.Time;
using Snake.Game;

namespace Snake.Scripting
{
    /// <summary>
    /// SnakeProgram — code-controlled snake AI.
    /// Subclasses ProgramBehaviour from .engine.
    ///
    /// EXECUTION MODEL (tick-based, deterministic):
    ///   - Each simulation tick (~20 ops/sec sim-time), the script runs from the top
    ///   - Memory (variables) persists across ticks
    ///   - PC resets to 0 each tick
    ///   - Each tick the script reads game state and sets a direction
    ///   - The snake steps on a separate timer (SnakeMatchManager)
    ///   - Results are IDENTICAL at 0.5x, 1x, 100x speed
    ///
    /// BUILTINS:
    ///   get_head_row/col()    → snake head position
    ///   get_food_row/col()    → food position
    ///   get_direction()       → current direction (0-3)
    ///   get_safe_up/right/down/left() → 1 if direction is safe
    ///   get_food_distance()   → Manhattan distance to food
    ///   set_direction(d)      → set direction (0=up,1=right,2=down,3=left)
    ///   turn_left/right()     → turn 90° relative to current direction
    /// </summary>
    public class SnakeProgram : ProgramBehaviour
    {
        private SnakeMatchManager _match;
        private SnakeGrid _grid;
        private SnakeIOHandler _ioHandler;
        private SnakeCompilerExtension _compilerExt;

        public const float OPS_PER_SECOND = 20f;
        private float _opAccumulator;

        private const string DEFAULT_CODE = @"# 🐍 SNAKE — Write your snake AI!
# Your script runs at 20 ops/sec (sim-time).
# When it finishes, it restarts from the top.
# Variables persist — use them to track state.
#
# BUILTINS — Queries:
#   get_head_row()       → snake head row
#   get_head_col()       → snake head col
#   get_food_row()       → food row
#   get_food_col()       → food col
#   get_direction()      → current dir (0=Up,1=Right,2=Down,3=Left)
#   get_length()         → snake length
#   get_score()          → current score
#   get_high_score()     → best score this session
#   get_food_eaten()     → food eaten this round
#   get_food_distance()  → Manhattan distance to food
#   get_grid_width()     → grid width
#   get_grid_height()    → grid height
#   get_cell(row, col)   → 0=empty, 1=food, 2=head, 3=body
#   get_safe_up()        → 1 if up is safe
#   get_safe_right()     → 1 if right is safe
#   get_safe_down()      → 1 if down is safe
#   get_safe_left()      → 1 if left is safe
#   get_input()          → keyboard (0=none,1=up,2=right,3=down,4=left)
#
# BUILTINS — Commands:
#   set_direction(d)     → set next direction (0-3)
#   turn_left()          → turn 90° left
#   turn_right()         → turn 90° right
#
# This starter passes keyboard input through:
inp = get_input()
if inp == 1:
    set_direction(0)
if inp == 2:
    set_direction(1)
if inp == 3:
    set_direction(2)
if inp == 4:
    set_direction(3)
";

        public string CurrentSourceCode => _sourceCode;
        public System.Action OnCodeChanged;

        public void Initialize(SnakeMatchManager match, SnakeGrid grid,
                               string initialCode = null, string programName = "SnakeAI")
        {
            _match = match;
            _grid = grid;
            _compilerExt = new SnakeCompilerExtension();

            _programName = programName;
            _sourceCode = initialCode ?? DEFAULT_CODE;
            _autoRun = true;

            LoadAndRun(_sourceCode);
        }

        protected override void Update()
        {
            if (_executor == null || _program == null || _isPaused) return;
            if (_match == null || !_match.MatchInProgress || _match.GameOver) return;

            float timeScale = SimulationTime.Instance?.timeScale ?? 1f;
            if (SimulationTime.Instance != null && SimulationTime.Instance.isPaused) return;

            float simDelta = UnityEngine.Time.deltaTime * timeScale;
            _opAccumulator += simDelta * OPS_PER_SECOND;

            int opsToRun = (int)_opAccumulator;
            _opAccumulator -= opsToRun;

            for (int i = 0; i < opsToRun; i++)
            {
                if (_executor.State.IsHalted)
                {
                    _executor.State.PC = 0;
                    _executor.State.IsHalted = false;
                }
                _executor.ExecuteOne();
            }

            if (opsToRun > 0)
                ProcessEvents();
        }

        protected override IGameIOHandler CreateIOHandler()
        {
            _ioHandler = new SnakeIOHandler(_match, _grid);
            return _ioHandler;
        }

        protected override CompiledProgram CompileSource(string source, string name)
        {
            return PythonCompiler.Compile(source, name, _compilerExt);
        }

        protected override void ProcessEvents()
        {
            if (_executor?.State == null) return;
            while (_executor.State.OutputEvents.Count > 0)
                _executor.State.OutputEvents.Dequeue();
        }

        public void UploadCode(string newSource)
        {
            _sourceCode = newSource ?? DEFAULT_CODE;
            LoadAndRun(_sourceCode);
            Debug.Log($"[SnakeAI] Uploaded new code ({_program?.Instructions?.Length ?? 0} instructions)");
            OnCodeChanged?.Invoke();
        }

        public void ResetExecution()
        {
            if (_executor?.State == null) return;
            _executor.State.Reset();
            _opAccumulator = 0f;
        }
    }
}
