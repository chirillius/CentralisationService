using Server.Models;
using Server.Services;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls(builder.Configuration["Runtime:BaseUrl"] ?? "http://localhost:5101");
builder.Services.Configure<ServerNodeOptions>(builder.Configuration.GetSection("ServerNode"));
builder.Services.AddOpenApi();
builder.Services.AddControllers();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin();
    });
});
builder.Services.AddSingleton<FfmpegFrameGrabber>();
builder.Services.AddSingleton<ConnectorBindingService>();
builder.Services.AddSingleton<CameraConfigurationService>();
builder.Services.AddSingleton<CameraSecretsService>();
builder.Services.AddSingleton<CameraRtspAddressService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors();
app.MapControllers();
app.MapGet("/", () => Results.Ok(new
{
    service = "CentralisationService.Server",
    role = "site-connector",
    processingRule = "transport-only",
}));

app.Run();
