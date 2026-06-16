using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ccms_backend.data;
using ccms_backend.services;

var builder = WebApplication.CreateBuilder(args);

// Explicitly configure configuration loading priority
builder.Configuration.Sources.Clear();
builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

// Override configuration with secret environment variables if present
var envDbConn = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING");
if (!string.IsNullOrEmpty(envDbConn))
{
    builder.Configuration["ConnectionStrings:DefaultConnection"] = envDbConn;
}
var envJwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET");
if (!string.IsNullOrEmpty(envJwtSecret))
{
    builder.Configuration["JwtSettings:Secret"] = envJwtSecret;
}
var envJwtIssuer = Environment.GetEnvironmentVariable("JWT_ISSUER");
if (!string.IsNullOrEmpty(envJwtIssuer))
{
    builder.Configuration["JwtSettings:Issuer"] = envJwtIssuer;
}
var envJwtAudience = Environment.GetEnvironmentVariable("JWT_AUDIENCE");
if (!string.IsNullOrEmpty(envJwtAudience))
{
    builder.Configuration["JwtSettings:Audience"] = envJwtAudience;
}

var envBlobConn = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING");
if (!string.IsNullOrEmpty(envBlobConn))
{
    builder.Configuration["AZURE_STORAGE_CONNECTION_STRING"] = envBlobConn;
}
var envBlobContainer = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONTAINER_NAME");
if (!string.IsNullOrEmpty(envBlobContainer))
{
    builder.Configuration["AZURE_STORAGE_CONTAINER_NAME"] = envBlobContainer;
}

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();

// Configure CORS — allow local dev (4200) and Docker frontend (4300)
builder.Services.AddCors(opts => opts.AddPolicy("CcmsPolicy",
    p => p.WithOrigins("http://localhost:4200", "http://localhost:4300", "http://localhost:8080")
          .AllowAnyHeader()
          .AllowAnyMethod()
          .AllowCredentials()));

// Configure EF Core:
//   Development → InMemory (no MySQL needed locally)
//   Production  → MySQL via Pomelo (used in Docker / AKS)
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseInMemoryDatabase("CcmsDb")
               .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning)));
}
else
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));
}

// Configure Repositories
builder.Services.AddScoped<ICaseRepository, CaseRepository>();
builder.Services.AddScoped<IBatchJobLogRepository, BatchJobLogRepository>();

// Configure Services
builder.Services.AddScoped<CaseService>();
builder.Services.AddScoped<BatchValidationService>();
builder.Services.AddHostedService<BatchSchedulerService>();
builder.Services.AddSingleton<Azure.Storage.Blobs.BlobServiceClient>(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var connectionString = configuration["AZURE_STORAGE_CONNECTION_STRING"];
    if (string.IsNullOrEmpty(connectionString))
    {
        connectionString = "UseDevelopmentStorage=true";
    }
    return new Azure.Storage.Blobs.BlobServiceClient(connectionString);
});
builder.Services.AddScoped<IFileStorageService, FileStorageService>();

// Configure JWT Authentication
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secret = jwtSettings["Secret"] ?? "SuperSecretJWTKey12345678901234567890";
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
        ValidIssuer = jwtSettings["Issuer"] ?? "CCMS.API",
        ValidAudience = jwtSettings["Audience"] ?? "CCMS.Client",
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret))
    };
});

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseMiddleware<ccms_backend.middleware.ExceptionHandlingMiddleware>();

app.UseSwagger();
app.UseSwaggerUI();

// Configure the HTTP request pipeline.
app.UseCors("CcmsPolicy");

app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

// Seed the database at startup
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<AppDbContext>();
        await DatabaseSeeder.SeedAsync(context);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred seeding the DB.");
    }
}

app.Run();

public partial class Program { }