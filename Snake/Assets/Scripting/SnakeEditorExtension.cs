// Copyright CodeGamified 2025-2026
// MIT License — Snake
using System.Collections.Generic;
using CodeGamified.Editor;

namespace Snake.Scripting
{
    /// <summary>
    /// Editor extension for Snake — provides game-specific options
    /// to CodeEditorWindow's option tree for tap-to-code editing.
    /// </summary>
    public class SnakeEditorExtension : IEditorExtension
    {
        public List<EditorTypeInfo> GetAvailableTypes() => new();

        public List<EditorFuncInfo> GetAvailableFunctions() => new()
        {
            // Queries
            new EditorFuncInfo { Name = "get_head_row",      Hint = "snake head row",          ArgCount = 0 },
            new EditorFuncInfo { Name = "get_head_col",      Hint = "snake head col",          ArgCount = 0 },
            new EditorFuncInfo { Name = "get_food_row",      Hint = "food row",                ArgCount = 0 },
            new EditorFuncInfo { Name = "get_food_col",      Hint = "food col",                ArgCount = 0 },
            new EditorFuncInfo { Name = "get_direction",     Hint = "current dir (0-3)",       ArgCount = 0 },
            new EditorFuncInfo { Name = "get_length",        Hint = "snake length",            ArgCount = 0 },
            new EditorFuncInfo { Name = "get_score",         Hint = "current score",           ArgCount = 0 },
            new EditorFuncInfo { Name = "get_high_score",    Hint = "best score this session", ArgCount = 0 },
            new EditorFuncInfo { Name = "get_food_eaten",    Hint = "food eaten this round",   ArgCount = 0 },
            new EditorFuncInfo { Name = "get_food_distance", Hint = "Manhattan dist to food",  ArgCount = 0 },
            new EditorFuncInfo { Name = "get_grid_width",    Hint = "grid width",              ArgCount = 0 },
            new EditorFuncInfo { Name = "get_grid_height",   Hint = "grid height",             ArgCount = 0 },
            new EditorFuncInfo { Name = "get_safe_up",       Hint = "1 if up is safe",         ArgCount = 0 },
            new EditorFuncInfo { Name = "get_safe_right",    Hint = "1 if right is safe",      ArgCount = 0 },
            new EditorFuncInfo { Name = "get_safe_down",     Hint = "1 if down is safe",       ArgCount = 0 },
            new EditorFuncInfo { Name = "get_safe_left",     Hint = "1 if left is safe",       ArgCount = 0 },
            new EditorFuncInfo { Name = "get_input",         Hint = "keyboard input code",     ArgCount = 0 },
            new EditorFuncInfo { Name = "get_cell",          Hint = "cell at (row, col)",      ArgCount = 2 },

            // Commands
            new EditorFuncInfo { Name = "set_direction",     Hint = "set direction (0-3)",     ArgCount = 1 },
            new EditorFuncInfo { Name = "turn_left",         Hint = "turn 90° left",           ArgCount = 0 },
            new EditorFuncInfo { Name = "turn_right",        Hint = "turn 90° right",          ArgCount = 0 },
        };

        public List<EditorMethodInfo> GetMethodsForType(string typeName) => new();

        public List<string> GetVariableNameSuggestions() => new()
        {
            "head_r", "head_c", "food_r", "food_c",
            "dir", "safe", "dist", "inp", "length"
        };

        public List<string> GetStringLiteralSuggestions() => new();
    }
}
