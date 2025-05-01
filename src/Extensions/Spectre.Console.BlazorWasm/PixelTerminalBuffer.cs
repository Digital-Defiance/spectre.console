using System;
using System.Globalization;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Spectre.Console.BlazorWasm
{
    // Represents a single cell in the terminal buffer
    public class TerminalCell
    {
        public string Char { get; set; } = " ";
        public string FgColor { get; set; } = "#fff";
        public string BgColor { get; set; } = "#000";
        public bool Bold { get; set; }
        public bool Underline { get; set; }
        // Add more attributes as needed
    }

    // Virtual terminal buffer for pixel-based rendering
    public class PixelTerminalBuffer
    {
        public int Width { get; }
        public int Height { get; }
        public TerminalCell[,] Buffer { get; }

        public int CursorX { get; set; }
        public int CursorY { get; set; }

        public PixelTerminalBuffer(int width, int height)
        {
            Width = width;
            Height = height;
            Buffer = new TerminalCell[width, height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    Buffer[x, y] = new TerminalCell();
        }

        // Write a string (single grapheme) at the current cursor position
        public void WriteChar(string s, string fg = "#fff", string bg = "#000", bool bold = false, bool underline = false)
        {
            if (CursorX < 0 || CursorX >= Width || CursorY < 0 || CursorY >= Height)
                return;
            Buffer[CursorX, CursorY].Char = s;
            Buffer[CursorX, CursorY].FgColor = fg;
            Buffer[CursorX, CursorY].BgColor = bg;
            Buffer[CursorX, CursorY].Bold = bold;
            Buffer[CursorX, CursorY].Underline = underline;

            // Determine width (1 for most, 2 for wide chars/emoji)
            int width = GetCharWidth(s);
            CursorX += width;
            if (CursorX >= Width)
            {
                CursorX = 0;
                CursorY++;
            }
        }

        // Write a string at the current cursor position, with wrapping and newline support
        public void WriteString(string s, string fg = "#fff", string bg = "#000", bool bold = false, bool underline = false)
        {
            var enumerator = StringInfo.GetTextElementEnumerator(s);
            while (enumerator.MoveNext())
            {
                string textElement = enumerator.GetTextElement();
                if (textElement == "\n")
                {
                    CursorX = 0;
                    CursorY++;
                }
                else
                {
                    WriteChar(textElement, fg, bg, bold, underline);
                    if (CursorX >= Width)
                    {
                        CursorX = 0;
                        CursorY++;
                    }
                }
                if (CursorY >= Height)
                    break;
            }
        }

        // Improved ANSI SGR parser for color and style (supports nested SGR, 256/truecolor, emoji/unicode)
        public void WriteAnsi(string ansi)
        {
            var ansiRegex = new Regex(@"\x1B\[(?<codes>[0-9;]*)m");
            int lastIndex = 0;

            // Style stack for nested SGR
            var styleStack = new Stack<(string fg, string bg, bool bold, bool underline)>();
            string fg = "#fff";
            string bg = "#000";
            bool bold = false;
            bool underline = false;

            foreach (Match match in ansiRegex.Matches(ansi))
            {
                int idx = match.Index;
                if (idx > lastIndex)
                {
                    string text = ansi.Substring(lastIndex, idx - lastIndex);
                    WriteString(text, fg, bg, bold, underline);
                }

                string[] codes = match.Groups["codes"].Value.Split(';');
                int i = 0;
                while (i < codes.Length)
                {
                    string codeStr = codes[i];
                    if (string.IsNullOrEmpty(codeStr))
                    {
                        i++;
                        continue;
                    }
                    if (!int.TryParse(codeStr, out int code))
                    {
                        i++;
                        continue;
                    }

                    switch (code)
                    {
                        case 0: // Reset
                            fg = "#fff";
                            bg = "#000";
                            bold = false;
                            underline = false;
                            styleStack.Clear();
                            break;
                        case 1: // Bold
                            bold = true;
                            break;
                        case 4: // Underline
                            underline = true;
                            break;
                        case 22: // Normal intensity
                            bold = false;
                            break;
                        case 24: // Underline off
                            underline = false;
                            break;
                        // Foreground colors 30-37
                        case int n when (n >= 30 && n <= 37):
                            fg = AnsiColorToHex(n - 30, false);
                            break;
                        // Foreground bright 90-97
                        case int n when (n >= 90 && n <= 97):
                            fg = AnsiColorToHex(n - 90, true);
                            break;
                        // Background colors 40-47
                        case int n when (n >= 40 && n <= 47):
                            bg = AnsiColorToHex(n - 40, false);
                            break;
                        // Background bright 100-107
                        case int n when (n >= 100 && n <= 107):
                            bg = AnsiColorToHex(n - 100, true);
                            break;
                        // 256-color and truecolor (foreground)
                        case 38:
                            if (i + 1 < codes.Length)
                            {
                                if (codes[i + 1] == "5" && i + 2 < codes.Length) // 256-color
                                {
                                    if (int.TryParse(codes[i + 2], out int colorIdx))
                                    {
                                        fg = Ansi256ToHex(colorIdx);
                                        i += 2;
                                    }
                                }
                                else if (codes[i + 1] == "2" && i + 4 < codes.Length) // truecolor
                                {
                                    if (int.TryParse(codes[i + 2], out int r) &&
                                        int.TryParse(codes[i + 3], out int g) &&
                                        int.TryParse(codes[i + 4], out int b))
                                    {
                                        fg = $"#{r:X2}{g:X2}{b:X2}";
                                        i += 4;
                                    }
                                }
                            }
                            break;
                        // 256-color and truecolor (background)
                        case 48:
                            if (i + 1 < codes.Length)
                            {
                                if (codes[i + 1] == "5" && i + 2 < codes.Length) // 256-color
                                {
                                    if (int.TryParse(codes[i + 2], out int colorIdx))
                                    {
                                        bg = Ansi256ToHex(colorIdx);
                                        i += 2;
                                    }
                                }
                                else if (codes[i + 1] == "2" && i + 4 < codes.Length) // truecolor
                                {
                                    if (int.TryParse(codes[i + 2], out int r) &&
                                        int.TryParse(codes[i + 3], out int g) &&
                                        int.TryParse(codes[i + 4], out int b))
                                    {
                                        bg = $"#{r:X2}{g:X2}{b:X2}";
                                        i += 4;
                                    }
                                }
                            }
                            break;
                        // Save/restore style stack (not standard, but for nested SGR)
                        case 1000: // push
                            styleStack.Push((fg, bg, bold, underline));
                            break;
                        case 1001: // pop
                            if (styleStack.Count > 0)
                            {
                                (fg, bg, bold, underline) = styleStack.Pop();
                            }
                            break;
                    }
                    i++;
                }

                lastIndex = match.Index + match.Length;
            }

            if (lastIndex < ansi.Length)
            {
                string text = ansi.Substring(lastIndex);
                WriteString(text, fg, bg, bold, underline);
            }
        }

        // Map ANSI color index to hex (8/16 color)
        private static string AnsiColorToHex(int idx, bool bright)
        {
            string[] colors = bright
                ? new[] { "#5555ff", "#55ff55", "#ffff55", "#ff5555", "#ff55ff", "#55ffff", "#ffffff", "#aaaaaa" }
                : new[] { "#000000", "#aa0000", "#00aa00", "#aa5500", "#0000aa", "#aa00aa", "#00aaaa", "#aaaaaa" };
            if (idx >= 0 && idx < colors.Length)
                return colors[idx];
            return "#fff";
        }

        // Map ANSI 256-color index to hex
        private static string Ansi256ToHex(int idx)
        {
            if (idx < 16)
            {
                // Standard colors
                return AnsiColorToHex(idx % 8, idx >= 8);
            }
            else if (idx >= 16 && idx < 232)
            {
                // 6x6x6 color cube
                int c = idx - 16;
                int r = (c / 36) % 6;
                int g = (c / 6) % 6;
                int b = c % 6;
                r = r == 0 ? 0 : 55 + r * 40;
                g = g == 0 ? 0 : 55 + g * 40;
                b = b == 0 ? 0 : 55 + b * 40;
                return $"#{r:X2}{g:X2}{b:X2}";
            }
            else if (idx >= 232 && idx < 256)
            {
                // Grayscale
                int level = 8 + (idx - 232) * 10;
                return $"#{level:X2}{level:X2}{level:X2}";
            }
            return "#fff";
        }
        // Get the display width of a Unicode character (1 for most, 2 for wide/emoji/box drawing)
        private static int GetCharWidth(string s)
        {
            if (string.IsNullOrEmpty(s))
                return 1;
            var codePoint = char.ConvertToUtf32(s, 0);
            // Basic check for wide characters (CJK, emoji, box drawing, etc.)
            if (EastAsianWidth.IsWide(codePoint) || EastAsianWidth.IsFullWidth(codePoint))
                return 2;
            return 1;
        }
    }

    // Helper for East Asian Width (basic implementation)
    public static class EastAsianWidth
    {
        public static bool IsWide(int codePoint)
        {
            // Box drawing, block, CJK, emoji, etc.
            return
                (codePoint >= 0x1100 && codePoint <= 0x115F) || // Hangul Jamo
                (codePoint >= 0x2329 && codePoint <= 0x232A) ||
                (codePoint >= 0x2E80 && codePoint <= 0xA4CF) ||
                (codePoint >= 0xAC00 && codePoint <= 0xD7A3) ||
                (codePoint >= 0xF900 && codePoint <= 0xFAFF) ||
                (codePoint >= 0xFE10 && codePoint <= 0xFE19) ||
                (codePoint >= 0xFE30 && codePoint <= 0xFE6F) ||
                (codePoint >= 0xFF00 && codePoint <= 0xFF60) ||
                (codePoint >= 0xFFE0 && codePoint <= 0xFFE6) ||
                (codePoint >= 0x1F300 && codePoint <= 0x1F64F) || // Emoji
                (codePoint >= 0x1F900 && codePoint <= 0x1F9FF) ||
                (codePoint >= 0x20000 && codePoint <= 0x3FFFD);
        }
        public static bool IsFullWidth(int codePoint)
        {
            return (codePoint >= 0xFF01 && codePoint <= 0xFF60) ||
                   (codePoint >= 0xFFE0 && codePoint <= 0xFFE6);
        }
    }
}
