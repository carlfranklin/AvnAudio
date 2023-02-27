global using AvnAudio;
using AvnAudioWasmDemo;
using BlazorFileSaver;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
// This utility uses JavaScript to let us download a file in a WASM app
builder.Services.AddBlazorFileSaver();

// Required to use AvnAudio
builder.Services.AddScoped<AvnAudioInterop>();
await builder.Build().RunAsync();
