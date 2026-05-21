using FluentValidation;
using Scalar.AspNetCore;
using ServiceLayer.DTW.Application.Interfaces;
using ServiceLayer.DTW.Application.Validation;
using ServiceLayer.DTW.Domain.Models;
using ServiceLayer.DTW.Infrastructure.Mapping;
using ServiceLayer.DTW.Infrastructure.Parsing;
using ServiceLayer.DTW.Infrastructure.ServiceLayer;

var builder = WebApplication.CreateBuilder(args);

// --- Configuration ---
builder.Services.Configure<ServiceLayerConfig>(
    builder.Configuration.GetSection("ServiceLayer"));

// --- MediatR ---
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(
        typeof(ServiceLayer.DTW.Application.UseCases.ImportBusinessPartners
                                .ImportBusinessPartnersHandler).Assembly));

// --- FluentValidation ---
builder.Services.AddScoped<IValidator<BusinessPartner>, BusinessPartnerValidator>();

// --- File Parsers ---
builder.Services.AddScoped<IFileParser, CsvParser>();
builder.Services.AddScoped<IFileParser, ExcelParser>();
builder.Services.AddScoped<FileParserResolver>();

// --- Service Layer HTTP Client ---
builder.Services.AddHttpClient<IServiceLayerClient, ServiceLayerClient>(client =>
{
    var config = builder.Configuration.GetSection("ServiceLayer").Get<ServiceLayerConfig>()!;
    client.BaseAddress = new Uri(config.BaseUrl);
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    // Accept self-signed certificates on dev SAP servers
    ServerCertificateCustomValidationCallback =
        HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
    UseCookies = true,
    CookieContainer = new System.Net.CookieContainer()
});

// --- CORS (for Blazor WASM) ---
builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins("https://localhost:7001")
              .AllowAnyHeader()
              .AllowAnyMethod()));

// --- Controllers & OpenAPI (Scalar) ---
builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();          // serves /openapi/v1.json
    app.MapScalarApiReference(); // serves /scalar/v1
}

app.UseHttpsRedirection();
app.UseCors();
app.MapControllers();
app.Run();