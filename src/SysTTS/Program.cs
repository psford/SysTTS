using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SysTTS;
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

        // Read port from config and bind Kestrel to localhost only
        var serviceSettings = builder.Configuration.GetSection("Service").Get<ServiceSettings>()
            ?? new ServiceSettings();
        builder.WebHost.UseUrls($"http://127.0.0.1:{serviceSettings.Port}");

        var app = builder.Build();

        // Status endpoint
        app.MapGet("/api/status", (IVoiceManager voiceManager) => Results.Ok(new
        {
            running = true,
            activeVoices = voiceManager.GetAvailableVoices().Count,
            queueDepth = 0
        }));

        // Voices endpoint
        app.MapGet("/api/voices", (IVoiceManager voiceManager) =>
            Results.Ok(voiceManager.GetAvailableVoices().Select(v => new { v.Id, v.Name, v.SampleRate })));

        // Start Kestrel on background threads
        using var appCts = new CancellationTokenSource();
        var webTask = app.RunAsync(appCts.Token);

        // Wait for Kestrel to start or fail (port-in-use detection)
        var started = new TaskCompletionSource();
        var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
        lifetime.ApplicationStarted.Register(() => started.SetResult());

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
