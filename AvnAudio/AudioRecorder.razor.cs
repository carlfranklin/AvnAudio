using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System.Text.Json;
using System.Text;

namespace AvnAudio;
public partial class AudioRecorder : ComponentBase
{
    [Inject]
    public AvnAudioInterop avnAudioInterop { get; set; }

    [Inject]
    public IJSRuntime JSRuntime { get; set; }

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
    public List<BrowserMediaDevice> InputDevices { get; set; } = new List<BrowserMediaDevice>();

    /// <summary>
    /// A list of devices available for playing. Pass in a local list.
    /// </summary>
    [Parameter]
    public List<BrowserMediaDevice> OutputDevices { get; set; } = new List<BrowserMediaDevice>();

    /// <summary>
    /// Occurs when a buffer has been recorded.
    /// </summary>
    [Parameter]
    public EventCallback<AudioBuffer> BufferRecorded { get; set; }

    /// <summary>
    /// Number of samples per second. Bits per sample is hard-coded to 16. Default is 16000.
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
    DotNetObjectReference<AudioRecorder> myObjectReference;
    bool firstBuffer = true;
    
    /// <summary>
    /// Start the recording process passing in a deviceId.
    /// Call EnumerateDevices to get a list of devices and their deviceIds.
    /// </summary>
    /// <param name="deviceId"></param>
    /// <returns></returns>
    public async Task StartRecording(string deviceId)
    {
        firstBuffer = true;

        // Start Recording
        await avnAudioInterop.StartRecording(myObjectReference, deviceId, SampleRate, Channels, TimeSlice);

        Recording = true;
    }

    /// <summary>
    /// Stops the recording process, after which the RecordingStopped event will occur.
    /// </summary>
    /// <returns></returns>
    public async Task StopRecording()
    {
        await avnAudioInterop.StopRecording();
        Recording = false;
    }

    [JSInvokable]
    public async Task StatusChanged(string status)
    {
        await AudioStatusChanged.InvokeAsync(status);
    }

    [JSInvokable]
    public async Task RecordingStartedCallback()
    {
        await RecordingStarted.InvokeAsync();
    }

    [JSInvokable]
    public async Task RecordingStoppedCallback()
    {
        await RecordingStopped.InvokeAsync();
    }

    [JSInvokable]
    public async Task DataAvailable(string Base64EncodedByteArray)
    {
        // Data is recorded as WebM, a web standard based on Opus.
        // Output data sample rate is 44800 even though the sample rate is specified otherwise.
        // You can use FFMPEG to convert webm to wav like so:
        //      ffmpeg -i "recorded.webm" -vn "recorded.wav"
        // then you can convert the wav sample rate from 44800 like so:
        //      ffmpeg -i recorded.wav -ar 16000 recorded16.wav

        //if (firstBuffer)
        //{
        //    firstBuffer = false;
        //    The first 122 characters are the webm audio header
        //}

        // Convert to byte array
        var data = Convert.FromBase64String(Base64EncodedByteArray);
        var audiobuffer = new AudioBuffer();
        audiobuffer.BufferString = Base64EncodedByteArray;
        audiobuffer.Data = data;

        // Notify the caller
        await BufferRecorded.InvokeAsync(audiobuffer);
    }

    [JSInvokable]
    public async Task AvailableAudioDevices(object[] devices)
    {
        // Called by JavaScript when we get the list of devices
        var inputDevices = new List<BrowserMediaDevice>();
        var outputDevices = new List<BrowserMediaDevice>();

        foreach (var device in devices)
        {
            string deviceString = device.ToString();
            var dev = JsonSerializer.Deserialize<BrowserMediaDevice>(deviceString);
            if (dev.kind == "audioinput")
            {
                if (dev.label.Trim() != "" && dev.deviceId.Trim() != "")
                {
                    inputDevices.Add(dev);
                }
            }
            else if (dev.kind == "audiooutput")
            {
                if (dev.label.Trim() != "" && dev.deviceId.Trim() != "")
                {
                    outputDevices.Add(dev);
                }
            }
        }
        InputDevices.Clear();
        OutputDevices.Clear();

        if (inputDevices.Count > 0)
        {
            InputDevices.AddRange(inputDevices.OrderBy(o => o.label).ToList());
        }

        if (outputDevices.Count > 0)
        {
            OutputDevices.AddRange(outputDevices.OrderBy(o => o.label).ToList());
        }

        await AudioStatusChanged.InvokeAsync("Audio Devices Found");
        await InvokeAsync(StateHasChanged);

    }

    /// <summary>
    /// Enumerates the audio input and output devices, after which the InputDevices and OutputDevices lists will populate.
    /// </summary>
    /// <returns></returns>
    public async Task EnumerateDevices()
    {
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
    public void WriteWavHeader(Stream stream, bool isFloatingPoint, ushort channelCount, ushort bitDepth, int sampleRate, int totalSampleCount)
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
        myObjectReference = DotNetObjectReference.Create(this);
    }
}
