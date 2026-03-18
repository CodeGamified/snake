// Copyright CodeGamified 2025-2026
// MIT License — Snake
using CodeGamified.TUI;
using Snake.Scripting;

namespace Snake.UI
{
    /// <summary>
    /// Thin adapter — wires a SnakeProgram into the engine's CodeDebuggerWindow
    /// via SnakeDebuggerData (IDebuggerDataSource).
    /// </summary>
    public class SnakeCodeDebugger : CodeDebuggerWindow
    {
        protected override void Awake()
        {
            base.Awake();
            windowTitle = "CODE";
        }

        public void Bind(SnakeProgram program)
        {
            SetDataSource(new SnakeDebuggerData(program));
        }
    }
}
