# SysTTS Implementation Plan - Phase 1

**Goal:** Working WinForms application with DI container, system tray icon, and embedded Kestrel HTTP server responding on localhost.

**Architecture:** Single-process WinForms application using `WebApplication.CreateBuilder` for DI + embedded Kestrel. Kestrel runs on background threads via `RunAsync()`, WinForms message pump runs on the main STA thread via `Application.Run()`. Built-in `System.Windows.Forms.NotifyIcon` for tray presence.

**Tech Stack:** .NET 8, WinForms, ASP.NET Core (Kestrel), `Microsoft.NET.Sdk` with `FrameworkReference` to `Microsoft.AspNetCore.App`

**Scope:** 7 phases from original design (phase 1 of 7)

**Codebase verified:** 2026-02-15

---

## Acceptance Criteria Coverage

This phase implements:

### sys-tts.AC1: Service runs as Windows application with system tray presence
- **sys-tts.AC1.1 Success:** Application starts, tray icon appears in system tray, and `GET /api/status` returns 200 with `{ running: true }`
- **sys-tts.AC1.2 Success:** Right-clicking tray icon shows context menu with status info and quit option
- **sys-tts.AC1.3 Success:** Selecting "Quit" from tray menu shuts down Kestrel, unhooks hotkeys, and exits cleanly (no orphan processes)
- **sys-tts.AC1.4 Failure:** If configured port is already in use, application logs error and exits with descriptive message (does not crash silently)

**Verifies: None** — This is an infrastructure/scaffolding phase. All ACs above are verified operationally.

**Note:** The design plan lists NAudio as a Phase 1 dependency, but NAudio is only needed for audio playback (Phase 2). Phase 1 is strictly project scaffolding and service host — no audio functionality. NAudio is introduced in Phase 2 where it is first used.

---

<!-- START_TASK_1 -->
### Task 1: Create solution and project files

**Files:**
- Create: `SysTTS.sln`
- Create: `src/SysTTS/SysTTS.csproj`

**Step 1: Create directory structure**

```bash
mkdir -p src/SysTTS
```

**Step 2: Create the .csproj**

Uses `Microsoft.NET.Sdk` (not `Microsoft.NET.Sdk.Web`) with a FrameworkReference to ASP.NET Core. This keeps WinForms as the primary SDK while providing full Kestrel and DI support:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWindowsForms>true</UseWindowsForms>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <RootNamespace>SysTTS</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>
</Project>
```

**Step 3: Create the solution**

```bash
dotnet new sln --name SysTTS
dotnet sln add src/SysTTS/SysTTS.csproj
```

**Step 4: Verify**

Run: `dotnet restore SysTTS.sln`
Expected: Restores without errors.

**Step 5: Commit**

```bash
git add SysTTS.sln src/SysTTS/SysTTS.csproj
git commit -m "chore: create solution and project structure"
```
<!-- END_TASK_1 -->

<!-- START_TASK_2 -->
### Task 2: Create settings classes and appsettings.json

**Files:**
- Create: `src/SysTTS/Settings/ServiceSettings.cs`
- Create: `src/SysTTS/Settings/AudioSettings.cs`
- Create: `src/SysTTS/appsettings.json`

**Step 1: Create ServiceSettings.cs**

```csharp
namespace SysTTS.Settings;

public class ServiceSettings
{
    public int Port { get; set; } = 5100;
    public string VoicesPath { get; set; } = "voices";
    public string DefaultVoice { get; set; } = "en_US-amy-medium";
    public int MaxQueueDepth { get; set; } = 10;
    public bool InterruptOnHigherPriority { get; set; } = true;
}
```

**Step 2: Create AudioSettings.cs**

```csharp
namespace SysTTS.Settings;

public class AudioSettings
{
    public string? OutputDevice { get; set; }
    public float Volume { get; set; } = 1.0f;
}
```

**Step 3: Create appsettings.json**

```json
{
  "Service": {
    "Port": 5100,
    "VoicesPath": "voices",
    "DefaultVoice": "en_US-amy-medium",
    "MaxQueueDepth": 10,
    "InterruptOnHigherPriority": true
  },
  "Sources": {
    "t-tracker": {
      "voice": "custom-bear",
      "filters": ["approaching", "arrived"],
      "priority": 1
    },
    "default": {
      "voice": "en_US-amy-medium",
      "filters": null,
      "priority": 3
    }
  },
  "Hotkeys": [],
  "Audio": {
    "OutputDevice": null,
    "Volume": 1.0
  }
}
```

**Step 4: Verify**

Run: `dotnet build src/SysTTS/SysTTS.csproj`
Expected: Build succeeds.

**Step 5: Commit**

```bash
git add src/SysTTS/Settings/ src/SysTTS/appsettings.json
git commit -m "feat: add settings classes and configuration"
```
<!-- END_TASK_2 -->

<!-- START_TASK_3 -->
### Task 3: Create TrayApplicationContext

**Files:**
- Create: `src/SysTTS/TrayApplicationContext.cs`

**Step 1: Create the file**

`TrayApplicationContext` is an `ApplicationContext` subclass that manages the system tray icon lifecycle. It receives a `CancellationTokenSource` to signal Kestrel shutdown when the user quits.

```csharp
using System.Drawing;
using System.Windows.Forms;

namespace SysTTS;

public class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon;
    private readonly CancellationTokenSource _appCts;

    public TrayApplicationContext(CancellationTokenSource appCts)
    {
        _appCts = appCts;

        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add("SysTTS - Running", null, null!).Enabled = false;
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add("Quit", null, OnQuit);

        _notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "SysTTS",
            Visible = true,
            ContextMenuStrip = contextMenu
        };
    }

    private void OnQuit(object? sender, EventArgs e)
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _appCts.Cancel();
        ExitThread();
    }
}
```

**Step 2: Verify**

Run: `dotnet build src/SysTTS/SysTTS.csproj`
Expected: Build succeeds.

**Step 3: Commit**

```bash
git add src/SysTTS/TrayApplicationContext.cs
git commit -m "feat: add TrayApplicationContext with system tray icon"
```
<!-- END_TASK_3 -->

<!-- START_TASK_4 -->
### Task 4: Create Program.cs with WebApplication + WinForms integration

**Files:**
- Create: `src/SysTTS/Program.cs`

**Step 1: Create the file**

This is the main integration point. `WebApplication.CreateBuilder` provides DI + Kestrel. Kestrel runs on background threads via `RunAsync()`, WinForms message pump runs on the main STA thread. Port-in-use detection uses `IHostApplicationLifetime.ApplicationStarted` racing against `RunAsync` faulting.

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SysTTS;
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

        // Read port from config and bind Kestrel to localhost only
        var serviceSettings = builder.Configuration.GetSection("Service").Get<ServiceSettings>()
            ?? new ServiceSettings();
        builder.WebHost.UseUrls($"http://127.0.0.1:{serviceSettings.Port}");

        var app = builder.Build();

        // Status endpoint
        app.MapGet("/api/status", () => Results.Ok(new
        {
            running = true,
            activeVoices = 0,
            queueDepth = 0
        }));

        // Start Kestrel on background threads
        var appCts = new CancellationTokenSource();
        var webTask = app.RunAsync(appCts.Token);

        // Wait for Kestrel to start or fail (port-in-use detection)
        var started = new TaskCompletionSource();
        var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
        lifetime.ApplicationStarted.Register(() => started.SetResult());

        var completedTask = await Task.WhenAny(started.Task, webTask);
        if (completedTask == webTask)
        {
            // Kestrel failed before starting (e.g., port in use)
            var ex = webTask.Exception?.InnerException;
            MessageBox.Show(
                $"Failed to start HTTP server on port {serviceSettings.Port}:\n\n{ex?.Message}",
                "SysTTS Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
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
    }
}
```

**Step 2: Verify**

Run: `dotnet build src/SysTTS/SysTTS.csproj`
Expected: Build succeeds.

**Step 3: Commit**

```bash
git add src/SysTTS/Program.cs
git commit -m "feat: add Program.cs with WinForms + Kestrel integration"
```
<!-- END_TASK_4 -->

<!-- START_TASK_5 -->
### Task 5: Operational verification

**Step 1: Start the application**

Run: `dotnet run --project src/SysTTS/SysTTS.csproj`
Verify: Tray icon appears in system tray. (sys-tts.AC1.1)

**Step 2: Test status endpoint**

Run: `curl http://localhost:5100/api/status`
Expected: `{"running":true,"activeVoices":0,"queueDepth":0}` (sys-tts.AC1.1)

**Step 3: Test tray context menu**

Right-click tray icon. Verify: Context menu shows "SysTTS - Running" (disabled) and "Quit". (sys-tts.AC1.2)

**Step 4: Test clean shutdown**

Click "Quit" from tray menu. Verify: Application exits cleanly, no orphan processes. (sys-tts.AC1.3)

**Step 5: Test port-in-use handling**

Start a second instance while the first is running:
Run: `dotnet run --project src/SysTTS/SysTTS.csproj`
Expected: Error dialog appears with "Failed to start HTTP server on port 5100" message. (sys-tts.AC1.4)

**Step 6: Verify all changes committed**

Run: `git status`
Expected: Working tree clean — all phase changes committed in prior tasks.
<!-- END_TASK_5 -->
