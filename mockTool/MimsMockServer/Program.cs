using Microsoft.AspNetCore.Server.Kestrel.Core;
using MimsMockServer.Services;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenLocalhost(50051, listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http2;
    });
    options.ListenLocalhost(7002, listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
    });
});

// Add services to the container.
builder.Services.AddGrpc();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.MapGrpcService<MimsBridgeMockService>();

app.MapGet("/api/tms/health", () => Results.Json(new
{
    success = true,
    service = "tms-mock",
    timestamp = DateTimeOffset.UtcNow.ToString("O")
}));

app.MapPost("/api/tms/version-requirements", () => Results.Json(new
{
    stationId = "STATION-001",
    lineId = "LINE-001",
    source = "mims-mock-server",
    devices = new[]
    {
        new { deviceKey = "hw_powermeter", requiredVersion = "PM-1.3.2" },
        new { deviceKey = "hw_fixture_ctrl", requiredVersion = "FX-0.9.8" },
        new { deviceKey = "sw_test_program", requiredVersion = "TP-5.2.0" },
        new { deviceKey = "sw_station_agent", requiredVersion = "AGENT-2.1.4" },
        new { deviceKey = "fw_product_main", requiredVersion = "FW-2.5.1" },
        new { deviceKey = "fw_fixture_mcu", requiredVersion = "MCU-1.7.4" },
        new { deviceKey = "fpga_main", requiredVersion = "FPGA-3.4.0" },
        new { deviceKey = "cpld_ctrl", requiredVersion = "CPLD-1.2.7" }
    }
}));

app.MapGet("/api/tms/default-info", () => Results.Json(new
{
    stationId = "STATION-001",
    lineId = "LINE-001",
    partNumber = "TEST-001",
    author = "YUD",
    spec = "GUI"
}));

app.MapGet("/api/tms/lut/download/default", () => Results.Text(
    """
    # LUT_VERSION=1.0.3
    # STATION_ID=STATION-001
    # LINE_ID=LINE-001
    TABLE_BEGIN
    CH001,0.985,0.012
    CH002,0.988,0.011
    CH003,0.991,0.010
    CH004,0.987,0.012
    TABLE_END
    """,
    "text/plain"));

app.MapGet("/", () => "Mock MIMS gRPC server is running on http://127.0.0.1:50051");

app.Run();
