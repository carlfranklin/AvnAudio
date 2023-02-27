# AvnAudio

A Blazor Component for recording audio in real time, providing audio buffers in real-time. 

#### FFMPEG Dependency

After poking around, it appears that most browsers no longer support recording raw WAV data. Instead, they are using [Google's audio/webm format](https://www.webmproject.org/). It's highly compressed, so it's good for data transfer, and the quality is decent. However, in order to do any kind of analysis on the audio data, it must be converted to raw PCM data. FFMPEG does this nicely.

The **AvnAudio** component doesn't use [FFMPEG](https://ffmpeg.org/download.html) itself, but the SignalR demo does. Before you use it, you must download [FFMPEG](https://ffmpeg.org/download.html) and copy FFMPEG.exe to the root of the **AvnAudioSignalRDemo.Server** project.



#### Solution Projects:

**AvnAudio**

This is a Razor Class Library that you can use in Blazor to record audio. You must add this to your services, in order to use it

```
builder.Services.AddScoped<AvnAudioInterop>();
```

It's built with .NET 7 and works in both Blazor Wasm and Blazor Server apps. The JavaScript is self-contained, so you do not need to link it. The only requirement is including the above service.



**AvnAudioWasmDemo**

This is a standalone Blazor Wasm app that records audio and saves it to a local webm file. It uses a package called [BlazorFileSaver](https://github.com/IvanJosipovic/BlazorFileSaver), which uses JavaScript to download the file when done.

> NOTE: you must enumerate the input devices and pick one before you can record.



**AvnAudioSignalRDemo**

This is a hosted Blazor Wasm app that records and sends each buffer up to a SignalR hub.

There are two Hub methods:

*ProcessAudioFileBuffer*

The SignalR hub saves the data to a local WebM file.

Once the whole file has been uploaded, the server calls out to [FFMPEG.exe](https://ffmpeg.org/download.html) to:

  a) convert the .webm file to .wav

  b) convert the wav sample type from 48000 to whatever the desired sample rate is

  c) delete the original .webm file

*ProcessAudioBuffer*

The SignalR hub converts each buffer from WebM to PCM with [FFMPEG.exe](https://ffmpeg.org/download.html) on the fly.

You can optionally add buffers to a queue in a background processor, which will process each buffer and write it to a PCM file as you upload.

