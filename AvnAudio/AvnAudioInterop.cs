using Microsoft.JSInterop;
using System.Threading.Channels;

namespace AvnAudio
{
    public class AvnAudioInterop : IAsyncDisposable
    {
        private readonly Lazy<Task<IJSObjectReference>> moduleTask;

        public AvnAudioInterop(IJSRuntime jsRuntime)
        {
            moduleTask = new(() => jsRuntime.InvokeAsync<IJSObjectReference>(
                "import", "./_content/AvnAudio/avnAudio.js").AsTask());
        }

        public async ValueTask EnumerateAudioDevices(object caller)
        {
            var module = await moduleTask.Value;
            await module.InvokeVoidAsync("enumerateAudioDevices", caller);
        }

        public async ValueTask StartRecording(object caller, string deviceId, int sampleRate, int channels, int timeSlice)
        {
            var module = await moduleTask.Value;
            await module.InvokeVoidAsync("startRecording", caller, deviceId, sampleRate, channels, timeSlice);
        }

        public async ValueTask StopRecording()
        {
            var module = await moduleTask.Value;
            await module.InvokeVoidAsync("stopRecording");
        }

        public async ValueTask DisposeAsync()
        {
            if (moduleTask.IsValueCreated)
            {
                var module = await moduleTask.Value;
                await module.DisposeAsync();
            }
        }
    }
}