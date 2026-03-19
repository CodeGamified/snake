// Copyright CodeGamified 2025-2026
// MIT License — Snake
using System.Collections.Generic;
using UnityEngine;
using CodeGamified.Engine;
using CodeGamified.Engine.Runtime;
using CodeGamified.TUI;
using Snake.Scripting;
using static Snake.Scripting.SnakeOpCode;

namespace Snake.UI
{
    /// <summary>
    /// Adapts a SnakeProgram into the engine's IDebuggerDataSource contract.
    /// </summary>
    public class SnakeDebuggerData : IDebuggerDataSource
    {
        private readonly SnakeProgram _program;
        private readonly string _label;

        public SnakeDebuggerData(SnakeProgram program, string label = null)
        {
            _program = program;
            _label = label;
        }

        public string ProgramName => _label ?? _program?.ProgramName ?? "SnakeAI";
        public string[] SourceLines => _program?.Program?.SourceLines;
        public bool HasLiveProgram =>
            _program != null && _program.Executor != null && _program.Program != null
            && _program.Program.Instructions != null && _program.Program.Instructions.Length > 0;
        public int PC
        {
            get
            {
                var s = _program?.State;
                if (s == null) return 0;
                return s.LastExecutedPC >= 0 ? s.LastExecutedPC : s.PC;
            }
        }
        public long CycleCount => _program?.State?.CycleCount ?? 0;

        public string StatusString
        {
            get
            {
                if (_program == null || _program.Executor == null)
                    return TUIColors.Dimmed("NO PROGRAM");
                var state = _program.State;
                if (state == null) return TUIColors.Dimmed("NO STATE");
                int instCount = _program.Program?.Instructions?.Length ?? 0;
                return TUIColors.Fg(TUIColors.BrightGreen, $"TICK {instCount} inst");
            }
        }

        public List<string> BuildSourceLines(int pc, int scrollOffset, int maxRows)
        {
            var lines = new List<string>();
            var src = SourceLines;
            if (src == null) return lines;

            int activeLine = -1;
            int activeEnd = -1;
            bool isHalt = false;
            Instruction activeInst = default;
            if (HasLiveProgram && _program.Program.Instructions.Length > 0
                && pc < _program.Program.Instructions.Length)
            {
                activeInst = _program.Program.Instructions[pc];
                activeLine = activeInst.SourceLine - 1;
                isHalt = activeInst.Op == OpCode.HALT;
                if (activeLine >= 0)
                    activeEnd = SourceHighlight.GetContinuationEnd(src, activeLine);
            }

            if (scrollOffset == 0 && lines.Count < maxRows)
            {
                string whileLine = "while True:";
                if (isHalt)
                    lines.Add(TUIColors.Fg(TUIColors.BrightGreen, $"  {TUIGlyphs.ArrowR}   {whileLine}"));
                else
                    lines.Add($"  {TUIColors.Dimmed(TUIGlyphs.ArrowR)}   {SynthwaveHighlighter.Highlight(whileLine)}");
            }

            int tokenLine = -1;
            if (activeLine >= 0)
            {
                string token = SourceHighlight.GetSourceToken(activeInst);
                if (token != null)
                {
                    for (int k = activeLine; k <= activeEnd; k++)
                    {
                        if (src[k].IndexOf(token) >= 0) { tokenLine = k; break; }
                    }
                }
                if (tokenLine < 0) tokenLine = activeLine;
            }

            // Auto-scroll to keep active source line visible
            int focusLine = tokenLine >= 0 ? tokenLine : activeLine;
            if (focusLine >= 0 && src.Length > maxRows)
                scrollOffset = Mathf.Clamp(focusLine - maxRows / 3, 0, src.Length - maxRows);

            for (int i = scrollOffset; i < src.Length && lines.Count < maxRows; i++)
            {
                if (i == tokenLine)
                {
                    lines.Add(SourceHighlight.HighlightActiveLine(
                        src[i], $" {i + 1:D3}      ", activeInst));
                }
                else
                {
                    string num = TUIColors.Dimmed($"{i + 1:D3}");
                    lines.Add($" {num}      {SynthwaveHighlighter.Highlight(src[i])}");
                }
            }
            return lines;
        }

        public List<string> BuildMachineLines(int pc, int maxRows)
        {
            var lines = new List<string>();
            if (!HasLiveProgram) return lines;

            var instructions = _program.Program.Instructions;
            int total = instructions.Length;

            int offset = 0;
            if (total > maxRows)
                offset = Mathf.Clamp(pc - maxRows / 3, 0, total - maxRows);
            int visibleCount = Mathf.Min(maxRows, total);

            for (int j = 0; j < visibleCount; j++)
            {
                int i = offset + j;
                var inst = instructions[i];
                bool isPC = (i == pc);
                string asm = inst.ToAssembly(FormatSnakeOp);
                if (isPC)
                {
                    lines.Add(TUIColors.Fg(TUIColors.BrightGreen, $" {i:X3}  {asm}"));
                }
                else
                {
                    string addr = TUIColors.Dimmed($"{i:X3}");
                    lines.Add($" {addr}  {SynthwaveHighlighter.HighlightAsm(asm)}");
                }
            }
            return lines;
        }

        public List<string> BuildStateLines()
        {
            if (!HasLiveProgram) return new List<string>();
            var s = _program.State;
            int displayPC = s.LastExecutedPC >= 0 ? s.LastExecutedPC : s.PC;
            return TUIWidgets.BuildStateLines(
                s.Registers, s.LastRegisterModified,
                s.Flags, displayPC, s.Stack.Count,
                s.NameToAddress, s.Memory);
        }

        static string FormatSnakeOp(Instruction inst)
        {
            int id = (int)inst.Op - (int)OpCode.CUSTOM_0;
            return (SnakeOpCode)id switch
            {
                GET_HEAD_ROW   => "INP R0, HEAD.R",
                GET_HEAD_COL   => "INP R0, HEAD.C",
                GET_FOOD_ROW   => "INP R0, FOOD.R",
                GET_FOOD_COL   => "INP R0, FOOD.C",
                GET_DIRECTION  => "INP R0, DIR",
                GET_LENGTH     => "INP R0, LEN",
                GET_SCORE      => "INP R0, SCORE",
                GET_GRID_W     => "INP R0, GRD.W",
                GET_GRID_H     => "INP R0, GRD.H",
                GET_CELL       => "INP R0, CELL",
                GET_FOOD_DIST  => "INP R0, F.DST",
                GET_SAFE_UP    => "INP R0, SAFE.U",
                GET_SAFE_RIGHT => "INP R0, SAFE.R",
                GET_SAFE_DOWN  => "INP R0, SAFE.D",
                GET_SAFE_LEFT  => "INP R0, SAFE.L",
                GET_INPUT      => "INP R0, INPUT",
                GET_HIGH_SCORE => "INP R0, HI.SC",
                GET_FOOD_EATEN => "INP R0, F.EAT",
                SET_DIRECTION  => "OUT DIR, R0",
                TURN_LEFT      => "OUT TURN.L",
                TURN_RIGHT     => "OUT TURN.R",
                _              => $"IO.{id,2} {inst.Arg0}, {inst.Arg1}"
            };
        }
    }
}
