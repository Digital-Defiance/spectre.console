using System;
using System.Threading;
using System.Threading.Tasks;
using Spectre.Console;

namespace Spectre.Console.BlazorWasm
{
    // Implements IAnsiConsoleInput for Blazor WASM, routing input requests to BlazorConsoleService
    public sealed class BlazorAnsiConsoleInput : IAnsiConsoleInput
    {
        private readonly BlazorConsoleService _consoleService;

        public BlazorAnsiConsoleInput(BlazorConsoleService consoleService)
        {
            _consoleService = consoleService;
        }

        public string? ReadLine(Style? style = null, bool secret = false, char? mask = null, string[]? choices = null, CancellationToken cancellationToken = default)
        {
            // Synchronous input is not supported in WASM; use async version
            throw new NotSupportedException("Synchronous input is not supported in Blazor WASM. Use ReadLineAsync.");
        }

        public async Task<string?> ReadLineAsync(Style? style = null, bool secret = false, char? mask = null, string[]? choices = null, CancellationToken cancellationToken = default)
        {
            // Prompt the user for input via the Blazor UI, passing all parameters
            return await _consoleService.RequestInputAsync("Input:", secret, mask, choices);
        }

        public ConsoleKeyInfo? ReadKey(bool intercept = false)
        {
            // Synchronous input is not supported in WASM; use async version
            throw new NotSupportedException("Synchronous input is not supported in Blazor WASM. Use ReadKeyAsync.");
        }

        public async Task<ConsoleKeyInfo?> ReadKeyAsync(bool intercept = false, CancellationToken cancellationToken = default)
        {
            // Request a key from the UI
            return await _consoleService.RequestKeyAsync("Input key:");
        }

        public bool IsKeyAvailable()
        {
            // No input buffer in WASM; always return false
            return false;
        }
    }
}
