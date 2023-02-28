using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System.Text.Json;
using System.Text;

namespace AvnAudio;
public partial class AudioRecorder : ComponentBase
{
    /// <summary>
    /// Required to access JavaScript functions
    /// </summary>
    [Inject]
    public AvnAudioInterop avnAudioInterop { get; set; }

    /// <summary>
    /// Changes as recording progreses.
    /// </summary>
    [Parameter]
    public EventCallback<string> AudioStatusChanged { get; set; }

    /// <summary>
    /// Occurs when recording starts.
    /// </summary>
    [Parameter]
    public EventCallback RecordingStarted { get; set; }

    /// <summary>
    /// Occurs when recording stopped.
    /// </summary>
    [Parameter]
    public EventCallback RecordingStopped { get; set; }

    /// <summary>
    /// A list of devices available for recording. Pass in a local list.
    /// </summary>
    [Parameter]
    public List<BrowserMediaDevice> InputDevices { get; set; } 
        = new List<BrowserMediaDevice>();

    /// <summary>
    /// A list of devices available for playing. Pass in a local list.
    /// </summary>
    [Parameter]
    public List<BrowserMediaDevice> OutputDevices { get; set; } 
        = new List<BrowserMediaDevice>();

    /// <summary>
    /// Occurs when a buffer has been recorded.
    /// </summary>
    [Parameter]
    public EventCallback<AudioBuffer> BufferRecorded { get; set; }

    /// <summary>
    /// Number of samples per second. Bits per sample is hard-coded to 16. 
    /// Default is 16000.
    /// </summary>
    [Parameter]
    public int SampleRate { get; set; } = 16000;

    /// <summary>
    /// Channels: 1 for mono, 2 for stereo. Default is 1 (mono).
    /// </summary>
    [Parameter]
    public int Channels { get; set; } = 1;

    /// <summary>
    /// Number of milliseconds after which the next audio buffer is processed. Default is 500.
    /// </summary>
    [Parameter]
    public int TimeSlice { get; set; } = 500;

    // Properties

    /// <summary>
    /// Set to true when recording, and false when not.
    /// </summary>
    public bool Recording { get; set; } = false;

    // Privates

    /// <summary>
    /// A reference to this component.
    /// It's passed to JavaScript so we can be called back.
    /// </summary>
    private DotNetObjectReference<AudioRecorder> myObjectReference;

    /// <summary>
    /// Start the recording process passing in a deviceId.
    /// Call EnumerateDevices to get a list of devices and their deviceIds.
    /// </summary>
    /// <param name="deviceId"></param>
    /// <returns></returns>
    public async Task StartRecording(string deviceId)
    {
        // Call JavaScript via the AvnAudioInterop class
        await avnAudioInterop.StartRecording(myObjectReference, deviceId, SampleRate, Channels, TimeSlice);

        Recording = true;
    }

    /// <summary>
    /// Stops the recording process, after which the RecordingStopped event will occur.
    /// </summary>
    /// <returns></returns>
    public async Task StopRecording()
    {
        // Call JavaScript via the AvnAudioInterop class
        await avnAudioInterop.StopRecording();
        Recording = false;
    }

    /// <summary>
    /// Called by JavaScript when the status has changed.
    /// </summary>
    /// <returns></returns>
    [JSInvokable]
    public async Task StatusChanged(string status)
    {
        // Notify the consumer
        await AudioStatusChanged.InvokeAsync(status);
    }

    /// <summary>
    /// Called by JavaScript when recording has started
    /// </summary>
    /// <returns></returns>
    [JSInvokable]
    public async Task RecordingStartedCallback()
    {
        // Notify the consumer
        await RecordingStarted.InvokeAsync();
    }

    /// <summary>
    /// Called by JavaScript when recording has stopped
    /// </summary>
    /// <returns></returns>
    [JSInvokable]
    public async Task RecordingStoppedCallback()
    {
        // Notify the consumer
        await RecordingStopped.InvokeAsync();
    }

    /// <summary>
    /// Called by JavaScript when a new audio buffer is ready
    /// </summary>
    /// <param name="Base64EncodedByteArray">A Base64 encoded audio buffer</param>
    /// <returns></returns>
    [JSInvokable]
    public async Task DataAvailable(string Base64EncodedByteArray)
    {
        // Data is recorded as WebM, a web standard based on Opus.
        // Output data sample rate is 44800 even though the sample rate is
        // specified otherwise.

        // You can use FFMPEG to convert a webm file to a CD-quality wav file:
        //      ffmpeg -i "recorded.webm" -ar 44100 -vn "recorded.wav"

        // Convert to byte array
        var data = Convert.FromBase64String(Base64EncodedByteArray);

        // Create an audio buffer to pass to the consumer.
        var audiobuffer = new AudioBuffer()
        {
            BufferString = Base64EncodedByteArray,
            Data = data
        };

        // Notify the consumer
        await BufferRecorded.InvokeAsync(audiobuffer);
    }

    /// <summary>
    /// Called from JavaScript code when it discovers available audio devices
    /// for both input and output
    /// </summary>
    /// <param name="devices"></param>
    /// <returns></returns>
    [JSInvokable]
    public async Task AvailableAudioDevices(object[] devices)
    {
        // Prepare local lists.
        var inputDevices = new List<BrowserMediaDevice>();
        var outputDevices = new List<BrowserMediaDevice>();

        // Loop through the devices
        foreach (var device in devices)
        {
            // Get the JSON for this device
            string deviceString = device.ToString();
            
            // Create a BrowserMediaDevice object from it.
            var dev = JsonSerializer.Deserialize<BrowserMediaDevice>(deviceString);
            
            if (dev.kind == "audioinput")
            {
                // This is an input device
                if (dev.label.Trim() != "" && dev.deviceId.Trim() != "")
                {
                    // Sometimes we get blank labels and ids.
                    inputDevices.Add(dev);
                }
            }
            else if (dev.kind == "audiooutput")
            {
                // This is an output device
                if (dev.label.Trim() != "" && dev.deviceId.Trim() != "")
                {
                    // Sometimes we get blank labels and ids.
                    outputDevices.Add(dev);
                }
            }
        }

        // Clear the lists that the consumer has passed in as parameters
        InputDevices.Clear();
        OutputDevices.Clear();

        // Add the local BrowserMediaDevice objects
        if (inputDevices.Count > 0)
        {
            InputDevices.AddRange(inputDevices.OrderBy(o => o.label).ToList());
        }
        if (outputDevices.Count > 0)
        {
            OutputDevices.AddRange(outputDevices.OrderBy(o => o.label).ToList());
        }
        
        // Notify the consumer
        await AudioStatusChanged.InvokeAsync("Audio Devices Found");

    }

    /// <summary>
    /// Enumerates the audio input and output devices, after which the 
    /// InputDevices and OutputDevices lists will populate.
    /// </summary>
    /// <returns></returns>
    public async Task EnumerateDevices()
    {
        // Call JavaScript via the AvnAudioInterop class
        await avnAudioInterop.EnumerateAudioDevices(myObjectReference);
    }

    /// <summary>
    /// Writes a Wave file header to an OPEN file stream. 
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="isFloatingPoint"></param>
    /// <param name="channelCount"></param>
    /// <param name="bitDepth"></param>
    /// <param name="sampleRate"></param>
    /// <param name="totalSampleCount"></param>
    public void WriteWavHeader(Stream stream, bool isFloatingPoint, 
        ushort channelCount, ushort bitDepth, int sampleRate, 
        int totalSampleCount)
    {
        stream.Position = 0;

        // RIFF header.
        // Chunk ID.
        stream.Write(Encoding.ASCII.GetBytes("RIFF"), 0, 4);

        // Chunk size.
        stream.Write(BitConverter.GetBytes(((bitDepth / 8) * totalSampleCount) + 36), 0, 4);

        // Format.
        stream.Write(Encoding.ASCII.GetBytes("WAVE"), 0, 4);

        // Sub-chunk 1.
        // Sub-chunk 1 ID.
        stream.Write(Encoding.ASCII.GetBytes("fmt "), 0, 4);

        // Sub-chunk 1 size.
        stream.Write(BitConverter.GetBytes(16), 0, 4);

        // Audio format (floating point (3) or PCM (1)). Any other format indicates compression.
        stream.Write(BitConverter.GetBytes((ushort)(isFloatingPoint ? 3 : 1)), 0, 2);

        // Channels.
        stream.Write(BitConverter.GetBytes(channelCount), 0, 2);

        // Sample rate.
        stream.Write(BitConverter.GetBytes(sampleRate), 0, 4);

        // Bytes rate.
        stream.Write(BitConverter.GetBytes(sampleRate * channelCount * (bitDepth / 8)), 0, 4);

        // Block align.
        stream.Write(BitConverter.GetBytes((ushort)channelCount * (bitDepth / 8)), 0, 2);

        // Bits per sample.
        stream.Write(BitConverter.GetBytes(bitDepth), 0, 2);

        // Sub-chunk 2.
        // Sub-chunk 2 ID.
        stream.Write(Encoding.ASCII.GetBytes("data"), 0, 4);

        // Sub-chunk 2 size.
        stream.Write(BitConverter.GetBytes((bitDepth / 8) * totalSampleCount), 0, 4);
    }

    protected override void OnInitialized()
    {
        // Grab a reference to our component
        myObjectReference = DotNetObjectReference.Create(this);
    }
}
