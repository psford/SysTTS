namespace SysTTS.Settings;

public class ServiceSettings
{
    public int Port { get; set; } = 5100;
    public string VoicesPath { get; set; } = "voices";
    public string DefaultVoice { get; set; } = "en_US-amy-medium";
    public string EspeakDataPath { get; set; } = "espeak-ng-data";
    public int MaxQueueDepth { get; set; } = 10;
    public bool InterruptOnHigherPriority { get; set; } = true;
}
