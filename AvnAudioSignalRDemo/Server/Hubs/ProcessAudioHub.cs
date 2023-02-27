using System.Diagnostics;

namespace AvnAudioSignalRDemo.Server.Hubs;

public class ProcessAudioHub : Hub
{
    private readonly AudioConverter _audioConverter;
    private string fileName = $"{Environment.CurrentDirectory}\\files\\output.pcm";

    public ProcessAudioHub(AudioConverter audioConverter)
    {
        _audioConverter = audioConverter;
    }

    public async Task ProcessAudioBuffer(string bufferString, BufferPosition position, int sampleRate, int channels)
    {
        var bufferBytes = Convert.FromBase64String(bufferString);

        // Uncomment the next three lines to process buffers one at a time on demand.
        //var buffer = _recordingManager.ConvertWebMBufferToPCM(bufferBytes, sampleRate, channels, position);
        //Debug.WriteLine($"RECEIVED {buffer.Length} bytes");
        //return;

        // This code will use the AudioConverter's background processing to 
        // convert buffers to PCM and write them to a specified PCM file.

        if (position == BufferPosition.First)
        {
            // You need to do this to initialize the queue
            _audioConverter.ClearQueue();
        }

        // Add this buffer to the input queue
        _audioConverter.AddBuffer(bufferBytes);

        if (position == BufferPosition.First)
        {
            // Start processing if this is the first buffer
            _audioConverter.StartProcessing(fileName, sampleRate, channels);
        }

        if (position == BufferPosition.Last)
        {
            await _audioConverter.StopProcessing();
        }
    }

    public async Task ProcessAudioFileBuffer(string fileName, string buffer, BufferPosition position, int sampleRate)
    {
        await Task.Delay(0);

        // buffer is a Base64 string
        var data = Convert.FromBase64String(buffer);

        var localFileName = $"{Environment.CurrentDirectory}\\Files\\{fileName}";

        // Delete file if it exists and this the first buffer
        if (position == BufferPosition.First)
        {
            if (File.Exists(localFileName))
            {
                File.Delete(localFileName);
            }
        }

        // Open the file
        using (var stream = File.OpenWrite(localFileName))
        {
            // seek to the end
            stream.Seek(stream.Length, SeekOrigin.Begin);
            // write the data
            stream.Write(data, 0, data.Length);
        }

        // is this the last buffer?
        if (position == BufferPosition.Last)
        {
            // get the local filename with a wav extension
            var wavExt = Path.GetFileNameWithoutExtension(fileName) + ".wav";
            var wavFileName = $"{Environment.CurrentDirectory}\\Files\\{wavExt}";

            // process the file
            _audioConverter.ConvertFile(localFileName, wavFileName, sampleRate);
        }
    }
}
