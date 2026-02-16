using Microsoft.AspNetCore.Http;
using SysTTS.Models;
using SysTTS.Services;

namespace SysTTS.Handlers;

/// <summary>
/// Handler for POST /api/speak-selection endpoint.
/// Extracts selected text via clipboard and queues speech.
/// This handler is testable in isolation from the ASP.NET pipeline.
/// </summary>
public static class SpeakSelectionHandler
{
    /// <summary>
    /// Handle the speak-selection request.
    ///
    /// Verifies AC3.8: Captures selected text server-side and queues speech with voice override.
    /// </summary>
    public static async Task<IResult> Handle(
        SpeakSelectionRequestDto request,
        IClipboardService clipboard,
        ISpeechService speechService)
    {
        var text = await clipboard.CaptureSelectedTextAsync();
        if (string.IsNullOrWhiteSpace(text))
            return Results.Ok(new { queued = false, text = "" });

        var (queued, id) = speechService.ProcessSpeakRequest(text, "speak-selection", request.Voice);
        return Results.Accepted(value: new { queued, id, text });
    }
}
