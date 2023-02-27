global using Microsoft.AspNetCore.SignalR;
global using AvnAudioSignalRDemo.Shared;
using AvnAudioSignalRDemo.Server.Hubs;
using Microsoft.AspNetCore.ResponseCompression;
using AvnAudioSignalRDemo.Server;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// Use SignalR hubs
builder.Services.AddSignalR();

// Response Compression reduces the SignalR payload size
builder.Services.AddResponseCompression(opts =>
{
    opts.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
        new[] { "application/octet-stream" });
});

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

// AudioConverter is a service that converts audio buffers
// from WebM to PCM on a background thread
builder.Services.AddSingleton<AudioConverter>();

var app = builder.Build();

// Configure the HTTP request pipeline.

// Required for compression
app.UseResponseCompression();

if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

app.UseRouting();

app.MapRazorPages();
app.MapControllers();

// Map our SignalR hub
app.MapHub<ProcessAudioHub>("/processaudio");

app.MapFallbackToFile("index.html");

app.Run();
