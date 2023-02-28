using Microsoft.JSInterop;

namespace AvnAudio;

/// <summary>
/// This class provides access to the JavaScript functions in avnAudio.js.
/// With this class, the consumer of AvnAudio doesn't need to specify a path
/// to the .js file.
/// The AvnAudio component uses this to access the JavaScript functions.
/// </summary>
public class AvnAudioInterop : IAsyncDisposable
{
    /// <summary>
    /// Required to create a module to access JavaScript
    /// </summary>
    private readonly Lazy<Task<IJSObjectReference>> moduleTask;

    /// <summary>
    /// This class requires a JavaScript runtime, which is injected.
    /// </summary>
    /// <param name="jsRuntime"></param>
    public AvnAudioInterop(IJSRuntime jsRuntime)
    {
        // Load the .js file into a Task to get ready for access
        moduleTask = new(() => jsRuntime.InvokeAsync<IJSObjectReference>(
            "import", "./_content/AvnAudio/avnAudio.js").AsTask());
    }

    /// <summary>
    /// Kicks off the process of enumerating the audio devices
    /// </summary>
    /// <param name="caller">The Blazor Component for JavaScript to call back to</param>
    /// <returns></returns>
    public async ValueTask EnumerateAudioDevices(object caller)
    {
        var module = await moduleTask.Value;
        await module.InvokeVoidAsync("enumerateAudioDevices", caller);
    }

    /// <summary>
    /// Kicks off the recording process
    /// </summary>
    /// <param name="caller">The Blazor Component for JavaScript to call back to</param>
    /// <param name="deviceId">Id of the device to record with</param>
    /// <param name="sampleRate">Sample rate. Ex: 44100</param>
    /// <param name="channels">Number of channels. 1=mono. 2=stereo</param>
    /// <param name="timeSlice">Number of milliseconds between each buffer</param>
    /// <returns></returns>
    public async ValueTask StartRecording(object caller, string deviceId, 
        int sampleRate, int channels, int timeSlice)
    {
        var module = await moduleTask.Value;
        await module.InvokeVoidAsync("startRecording", caller, deviceId, 
            sampleRate, channels, timeSlice);
    }

    /// <summary>
    /// Stops the recording process
    /// </summary>
    /// <returns></returns>
    public async ValueTask StopRecording()
    {
        var module = await moduleTask.Value;
        await module.InvokeVoidAsync("stopRecording");
    }

    /// <summary>
    /// Required to release the module resource
    /// </summary>
    /// <returns></returns>
    public async ValueTask DisposeAsync()
    {
        if (moduleTask.IsValueCreated)
        {
            var module = await moduleTask.Value;
            await module.DisposeAsync();
        }
    }
}