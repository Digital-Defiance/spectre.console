using System;
using System.Threading.Tasks;

namespace Spectre.Console.BlazorWasm
{
    public class BlazorConsoleService
    {
        private string _output = string.Empty;
        public string Output => _output;

        public event Action? OutputChanged;

        // Interactive input support
        public event Func<InputRequest, Task<InputResponse>>? InputRequested;

        public class InputRequest
        {
            public string Prompt { get; set; } = "";
            public bool IsKey { get; set; }
            public bool Secret { get; set; }
            public char? Mask { get; set; }
            public string[]? Choices { get; set; }
        }

        public class InputResponse
        {
            public string? Line { get; set; }
            public ConsoleKeyInfo? Key { get; set; }
        }

        public void Clear()
        {
            _output = string.Empty;
            OutputChanged?.Invoke();
        }

        public void Write(string value)
        {
            _output += value;
            OutputChanged?.Invoke();
        }

        public void Set(string value)
        {
            _output = value;
            OutputChanged?.Invoke();
        }

        // --- Canvas support ---
        public class CanvasDataModel
        {
            public int Width { get; set; }
            public int Height { get; set; }
            public int[] Pixels { get; set; } = Array.Empty<int>(); // ARGB or RGBA
        }

        private CanvasDataModel? _canvasData;
        public CanvasDataModel? CanvasData => _canvasData;
        public event Action? CanvasChanged;

        public void SetCanvas(int width, int height, int[] pixels)
        {
            _canvasData = new CanvasDataModel
            {
                Width = width,
                Height = height,
                Pixels = pixels
            };
            CanvasChanged?.Invoke();
        }

        // Request line input from the UI, asynchronously
        public async Task<string> RequestInputAsync(string prompt, bool secret = false, char? mask = null, string[]? choices = null)
        {
            if (InputRequested != null)
            {
                var req = new InputRequest
                {
                    Prompt = prompt,
                    IsKey = false,
                    Secret = secret,
                    Mask = mask,
                    Choices = choices
                };
                var resp = await InputRequested.Invoke(req);
                return resp.Line ?? "";
            }
            else
            {
                throw new InvalidOperationException("No input handler registered.");
            }
        }

        // Request key input from the UI, asynchronously
        public async Task<ConsoleKeyInfo?> RequestKeyAsync(string prompt)
        {
            if (InputRequested != null)
            {
                var req = new InputRequest
                {
                    Prompt = prompt,
                    IsKey = true
                };
                var resp = await InputRequested.Invoke(req);
                return resp.Key;
            }
            else
            {
                throw new InvalidOperationException("No input handler registered.");
            }
        }
    }
}
