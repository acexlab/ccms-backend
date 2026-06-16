using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.MsSql;
using Xunit;
using ccms_backend.data;
using ccms_backend.services;

namespace CCMS.IntegrationTests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private MsSqlContainer? _dbContainer;
    private bool _useInMemory = false;

    public async Task InitializeAsync()
    {
        try
        {
            _dbContainer = new MsSqlBuilder()
                .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
                .Build();
            await _dbContainer.StartAsync();
        }
        catch (Exception)
        {
            _useInMemory = true;
            _dbContainer = null;
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // 1. Remove original AppDbContext DbContextOptions
            var dbContextDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (dbContextDescriptor != null)
            {
                services.Remove(dbContextDescriptor);
            }

            // 2. Add AppDbContext configured to use Testcontainers MsSql or InMemory fallback
            services.AddDbContext<AppDbContext>(options =>
            {
                if (_useInMemory || _dbContainer == null)
                {
                    options.UseInMemoryDatabase("CcmsIntegrationTestDb")
                           .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning));
                }
                else
                {
                    options.UseSqlServer(_dbContainer.GetConnectionString());
                }
            });

            // 3. Remove background BatchSchedulerService HostedService to prevent background worker interference
            var schedulerDescriptor = services.SingleOrDefault(
                d => d.ImplementationType == typeof(BatchSchedulerService));
            if (schedulerDescriptor != null)
            {
                services.Remove(schedulerDescriptor);
            }
        });
    }

    public async Task ResetDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await TestDataSeeder.SeedAsync(context);
    }

    public new async Task DisposeAsync()
    {
        if (_dbContainer != null)
        {
            await _dbContainer.StopAsync();
        }
    }
}
