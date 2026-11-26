using BackgroundJobProcessor.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using BackgroundJobProcessor.Infrastructure.Persistence;
using BackgroundJobProcessor.Infrastructure.Repositories;

namespace BackgroundJobProcessor.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<BackgroundJobDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsAssembly(typeof(BackgroundJobDbContext).Assembly.FullName)));
        services.AddScoped<IBackgroundJobRepository, BackgroundJobRepository>();
        services.AddScoped<IJobExecutionRepository, JobExecutionRepository>();
        return services;
    }
}
