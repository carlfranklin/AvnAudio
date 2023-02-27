using System.Collections.Concurrent;
using System.Diagnostics;

namespace AvnAudioSignalRDemo.Server;

/// <summary>
/// Background process that converts WebM audio data to PCM audio data
/// and writes them to a specified .PCM file
/// </summary>
public class AudioConverter
{
    // Thread-safe queue for storing WebM audio buffers
    private ConcurrentQueue<byte[]> InputBuffers = new ConcurrentQueue<byte[]>();
    private bool Working = false;   // Whether or not the background process is running
    private bool Done = false;      // Flag set when the background process is done
    private int SampleRate;         // Sample Rate for conversion
    private int Channels;           // Channels for conversion
    private bool firstBuffer = true;  // Flag used to modify data with WebM header
    private string FileName = string.Empty; // PCM File name to write data to
    private byte[] WebHeader { get; set; }  // See AddBuffer

    /// <summary>
    /// Clears the input buffer queue.
    /// Should be called before the first buffer is added
    /// </summary>
    public void ClearQueue()
    {
        firstBuffer = true;
        InputBuffers.Clear();
    }

    /// <summary>
    /// Called by SignalR hub to add a buffer to the input queue
    /// </summary>
    /// <param name="buffer"></param>
    public void AddBuffer(byte[] buffer)
    {
        if (firstBuffer)
        {
            firstBuffer = false;
            // first buffer. Save the WebM header

            // 162 is a magic number.
            // The first 162 bytes of the first buffer
            //  are the WebM header, that must be
            //  prepended to every buffer in order
            //  for FFMPEG to convert it properly
            var mem = new MemoryStream();
            mem.Write(buffer, 0, 162);
            WebHeader = mem.ToArray();
            mem.Dispose();
        }
        else
        {
            // Add the WebM header to the buffer
            var mem = new MemoryStream();
            mem.Write(WebHeader);
            mem.Write(buffer);
            buffer = mem.ToArray();
            mem.Dispose();
        }
        // Add it to the Queue
        InputBuffers.Enqueue(buffer);
    }

    /// <summary>
    /// Alternative to using this service to convert buffers
    /// in the background and write them to a file.
    /// This does an immediate conversion and returns the buffer.
    /// </summary>
    /// <param name="buffer">WebM audio data</param>
    /// <param name="SampleRate">Sample rate for converting</param>
    /// <param name="Channels">Number of channels for converting</param>
    /// <param name="position">First, last, or middle</param>
    /// <returns></returns>
    public byte[] ConvertWebMBufferToPCM(byte[] buffer, int SampleRate, int Channels, BufferPosition position)
    {
        // Save the Sample rate and channels
        this.SampleRate = SampleRate;
        this.Channels = Channels;

        if (position == BufferPosition.First)
        {
            // first buffer. Grab the header

            // 162 is a magic number.
            // The first 162 bytes of the first buffer
            //  are the WebM header, that must be
            //  prepended to every buffer in order
            //  for FFMPEG to convert it properly
            var mem = new MemoryStream();
            mem.Write(buffer, 0, 162);
            WebHeader = mem.ToArray();
            mem.Dispose();
        }
        else
        {
            // Add the WebM header to the buffer
            var mem = new MemoryStream();
            mem.Write(WebHeader);
            mem.Write(buffer);
            buffer = mem.ToArray();
            mem.Dispose();
        }
        
        // Convert and return
        return ConvertWebmToPcm(buffer);
    }

    /// <summary>
    /// Called by SignalR hub to stop processing
    /// and writing to the PCM file
    /// </summary>
    /// <returns></returns>
    public async Task StopProcessing()
    {
        // This signals to the background thread
        // to exit the processing loop and set
        // the Done flag to true.
        Working = false;

        // Wait for the Done flag to be set
        while(!Done)
        {
            await Task.Delay(200);
        }
    }

    /// <summary>
    /// Starts the process of converting the data from the input queue
    /// and writing the PCM data to the specified file
    /// </summary>
    /// <param name="fileName">The PCM file name to write to</param>
    /// <param name="sampleRate">The Sample Rate for processing</param>
    /// <param name="channels">Number of channels for processing</param>
    public void StartProcessing(string fileName, int sampleRate, int channels)
    {
        // We should not already be working.
        if (!Working)
        {
            // Save these values
            SampleRate = sampleRate;
            Channels = channels;
            FileName = fileName;

            // Delete file if it exists
            if (File.Exists(FileName))
                File.Delete(FileName);

            // Create a new thread for the DoWork() method
            var WorkingThread = new Thread(new ThreadStart(DoWork));
            // Start it up!
            WorkingThread.Start();
        }
    }

    /// <summary>
    /// Processes the input queue on a background thread
    /// </summary>
    private void DoWork()
    {
        Working = true;     // Let em know we're working here!
        Done = false;       // Not done until it's done!

        // loop it
        while (Working)
        {
            // Do we have an input buffer?
            if (InputBuffers.Count > 0)
            {
                // Remove the next one from the queue
                if (InputBuffers.TryDequeue(out var inputBuffer))
                {
                    // Convert the buffer
                    var outputBuffer = ConvertWebmToPcm(inputBuffer);

                    // Retry loop to write buffer to file,
                    // in case of IO exceptions (can not access
                    // file because it is being used by another
                    // process.
                    int NumberOfRetries = 4;    // arbitrary

                    for (int i = 1; i <= NumberOfRetries; ++i)
                    {
                        try
                        {
                            using (var stream = File.OpenWrite(FileName))
                            {
                                stream.Position = stream.Length;
                                stream.Write(outputBuffer);
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
                }
            }
            // Wait 100ms before looking for the next buffer
            Thread.Sleep(100);
        }
        // We are done!
        Done = true;
    }

    /// <summary>
    /// Calls FFMPEG (see FFMPEG.txt) to convert the buffer
    /// using standard input and standard output
    /// </summary>
    /// <param name="webmData">WebM audio buffer</param>
    /// <returns></returns>
    private byte[] ConvertWebmToPcm(byte[] webmData)
    {
        // Set up the FFMPEG process
        var ffmpegProcess = new Process();
        ffmpegProcess.StartInfo.FileName = "ffmpeg.exe";
        ffmpegProcess.StartInfo.Arguments = $"-i - -f s16le -acodec pcm_s16le -ac {Channels} -ar {SampleRate} -";
        ffmpegProcess.StartInfo.UseShellExecute = false;
        ffmpegProcess.StartInfo.RedirectStandardInput = true;
        ffmpegProcess.StartInfo.RedirectStandardOutput = true;

        // Start the FFMPEG process
        ffmpegProcess.Start();

        // Write the WebM data to the process's input stream
        ffmpegProcess.StandardInput.BaseStream.Write(webmData, 0, webmData.Length);
        ffmpegProcess.StandardInput.BaseStream.Flush();
        ffmpegProcess.StandardInput.WriteLine("q\n");
        ffmpegProcess.StandardInput.Close();

        // Read the converted PCM data from the process's output stream
        var pcmData = new byte[10000]; // Buffer for reading data
        int bytesRead;
        using (var outputStream = ffmpegProcess.StandardOutput.BaseStream)
        using (var memoryStream = new MemoryStream())
        {
            while ((bytesRead = outputStream.Read(pcmData, 0, pcmData.Length)) > 0)
            {
                memoryStream.Write(pcmData, 0, bytesRead);
            }
            pcmData = memoryStream.ToArray();
        }

        // Wait for the FFMPEG process to exit
        ffmpegProcess.WaitForExit();

        // Clean up
        ffmpegProcess.Close();
        ffmpegProcess.Dispose();
        
        return pcmData;
    }

    /// <summary>
    /// Calls FFMPEG (see FFMPEG.txt) to convert a WebM file
    /// to another format, WAV in this case.
    /// </summary>
    /// <param name="inputFile"></param>
    /// <param name="outputFile"></param>
    /// <param name="sampleRate"></param>
    public void ConvertFile(string inputFile, string outputFile, int sampleRate)
    {
        // Create a process to convert webm to wav
        var processStartInfo = new ProcessStartInfo()
        {
            // ffmpeg arguments
            Arguments = $" -i {inputFile} -ar {sampleRate} -vn {outputFile}",
            FileName = "ffmpeg.exe",
            RedirectStandardInput = true, // Must be set to true
            UseShellExecute = false      // Must be set to false
        };
        // Execute
        Process p = Process.Start(processStartInfo);
        p.WaitForExit();
        p.StandardInput.WriteLine("q\n");
        p.Close();

        // Delete the input file
        File.Delete(inputFile);
    }
}