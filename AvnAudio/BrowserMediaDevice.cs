namespace AvnAudio;

/// <summary>
/// Returned from JavaScript for each device it finds.
/// </summary>
public class BrowserMediaDevice
{
    public string deviceId { get; set; } = string.Empty;
    public string kind { get; set; } = string.Empty;
    public string label { get; set; } = string.Empty;
    public string groupId { get; set; } = string.Empty;
}
