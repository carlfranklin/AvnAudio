namespace AvnAudio;
/// <summary>
/// Passed to the consumer when an audio buffer is ready
/// </summary>
public class AudioBuffer
{
    public string BufferString { get; set; }
    public byte[] Data { get; set; }
}
