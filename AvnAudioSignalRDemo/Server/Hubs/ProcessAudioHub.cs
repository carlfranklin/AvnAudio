using System.Diagnostics;

namespace AvnAudioSignalRDemo.Server.Hubs;

// Hub for handling WebM audio buffers from the Blazor client app
public class ProcessAudioHub : Hub
{
    private readonly AudioConverter _audioConverter;
    
    // You can change this to whatever file name you want to save to
    private string fileName = $"{Environment.CurrentDirectory}\\files\\output.pcm";

    /// <summary>
    /// The AudioConverter is injected by the DI system (see Program.cs)
    /// </summary>
    /// <param name="audioConverter"></param>
    public ProcessAudioHub(AudioConverter audioConverter)
    {
        _audioConverter = audioConverter;
    }

    /// <summary>
    /// Called by Blazor client app to process a WebM audio buffer
    /// </summary>
    /// <param name="bufferString">The WebM audio buffer</param>
    /// <param name="position">First, Last, or Middle</param>
    /// <param name="sampleRate">Sample rate for converting</param>
    /// <param name="channels">Channel count for converting</param>
    /// <returns></returns>
    public async Task ProcessAudioBuffer(string bufferString, 
        BufferPosition position, int sampleRate, int channels)
    {
        // Convert buffer to a byte array
        var data = Convert.FromBase64String(bufferString);

        // Uncomment the next three lines to process buffers one at a time on demand.
        //var buffer = _audioConverter.ConvertWebMBufferToPCM(data, sampleRate, channels, position);
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
        _audioConverter.AddBuffer(data);

        if (position == BufferPosition.First)
        {
            // Start processing if this is the first buffer
            _audioConverter.StartProcessing(fileName, sampleRate, channels);
        }

        // Stop processing if this is the last buffer
        if (position == BufferPosition.Last)
        {
            await _audioConverter.StopProcessing();
        }
    }

    /// <summary>
    ///  Called by Blazor client app to write a WebM buffer
    ///  directly to a .webm file without conversion.
    /// </summary>
    /// <param name="fileName">Name of file to save to</param>
    /// <param name="buffer">WebM audio buffer</param>
    /// <param name="position">First, Last, or Middle</param>
    /// <param name="sampleRate">Sample rate for converting</param>
    /// <returns></returns>
    public async Task ProcessAudioFileBuffer(string fileName, string buffer, 
        BufferPosition position, int sampleRate)
    {
        await Task.Delay(0);

        // Convert buffer to a byte array
        var data = Convert.FromBase64String(buffer);

        // Formulate a local file name
        var localFileName = $"{Environment.CurrentDirectory}\\Files\\{fileName}";

        // Delete file if it exists and this the first buffer
        if (position == BufferPosition.First)
        {
            if (File.Exists(localFileName))
                File.Delete(localFileName);
        }

        int NumberOfRetries = 4;    // arbitrary

        for (int i = 1; i <= NumberOfRetries; ++i)
        {
            try
            {
                // Open the file
                using (var stream = File.OpenWrite(localFileName))
                {
                    // seek to the end
                    stream.Position = stream.Length;
                    // write the data
                    stream.Write(data, 0, data.Length);
                    stream.Flush();
                }
                // Success. 
                break;
            }
            catch (IOException e) when (i <= NumberOfRetries)
            {
                Thread.Sleep(100);
            }
        }


        // is this the last buffer?
        if (position == BufferPosition.Last)
        {
            // get the local filename with a wav extension
            var wavExt = Path.GetFileNameWithoutExtension(fileName) + ".wav";
            var wavFileName = $"{Environment.CurrentDirectory}\\Files\\{wavExt}";

            // Convert the .webm file to a .wav file
            _audioConverter.ConvertFile(localFileName, wavFileName, sampleRate);
        }
    }
}
