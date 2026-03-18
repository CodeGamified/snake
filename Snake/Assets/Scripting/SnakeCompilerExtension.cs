// Copyright CodeGamified 2025-2026
// MIT License — Snake
using System.Collections.Generic;
using CodeGamified.Engine;
using CodeGamified.Engine.Compiler;

namespace Snake.Scripting
{
    /// <summary>
    /// Snake-specific opcodes mapped to CUSTOM_0..CUSTOM_N.
    /// </summary>
    public enum SnakeOpCode
    {
        // ── Queries (read game state → R0) ──
        GET_HEAD_ROW     = 0,   // snake head row
        GET_HEAD_COL     = 1,   // snake head col
        GET_FOOD_ROW     = 2,   // food row
        GET_FOOD_COL     = 3,   // food col
        GET_DIRECTION    = 4,   // current direction (0=Up,1=Right,2=Down,3=Left)
        GET_LENGTH       = 5,   // snake length
        GET_SCORE        = 6,   // current score
        GET_GRID_W       = 7,   // grid width
        GET_GRID_H       = 8,   // grid height
        GET_CELL         = 9,   // cell at (R0=row, R1=col) → R0 (0=empty,1=food,2=head,3=body)
        GET_FOOD_DIST    = 10,  // Manhattan distance to food
        GET_SAFE_UP      = 11,  // 1 if moving up is safe, 0 if wall/body
        GET_SAFE_RIGHT   = 12,  // 1 if moving right is safe
        GET_SAFE_DOWN    = 13,  // 1 if moving down is safe
        GET_SAFE_LEFT    = 14,  // 1 if moving left is safe
        GET_INPUT        = 15,  // keyboard input (0=none,1=up,2=right,3=down,4=left)
        GET_HIGH_SCORE   = 16,  // best score this session
        GET_FOOD_EATEN   = 17,  // total food eaten this round

        // ── Commands ──
        SET_DIRECTION    = 18,  // set direction (R0: 0=Up,1=Right,2=Down,3=Left)
        TURN_LEFT        = 19,  // turn 90° left relative to current direction
        TURN_RIGHT       = 20,  // turn 90° right relative to current direction
    }

    /// <summary>
    /// Compiler extension for Snake — registers builtins for snake queries and commands.
    /// </summary>
    public class SnakeCompilerExtension : ICompilerExtension
    {
        public void RegisterBuiltins(CompilerContext ctx) { }

        public bool TryCompileCall(string functionName, List<AstNodes.ExprNode> args,
                                   CompilerContext ctx, int sourceLine)
        {
            switch (functionName)
            {
                // ── Queries: no args, result in R0 ──
                case "get_head_row":
                    Emit(ctx, SnakeOpCode.GET_HEAD_ROW, sourceLine, "get_head_row → R0");
                    return true;
                case "get_head_col":
                    Emit(ctx, SnakeOpCode.GET_HEAD_COL, sourceLine, "get_head_col → R0");
                    return true;
                case "get_food_row":
                    Emit(ctx, SnakeOpCode.GET_FOOD_ROW, sourceLine, "get_food_row → R0");
                    return true;
                case "get_food_col":
                    Emit(ctx, SnakeOpCode.GET_FOOD_COL, sourceLine, "get_food_col → R0");
                    return true;
                case "get_direction":
                    Emit(ctx, SnakeOpCode.GET_DIRECTION, sourceLine, "get_direction → R0");
                    return true;
                case "get_length":
                    Emit(ctx, SnakeOpCode.GET_LENGTH, sourceLine, "get_length → R0");
                    return true;
                case "get_score":
                    Emit(ctx, SnakeOpCode.GET_SCORE, sourceLine, "get_score → R0");
                    return true;
                case "get_grid_width":
                    Emit(ctx, SnakeOpCode.GET_GRID_W, sourceLine, "get_grid_width → R0");
                    return true;
                case "get_grid_height":
                    Emit(ctx, SnakeOpCode.GET_GRID_H, sourceLine, "get_grid_height → R0");
                    return true;
                case "get_food_distance":
                    Emit(ctx, SnakeOpCode.GET_FOOD_DIST, sourceLine, "get_food_distance → R0");
                    return true;
                case "get_safe_up":
                    Emit(ctx, SnakeOpCode.GET_SAFE_UP, sourceLine, "get_safe_up → R0");
                    return true;
                case "get_safe_right":
                    Emit(ctx, SnakeOpCode.GET_SAFE_RIGHT, sourceLine, "get_safe_right → R0");
                    return true;
                case "get_safe_down":
                    Emit(ctx, SnakeOpCode.GET_SAFE_DOWN, sourceLine, "get_safe_down → R0");
                    return true;
                case "get_safe_left":
                    Emit(ctx, SnakeOpCode.GET_SAFE_LEFT, sourceLine, "get_safe_left → R0");
                    return true;
                case "get_input":
                    Emit(ctx, SnakeOpCode.GET_INPUT, sourceLine, "get_input → R0");
                    return true;
                case "get_high_score":
                    Emit(ctx, SnakeOpCode.GET_HIGH_SCORE, sourceLine, "get_high_score → R0");
                    return true;
                case "get_food_eaten":
                    Emit(ctx, SnakeOpCode.GET_FOOD_EATEN, sourceLine, "get_food_eaten → R0");
                    return true;

                // ── Two-arg query: get_cell(row, col) ──
                case "get_cell":
                    if (args != null && args.Count >= 2)
                    {
                        args[0].Compile(ctx);              // row → R0
                        ctx.Emit(OpCode.PUSH, 0);          // save row
                        args[1].Compile(ctx);              // col → R0
                        ctx.Emit(OpCode.MOV, 1, 0);        // col → R1
                        ctx.Emit(OpCode.POP, 0);           // restore row → R0
                    }
                    ctx.Emit(OpCode.CUSTOM_0 + (int)SnakeOpCode.GET_CELL, 0, 0, 0, sourceLine,
                        "get_cell(R0=row, R1=col) → R0");
                    return true;

                // ── Commands ──
                case "set_direction":
                    if (args != null && args.Count > 0)
                        args[0].Compile(ctx); // direction → R0
                    ctx.Emit(OpCode.CUSTOM_0 + (int)SnakeOpCode.SET_DIRECTION, 0, 0, 0, sourceLine,
                        "set_direction(R0)");
                    return true;
                case "turn_left":
                    Emit(ctx, SnakeOpCode.TURN_LEFT, sourceLine, "turn_left");
                    return true;
                case "turn_right":
                    Emit(ctx, SnakeOpCode.TURN_RIGHT, sourceLine, "turn_right");
                    return true;

                default:
                    return false;
            }
        }

        private static void Emit(CompilerContext ctx, SnakeOpCode op, int line, string comment)
        {
            ctx.Emit(OpCode.CUSTOM_0 + (int)op, 0, 0, 0, line, comment);
        }

        public bool TryCompileMethodCall(string objectName, string methodName,
                                         List<AstNodes.ExprNode> args,
                                         CompilerContext ctx, int sourceLine) => false;

        public bool TryCompileObjectDecl(string typeName, string varName,
                                         List<AstNodes.ExprNode> constructorArgs,
                                         CompilerContext ctx, int sourceLine) => false;
    }
}
