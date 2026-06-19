using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using CCMS.Infrastructure.Data;
using CCMS.Application.Interfaces;
using CCMS.Application.Services;
using CCMS.Infrastructure.Services;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting CCMS Web API...");

    var builder = WebApplication.CreateBuilder(args);

    // Register Serilog as the logging provider
    builder.Services.AddSerilog((services, lc) => lc
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    // Add services to the container.
    builder.Services.AddControllers().AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new UtcDateTimeConverter());
    });
    // Use LocalFileStorageService in dev (when Azure not configured), AzureBlobStorageService in prod
    var blobConnStr = builder.Configuration["AZURE_STORAGE_CONNECTION_STRING"]
                   ?? builder.Configuration["BlobStorage:ConnectionString"] ?? "";
    if (string.IsNullOrEmpty(blobConnStr) || blobConnStr == "UseDevelopmentStorage=true;")
        builder.Services.AddSingleton<IBlobStorageService, LocalFileStorageService>();
    else
        builder.Services.AddSingleton<IBlobStorageService, AzureBlobStorageService>();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new Microsoft.OpenApi.OpenApiInfo { Title = "CCMS API", Version = "v1" });
        c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.OpenApiSecurityScheme
        {
            Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
            Name = "Authorization",
            In = Microsoft.OpenApi.ParameterLocation.Header,
            Type = Microsoft.OpenApi.SecuritySchemeType.ApiKey,
            Scheme = "Bearer"
        });

        c.AddSecurityRequirement(document => new Microsoft.OpenApi.OpenApiSecurityRequirement
        {
            {
                new Microsoft.OpenApi.OpenApiSecuritySchemeReference("Bearer", document),
                new List<string>()
            }
        });
    });

    // Configure CORS
    var allowedOrigins = new[]
    {
        "http://localhost:4200",
        "http://localhost:4300",
        "https://20.75.151.63",
        "http://20.75.151.63",
    };
    builder.Services.AddCors(opts => opts.AddPolicy("CcmsPolicy",
        p => p.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials()));

    var connectionString = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING") ?? builder.Configuration.GetConnectionString("DefaultConnection");
    if (builder.Environment.EnvironmentName != "Testing")
    {
        builder.Services.AddDbContext<AppDbContext>(options =>
            options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 36))));
    }

    builder.Services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<AppDbContext>());

    // Configure Repositories
    builder.Services.AddScoped<ICaseRepository, CaseRepository>();
    builder.Services.AddScoped<IBatchJobLogRepository, BatchJobLogRepository>();
    builder.Services.AddScoped<IUserRepository, UserRepository>();

    // Configure Services
    builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
    builder.Services.AddScoped<CaseService>();
    builder.Services.AddScoped<BatchValidationService>();
    builder.Services.AddHostedService<BatchSchedulerService>();

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

    app.UseSwagger();
    app.UseSwaggerUI();

    // Configure the HTTP request pipeline.
    app.UseCors("CcmsPolicy");

    // Enable Serilog Request Logging
    app.UseSerilogRequestLogging();

    app.UseStaticFiles();

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();
    app.MapGet("/health", () => Results.Ok("Healthy"));

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
}
catch (Exception ex)
{
    Log.Fatal(ex, "CCMS Web API terminated unexpectedly!");
}
finally
{
    Log.CloseAndFlush();
}