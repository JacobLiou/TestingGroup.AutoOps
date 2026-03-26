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

app.MapGet("/api/tms/hw-config-integrity", () => Results.Json(new
{
    items = new[]
    {
        new { name = "HW/FW/FPGA/CPLD版本匹配", expected = "all_match", actual = "all_match", pass = true },
        new { name = "默认配置签名校验", expected = "valid", actual = "valid", pass = true },
        new { name = "损坏数据块数量", expected = "0", actual = "0", pass = true },
        new { name = "错误配置项数量", expected = "0", actual = "1", pass = false }
    }
}));

app.MapGet("/api/tms/hw-status-groups", (string? group) =>
{
    var key = (group ?? "optical").Trim().ToLowerInvariant();
    var groups = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
    {
        ["optical"] = new
        {
            groupKey = "optical",
            items = new[]
            {
                new { name = "PD通信", expected = "ok", actual = "ok", pass = true },
                new { name = "VOA调节", expected = "ok", actual = "ok", pass = true },
                new { name = "SW切换", expected = "ok", actual = "ok", pass = true },
                new { name = "Pump驱动", expected = "ok", actual = "warn", pass = false },
                new { name = "DFB激光器", expected = "ok", actual = "ok", pass = true },
                new { name = "TEC控温", expected = "ok", actual = "ok", pass = true },
                new { name = "Heater控制", expected = "ok", actual = "ok", pass = true }
            }
        },
        ["control_storage"] = new
        {
            groupKey = "control_storage",
            items = new[]
            {
                new { name = "MCU状态", expected = "ok", actual = "ok", pass = true },
                new { name = "EEPROM读写", expected = "ok", actual = "ok", pass = true },
                new { name = "Flash校验", expected = "ok", actual = "ok", pass = true },
                new { name = "温度传感器", expected = "ok", actual = "ok", pass = true },
                new { name = "Watchdog心跳", expected = "ok", actual = "ok", pass = true }
            }
        },
        ["interface_comm"] = new
        {
            groupKey = "interface_comm",
            items = new[]
            {
                new { name = "I/O线状态", expected = "ok", actual = "ok", pass = true },
                new { name = "IO Port映射", expected = "ok", actual = "ok", pass = true },
                new { name = "DAC输出", expected = "ok", actual = "ok", pass = true },
                new { name = "ADC采样", expected = "ok", actual = "ok", pass = true },
                new { name = "SPI通信压力测试", expected = "ok", actual = "ok", pass = true },
                new { name = "I2C通信压力测试", expected = "ok", actual = "ok", pass = true }
            }
        }
    };

    if (!groups.TryGetValue(key, out var payload))
    {
        return Results.NotFound(new { message = $"group not found: {key}" });
    }

    return Results.Json(payload);
});

app.MapGet("/api/tms/optical-risk", () => Results.Json(new
{
    rtsCompleted = true,
    items = new[]
    {
        new { name = "FiberBreaksResidual", expected = "0", actual = "0", pass = true },
        new { name = "MaxInsertionLoss", expected = "<= 1.20 dB", actual = "1.09 dB", pass = true },
        new { name = "TrayAnomalyResidual", expected = "0", actual = "1", pass = false }
    },
    residualIssues = new[] { "盘盒轻微异常" }
}));

app.MapGet("/", () => "Mock MIMS gRPC server is running on http://127.0.0.1:50051");

app.Run();
