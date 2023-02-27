using System.Collections.Concurrent;
using System.Diagnostics;

namespace AvnAudioSignalRDemo.Server;

public class AudioConverter
{
    private ConcurrentQueue<byte[]> InputBuffers = new ConcurrentQueue<byte[]>();
    private bool Working = false;
    private bool Done = false;
    private int SampleRate;
    private int Channels;
    private bool firstBuffer = true;
    private string FileName = string.Empty;

    public byte[] WebHeader { get; set; }

    public void ClearQueue()
    {
        firstBuffer = true;
        InputBuffers.Clear();
    }

    public void AddBuffer(byte[] buffer)
    {
        if (firstBuffer)
        {
            firstBuffer = false;
            // first buffer. Grab the header
            var mem = new MemoryStream();
            // This is a magic number.
            // The first 162 bytes of the first buffer
            //  are the webm header, that must be
            //  prepended to every buffer in order
            //  for FFMPEG to convert it properly
            mem.Write(buffer, 0, 162);
            WebHeader = mem.ToArray();
            mem.Dispose();
        }
        else
        {
            // Add the webm header to the buffer
            var mem = new MemoryStream();
            mem.Write(WebHeader);
            mem.Write(buffer);
            buffer = mem.ToArray();
            mem.Dispose();
        }
        // Add it to the Queue
        InputBuffers.Enqueue(buffer);
    }

    public byte[] ConvertWebMBufferToPCM(byte[] buffer, int SampleRate, int Channels, BufferPosition position)
    {
        this.SampleRate = SampleRate;
        this.Channels = Channels;

        if (position == BufferPosition.First)
        {
            // first buffer. Grab the header
            var mem = new MemoryStream();
            // This is a magic number.
            // The first 162 bytes of the first buffer
            //  are the webm header, that must be
            //  prepended to every buffer in order
            //  for FFMPEG to convert it properly
            mem.Write(buffer, 0, 162);
            WebHeader = mem.ToArray();
            mem.Dispose();
        }
        else
        {
            // Add the webm header to the buffer
            var mem = new MemoryStream();
            mem.Write(WebHeader);
            mem.Write(buffer);
            buffer = mem.ToArray();
            mem.Dispose();
        }
        
        return ConvertWebmToPcm(buffer);
    }

    public async Task StopProcessing()
    {
        Working = false;
        while(!Done)
        {
            await Task.Delay(200);
        }
    }

    public void StartProcessing(string fileName, int sampleRate, int channels)
    {
        SampleRate = sampleRate;
        Channels = channels;
        FileName = fileName;
        if (File.Exists(FileName))
            File.Delete(FileName);

        if (!Working)
        {
            var WorkingThread = new Thread(new ThreadStart(DoWork));
            WorkingThread.Start();
        }
    }

    private void DoWork()
    {
        Working = true;
        Done = false;
        while (Working)
        {
            if (InputBuffers.Count > 0)
            {
                if (InputBuffers.TryDequeue(out var inputBuffer))
                {
                    var outputBuffer = ConvertWebmToPcm(inputBuffer);
                    int NumberOfRetries = 4;
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
                            break; 
                        }
                        catch (IOException e) when (i <= NumberOfRetries)
                        {
                            Thread.Sleep(100);
                        }
                    }
                }
            }
            Thread.Sleep(100);
        }
        Done = true;
    }

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
        ffmpegProcess.Close();
        ffmpegProcess.Dispose();
        
        return pcmData;
    }

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

        File.Delete(inputFile);
    }
}