using Microsoft.AspNetCore.Server.Kestrel.Core;
using MimsMockServer.Services;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenLocalhost(50051, listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http2;
    });
});

// Add services to the container.
builder.Services.AddGrpc();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.MapGrpcService<MimsBridgeMockService>();
app.MapGet("/", () => "Mock MIMS gRPC server is running on http://127.0.0.1:50051");

app.Run();
