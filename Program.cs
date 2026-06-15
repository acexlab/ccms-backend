using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ccms_backend.data;
using ccms_backend.services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddSwaggerGen();

// Configure CORS
builder.Services.AddCors(opts => opts.AddPolicy("CcmsPolicy",
    p => p.WithOrigins("http://localhost:4200")
          .AllowAnyHeader()
          .AllowAnyMethod()
          .AllowCredentials()));

// Configure EF Core with InMemory for easy testing
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseInMemoryDatabase("CcmsDb"));

// Configure Repositories
builder.Services.AddScoped<ICaseRepository, CaseRepository>();
builder.Services.AddScoped<IBatchJobLogRepository, BatchJobLogRepository>();

// Configure Services
builder.Services.AddScoped<CaseService>();
builder.Services.AddScoped<BatchValidationService>();

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

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

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