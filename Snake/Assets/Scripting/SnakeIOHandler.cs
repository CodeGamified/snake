// Copyright CodeGamified 2025-2026
// MIT License — Snake
using CodeGamified.Engine;
using CodeGamified.Time;
using Snake.Game;

namespace Snake.Scripting
{
    /// <summary>
    /// Game I/O handler for Snake — bridges CUSTOM opcodes to game state.
    /// </summary>
    public class SnakeIOHandler : IGameIOHandler
    {
        private readonly SnakeMatchManager _match;
        private readonly SnakeGrid _grid;

        public SnakeIOHandler(SnakeMatchManager match, SnakeGrid grid)
        {
            _match = match;
            _grid = grid;
        }

        public bool PreExecute(Instruction inst, MachineState state) => true;

        public void ExecuteIO(Instruction inst, MachineState state)
        {
            int op = (int)inst.Op - (int)OpCode.CUSTOM_0;

            switch ((SnakeOpCode)op)
            {
                // ── Queries → R0 ──
                case SnakeOpCode.GET_HEAD_ROW:
                    state.SetRegister(0, _grid.Body.Count > 0 ? _grid.Body[0].row : 0);
                    break;
                case SnakeOpCode.GET_HEAD_COL:
                    state.SetRegister(0, _grid.Body.Count > 0 ? _grid.Body[0].col : 0);
                    break;
                case SnakeOpCode.GET_FOOD_ROW:
                    state.SetRegister(0, _grid.FoodPos.row);
                    break;
                case SnakeOpCode.GET_FOOD_COL:
                    state.SetRegister(0, _grid.FoodPos.col);
                    break;
                case SnakeOpCode.GET_DIRECTION:
                    state.SetRegister(0, (int)_grid.CurrentDirection);
                    break;
                case SnakeOpCode.GET_LENGTH:
                    state.SetRegister(0, _grid.Body.Count);
                    break;
                case SnakeOpCode.GET_SCORE:
                    state.SetRegister(0, _match.Score);
                    break;
                case SnakeOpCode.GET_GRID_W:
                    state.SetRegister(0, _grid.Width);
                    break;
                case SnakeOpCode.GET_GRID_H:
                    state.SetRegister(0, _grid.Height);
                    break;
                case SnakeOpCode.GET_FOOD_DIST:
                    state.SetRegister(0, _grid.FoodDistance());
                    break;
                case SnakeOpCode.GET_SAFE_UP:
                    state.SetRegister(0, _grid.IsDirectionSafe(Direction.Up));
                    break;
                case SnakeOpCode.GET_SAFE_RIGHT:
                    state.SetRegister(0, _grid.IsDirectionSafe(Direction.Right));
                    break;
                case SnakeOpCode.GET_SAFE_DOWN:
                    state.SetRegister(0, _grid.IsDirectionSafe(Direction.Down));
                    break;
                case SnakeOpCode.GET_SAFE_LEFT:
                    state.SetRegister(0, _grid.IsDirectionSafe(Direction.Left));
                    break;
                case SnakeOpCode.GET_INPUT:
                    state.SetRegister(0, SnakeInputProvider.Instance != null
                        ? SnakeInputProvider.Instance.CurrentInput : 0f);
                    break;
                case SnakeOpCode.GET_HIGH_SCORE:
                    state.SetRegister(0, _match.HighScore);
                    break;
                case SnakeOpCode.GET_FOOD_EATEN:
                    state.SetRegister(0, _grid.FoodEaten);
                    break;

                // ── Two-arg query ──
                case SnakeOpCode.GET_CELL:
                    int row = (int)state.GetRegister(0);
                    int col = (int)state.GetRegister(1);
                    state.SetRegister(0, _grid.GetCell(row, col));
                    break;

                // ── Commands ──
                case SnakeOpCode.SET_DIRECTION:
                    int dir = (int)state.GetRegister(0);
                    if (dir >= 0 && dir <= 3)
                        _match.SetDirection((Direction)dir);
                    break;
                case SnakeOpCode.TURN_LEFT:
                    _match.Turn(-1);
                    break;
                case SnakeOpCode.TURN_RIGHT:
                    _match.Turn(1);
                    break;
            }
        }

        public float GetTimeScale() => SimulationTime.Instance?.timeScale ?? 1f;
        public double GetSimulationTime() => SimulationTime.Instance?.simulationTime ?? 0.0;
    }
}
