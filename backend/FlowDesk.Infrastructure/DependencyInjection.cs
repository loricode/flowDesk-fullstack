using Amazon;
using Amazon.S3;
using Amazon.SQS;
using FlowDesk.Application.Abstractions;
using FlowDesk.Infrastructure.Messaging;
using FlowDesk.Infrastructure.Persistence;
using FlowDesk.Infrastructure.Security;
using FlowDesk.Infrastructure.Services;
using FlowDesk.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FlowDesk.Infrastructure;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>"SqlServer" (RDS / local) o "Sqlite" (desarrollo rápido sin servidor).</summary>
    public string Provider { get; set; } = "Sqlite";

    /// <summary>Crea el esquema desde el modelo si no existe. En producción el esquema lo gestionan database/Scripts.</summary>
    public bool EnsureCreated { get; set; } = true;
    public bool SeedDemoData { get; set; } = true;
}

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.SectionName));
        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<MessagingOptions>(configuration.GetSection(MessagingOptions.SectionName));

        var database = configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>() ?? new();
        var connectionString = configuration.GetConnectionString("FlowDesk")
            ?? throw new InvalidOperationException("ConnectionStrings:FlowDesk no configurada.");

        if (!database.Provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            var dataSource = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(connectionString).DataSource;
            var directory = Path.GetDirectoryName(Path.GetFullPath(dataSource));
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        }

        services.AddDbContext<FlowDeskDbContext>(options =>
        {
            if (database.Provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
                options.UseSqlServer(connectionString, sql =>
                {
                    sql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
                    sql.EnableRetryOnFailure(3);
                });
            else
                options.UseSqlite(connectionString, sqlite => sqlite.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery));
        });
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<FlowDeskDbContext>());
        services.AddScoped<DbSeeder>();

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddScoped<IAuditWriter, AuditWriter>();

        var storage = configuration.GetSection(StorageOptions.SectionName).Get<StorageOptions>() ?? new();
        if (storage.Provider.Equals("S3", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IAmazonS3>(_ => CreateS3Client(configuration));
            services.AddSingleton<IFileStorage, S3FileStorage>();
        }
        else
        {
            services.AddSingleton<IFileStorage, LocalFileStorage>();
        }

        return services;
    }

    /// <summary>Registra el Outbox y el consumo de mensajes (usado por FlowDesk.Worker).</summary>
    public static IServiceCollection AddMessageProcessing(this IServiceCollection services, IConfiguration configuration)
    {
        var messaging = configuration.GetSection(MessagingOptions.SectionName).Get<MessagingOptions>() ?? new();
        services.AddScoped<IntegrationEventDispatcher>();

        if (messaging.Provider.Equals("Sqs", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IAmazonSQS>(_ => CreateSqsClient(configuration));
            services.AddSingleton<IMessageBus, SqsMessageBus>();
            services.AddHostedService<SqsConsumerService>();
        }
        else
        {
            services.AddSingleton<IMessageBus, InProcessMessageBus>();
        }

        services.AddHostedService<OutboxDispatcherService>();
        return services;
    }

    public static IServiceCollection AddHttpRealtimeRelay(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RealtimeRelayOptions>(configuration.GetSection(RealtimeRelayOptions.SectionName));
        services.AddHttpClient<IRealtimeNotifier, HttpRealtimeNotifier>((sp, client) =>
        {
            var o = sp.GetRequiredService<IOptions<RealtimeRelayOptions>>().Value;
            client.BaseAddress = new Uri(o.ApiBaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(10);
        });
        return services;
    }

    /// <summary>Crea el esquema (si corresponde) y aplica el seed. Llamado al iniciar la API.</summary>
    public static async Task InitializeDatabaseAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
        var db = scope.ServiceProvider.GetRequiredService<FlowDeskDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("FlowDesk.Database");

        if (options.EnsureCreated && await db.Database.EnsureCreatedAsync(ct))
            logger.LogInformation("Esquema de base de datos creado ({Provider}).", db.Database.ProviderName);

        await scope.ServiceProvider.GetRequiredService<DbSeeder>().SeedAsync(options.SeedDemoData, ct);
    }

    private static AmazonS3Client CreateS3Client(IConfiguration configuration)
    {
        var aws = configuration.GetSection("AWS");
        var config = new AmazonS3Config();
        if (aws["ServiceURL"] is { Length: > 0 } url) { config.ServiceURL = url; config.ForcePathStyle = true; } // LocalStack
        else config.RegionEndpoint = RegionEndpoint.GetBySystemName(aws["Region"] ?? "us-east-1");
        return new AmazonS3Client(config);
    }

    private static AmazonSQSClient CreateSqsClient(IConfiguration configuration)
    {
        var aws = configuration.GetSection("AWS");
        var config = new AmazonSQSConfig();
        if (aws["ServiceURL"] is { Length: > 0 } url) config.ServiceURL = url;
        else config.RegionEndpoint = RegionEndpoint.GetBySystemName(aws["Region"] ?? "us-east-1");
        return new AmazonSQSClient(config);
    }
}
