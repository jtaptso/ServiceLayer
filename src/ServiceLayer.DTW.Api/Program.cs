using FluentValidation;
using ServiceLayer.DTW.Application.Interfaces;
using ServiceLayer.DTW.Application.UseCases.ImportBusinessPartners;
using ServiceLayer.DTW.Application.Validation;
using ServiceLayer.DTW.Domain.Models;
using ServiceLayer.DTW.Infrastructure.Mapping;
using ServiceLayer.DTW.Infrastructure.Parsing;
using ServiceLayer.DTW.Infrastructure.ServiceLayer;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddOpenApi();

// Configuration
var slConfig = builder.Configuration.GetSection("ServiceLayer").Get<ServiceLayerConfig>()
    ?? throw new InvalidOperationException("ServiceLayer configuration is missing");
builder.Services.Configure<ServiceLayerConfig>(builder.Configuration.GetSection("ServiceLayer"));

// HttpClient for Service Layer
builder.Services.AddHttpClient<IServiceLayerClient, ServiceLayerClient>(client =>
{
    client.BaseAddress = new Uri(slConfig.BaseUrl);
});

// Parsers
builder.Services.AddScoped<IFileParser, CsvParser>();
builder.Services.AddScoped<IFileParser, ExcelParser>();
builder.Services.AddScoped<IFileParserResolver, FileParserResolver>();

// Mapping
builder.Services.AddSingleton<IBusinessPartnerMapper, BusinessPartnerMapper>();

// Validation
builder.Services.AddScoped<IValidator<BusinessPartner>, BusinessPartnerValidator>();

// MediatR
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<ImportBusinessPartnersCommand>());

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.Run();
