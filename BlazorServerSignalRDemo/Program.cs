using AvnAudio;
using BlazorServerSignalRDemo;
using BlazorServerSignalRDemo.Components;
using BlazorServerSignalRDemo.Hubs;
using Microsoft.AspNetCore.ResponseCompression;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
	.AddInteractiveServerComponents();

// Use SignalR hubs
builder.Services.AddSignalR();

// Response Compression reduces the SignalR payload size
builder.Services.AddResponseCompression(opts =>
{
	opts.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
		new[] { "application/octet-stream" });
});

// AudioConverter is a service that converts audio buffers
// from WebM to PCM on a background thread
builder.Services.AddSingleton<AudioConverter>();

// Required to use AvnAudio
builder.Services.AddScoped<AvnAudioInterop>();

var app = builder.Build();

// Configure the HTTP request pipeline.

// Required for compression
app.UseResponseCompression();

if (!app.Environment.IsDevelopment())
{
	app.UseExceptionHandler("/Error", createScopeForErrors: true);
	// The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
	app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
	.AddInteractiveServerRenderMode();

// Map our SignalR hub
app.MapHub<ProcessAudioHub>("/processaudio");

app.Run();
