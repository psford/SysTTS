using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SysTTS;
using SysTTS.Handlers;
using SysTTS.Models;
using SysTTS.Services;
using SysTTS.Settings;
using System.Windows.Forms;

static class Program
{
    [STAThread]
    static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Bind settings
        builder.Services.Configure<ServiceSettings>(builder.Configuration.GetSection("Service"));
        builder.Services.Configure<AudioSettings>(builder.Configuration.GetSection("Audio"));

        // Register TTS services as singletons
        builder.Services.AddSingleton<IVoiceManager, VoiceManager>();
        builder.Services.AddSingleton<ITtsEngine, TtsEngine>();
        builder.Services.AddSingleton<IAudioPlayer, AudioPlayer>();
        builder.Services.AddSingleton<ISpeechQueue, SpeechQueue>();
        builder.Services.AddSingleton<ISpeechService, SpeechService>();

        // Register Clipboard and Hotkey services
        builder.Services.AddSingleton<IClipboardService, ClipboardService>();
        builder.Services.AddSingleton<UserPreferences>();

        // Capture the STA UI thread SynchronizationContext before Application.Run
        // This context will be available to HotkeyService for marshaling UI operations
        var syncContext = SynchronizationContext.Current;
        builder.Services.AddSingleton<SynchronizationContext>(syncContext ?? throw new InvalidOperationException("SynchronizationContext.Current is null"));
        builder.Services.AddSingleton<HotkeyService>();

        // Read port from config and bind Kestrel to localhost only
        var serviceSettings = builder.Configuration.GetSection("Service").Get<ServiceSettings>()
            ?? new ServiceSettings();
        builder.WebHost.UseUrls($"http://127.0.0.1:{serviceSettings.Port}");

        var app = builder.Build();

        // Status endpoint
        app.MapGet("/api/status", (IVoiceManager voiceManager, ISpeechQueue queue) => Results.Ok(new
        {
            running = true,
            activeVoices = voiceManager.GetAvailableVoices().Count,
            queueDepth = queue.QueueDepth
        }));

        // Voices endpoint
        app.MapGet("/api/voices", (IVoiceManager voiceManager) =>
            Results.Ok(voiceManager.GetAvailableVoices().Select(v => new { v.Id, v.Name, v.SampleRate })));

        // Speak selection endpoint
        app.MapPost("/api/speak-selection", SpeakSelectionHandler.Handle);

        // Speak endpoint
        app.MapPost("/api/speak", (SpeakRequestDto request, ISpeechService speechService) =>
        {
            if (string.IsNullOrWhiteSpace(request.Text))
                return Results.BadRequest(new { error = "text is required" });

            var (queued, id) = speechService.ProcessSpeakRequest(request.Text, request.Source, request.Voice);
            return Results.Accepted(value: new { queued, id });
        });

        // Stop endpoint
        app.MapPost("/api/stop", async (ISpeechQueue queue) =>
        {
            await queue.StopAndClear();
            return Results.Ok(new { stopped = true });
        });

        // Start Kestrel on background threads
        using var appCts = new CancellationTokenSource();
        var webTask = app.RunAsync(appCts.Token);

        // Wait for Kestrel to start or fail (port-in-use detection)
        var started = new TaskCompletionSource();
        var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
        lifetime.ApplicationStarted.Register(() =>
        {
            // Start HotkeyService after Kestrel confirms running
            var hotkeyService = app.Services.GetRequiredService<HotkeyService>();
            hotkeyService.Start();
            started.SetResult();
        });

        var completedTask = await Task.WhenAny(started.Task, webTask);
        if (completedTask == webTask)
        {
            // Kestrel failed before starting (e.g., port in use)
            // webTask is faulted, so get the exception
            var ex = webTask.Exception?.InnerException ?? webTask.Exception;
            MessageBox.Show(
                $"Failed to start HTTP server on port {serviceSettings.Port}:\n\n{ex?.Message}",
                "SysTTS Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            await app.DisposeAsync();
            return;
        }

        // Run WinForms on main STA thread
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new TrayApplicationContext(appCts));

        // Stop HotkeyService before shutting down
        try
        {
            var hotkeyService = app.Services.GetRequiredService<HotkeyService>();
            hotkeyService.Stop();
        }
        catch (Exception ex)
        {
            var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger("SysTTS.Program");
            logger.LogDebug(ex, "Error stopping HotkeyService during shutdown");
        }

        // Wait for Kestrel to stop after WinForms exits
        try
        {
            await webTask;
        }
        catch (OperationCanceledException)
        {
            // Expected when CancellationTokenSource is cancelled on quit
        }
        finally
        {
            await app.DisposeAsync();
        }
    }
}
