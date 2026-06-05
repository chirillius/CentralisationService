using Neuro.Models;
using Neuro.Services;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls(builder.Configuration["Runtime:BaseUrl"] ?? "http://localhost:5300");
builder.Services.Configure<RetailModelOptions>(builder.Configuration.GetSection("RetailModels"));
builder.Services.AddOpenApi();
builder.Services.AddControllers();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin();
    });
});
builder.Services.AddSingleton<DefectCatalogService>();
builder.Services.AddSingleton<RetailAnalysisService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors();
app.MapControllers();
app.MapGet("/", () => Results.Ok(new
{
    service = "CentralisationService.Neuro",
    role = "centralized-ai",
    retailAnalysisMode = builder.Configuration.GetValue<bool>("RetailModels:UseStubFallback") ? "onnx-with-stub-fallback" : "onnx-only",
}));

app.Run();
