global using Microsoft.AspNetCore.SignalR;
global using AvnAudioSignalRDemo.Shared;
using AvnAudioSignalRDemo.Server.Hubs;
using Microsoft.AspNetCore.ResponseCompression;
using AvnAudioSignalRDemo.Server;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddSignalR();
builder.Services.AddResponseCompression(opts =>
{
    opts.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
        new[] { "application/octet-stream" });
});
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();
builder.Services.AddSingleton<AudioConverter>();

var app = builder.Build();

// Configure the HTTP request pipeline.

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
app.MapHub<ProcessAudioHub>("/processaudio");
app.MapFallbackToFile("index.html");

app.Run();
