using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PCSentinel.Service;

var builder = Host.CreateApplicationBuilder(args);

// Enable running as a Windows Service if hosted on Windows OS
if (OperatingSystem.IsWindows())
{
    builder.Services.AddWindowsService(options =>
    {
        options.ServiceName = "PCSentinelBackgroundService";
    });
}

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
await host.RunAsync();
