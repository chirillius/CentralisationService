using CentralServer.Models;
using CentralServer.Services;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls(builder.Configuration["Runtime:BaseUrl"] ?? "http://localhost:5200");
builder.Services.Configure<MotionMonitoringOptions>(builder.Configuration.GetSection("MotionMonitoring"));
builder.Services.Configure<StoreCatalogOptions>(builder.Configuration.GetSection("StoreCatalog"));
builder.Services.Configure<ZoneCatalogOptions>(builder.Configuration.GetSection("ZoneCatalog"));
builder.Services.Configure<RetailDetectionMonitoringOptions>(builder.Configuration.GetSection("RetailDetectionMonitoring"));
builder.Services.Configure<AccessOptions>(builder.Configuration.GetSection("Access"));
builder.Services.Configure<PostgreSqlOptions>(builder.Configuration.GetSection("PostgreSql"));
builder.Services.AddOpenApi();
builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin();
    });
});
builder.Services.AddHttpClient(nameof(RemoteFrameProxyService));
builder.Services.AddHttpClient(nameof(ServerRegistryService));
builder.Services.AddSingleton(PostgreSqlDataSourceFactory.Create(builder.Configuration));
builder.Services.AddHostedService<PostgreSqlSchemaInitializer>();
builder.Services.AddSingleton<ServerRegistryService>();
builder.Services.AddSingleton<RemoteFrameProxyService>();
builder.Services.AddSingleton<MotionDetectionService>();
builder.Services.AddSingleton<MotionFrameIndexService>();
builder.Services.AddSingleton<MotionFrameArchiveService>();
builder.Services.AddSingleton<ZoneCatalogService>();
builder.Services.AddSingleton<RetailDetectionProfileCatalogService>();
builder.Services.AddSingleton<NeuroRetailAnalysisService>();
builder.Services.AddSingleton<RetailDetectionEvidenceArchiveService>();
builder.Services.AddSingleton<AccessStoreService>();
builder.Services.AddScoped<PlatformAdminAccessService>();
builder.Services.AddScoped<CompanyAccessContextService>();
builder.Services.AddHostedService<StoreCatalogSyncBackgroundService>();
builder.Services.AddHostedService<MotionMonitoringBackgroundService>();
builder.Services.AddHostedService<RetailDetectionMonitoringBackgroundService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors();
app.UseMiddleware<CompanyAccessMiddleware>();
app.MapControllers();
app.MapGet("/", () => Results.Ok(new
{
    service = "CentralisationService.CentralServer",
    processingRule = "all-processing-lives-here",
    archiveRoot = Path.Combine(app.Environment.ContentRootPath, "videos"),
}));

app.Run();
