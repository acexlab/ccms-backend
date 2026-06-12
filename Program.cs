/*
 * File: Program.cs
 * Description: The primary entry point for the ASP.NET Core Web API. Configures all DI services and HTTP pipeline middlewares.
 * To Implement: Keep logging and settings clean.
 */

using System;
using System.Text;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using ccms_backend.data;
using ccms_backend.dtos;
using ccms_backend.middleware;
using ccms_backend.services;

var builder = WebApplication.CreateBuilder(args);

// 1. Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .CreateLogger();

builder.Host.UseSerilog();

try
{
    Log.Information("Starting CCMS Web API...");

    // 2. EF Core DbContext
    builder.Services.AddDbContext<AppDbContext>(opts =>
        opts.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

    // 3. Repositories
    builder.Services.AddScoped<ICaseRepository, CaseRepository>();
    builder.Services.AddScoped<IUserRepository, UserRepository>();
    builder.Services.AddScoped<IBankCustomerRepository, BankCustomerRepository>();
    builder.Services.AddScoped<IBatchJobLogRepository, BatchJobLogRepository>();

    // 4. File Storage Selection
    var storageMode = builder.Configuration["FileStorage:StorageMode"];
    if (storageMode == "AzureBlob")
    {
        builder.Services.AddScoped<IFileStorageService, AzureBlobStorageService>();
    }
    else
    {
        builder.Services.AddScoped<IFileStorageService, LocalFileStorageService>();
    }

    // 5. App Services & Helpers
    builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
    builder.Services.AddScoped<CaseNumberGenerator>();
    builder.Services.AddScoped<AuthService>();
    builder.Services.AddScoped<CaseService>();
    builder.Services.AddScoped<BatchValidationService>();

    // Validators
    builder.Services.AddScoped<IValidator<CreateCaseDto>, CreateCaseDtoValidator>();
    builder.Services.AddScoped<IValidator<SubmitResponseDto>, SubmitResponseDtoValidator>();

    // 6. Background Job
    builder.Services.AddHostedService<BatchValidationJob>();

    // Health Checks
    builder.Services.AddHealthChecks();

    // 7. JWT Authentication
    var jwtSettings = builder.Configuration.GetSection("JwtSettings");
    var secret = jwtSettings["Secret"] ?? "FallbackSecretKey12345678901234567890";
    builder.Services.AddAuthentication(opts =>
    {
        opts.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        opts.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(opts =>
    {
        opts.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidAudience = jwtSettings["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret))
        };
    });

    builder.Services.AddAuthorization();

    // 8. Controllers + Swagger
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo { Title = "CCMS API", Version = "v1" });
        
        // Add JWT Bearer support in Swagger UI
        c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
            Name = "Authorization",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.ApiKey,
            Scheme = "Bearer"
        });

        c.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    },
                    Scheme = "oauth2",
                    Name = "Bearer",
                    In = ParameterLocation.Header
                },
                Array.Empty<string>()
            }
        });
    });

    // 9. CORS
    builder.Services.AddCors(opts => opts.AddPolicy("CcmsPolicy",
        p => p.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod()));

    var app = builder.Build();

    // 10. Middleware pipeline
    app.UseMiddleware<ExceptionHandlingMiddleware>();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
        
        // Seed database
        await DatabaseSeeder.SeedAsync(app.Services);
    }

    app.UseCors("CcmsPolicy");
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();
    app.MapHealthChecks("/healthz");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "CCMS Web API terminated unexpectedly.");
}
finally
{
    Log.CloseAndFlush();
}
