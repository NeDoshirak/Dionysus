using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;
using Xunit;

public sealed class SpecificationApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder().WithImage("postgres:16-alpine").Build();
    private readonly RedisContainer _valkey = new RedisBuilder().WithImage("valkey/valkey:7-alpine").Build();
    private readonly object _sync = new();
    private bool _started;

    public RecordingSpecificationQueue Queue { get; } = new();
    public string DatabaseConnectionString => _postgres.GetConnectionString();
    public string ValkeyConnectionString => _valkey.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await _valkey.StartAsync();
        _started = true;
        CreateClient().Dispose();
        await using var db = CreateDbContext();
        await db.Database.EnsureCreatedAsync();
    }

    public override async ValueTask DisposeAsync()
    {
        await DisposeContainersAsync();
        await base.DisposeAsync();
    }

    Task IAsyncLifetime.DisposeAsync() => DisposeContainersAsync();

    private async Task DisposeContainersAsync()
    {
        if (!_started) return;
        await _valkey.DisposeAsync();
        await _postgres.DisposeAsync();
        _started = false;
    }

    public AppDbContext CreateDbContext() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql(DatabaseConnectionString)
        .Options);

    public async Task ResetDatabaseAsync()
    {
        Queue.Clear();
        await using var db = CreateDbContext();
        await db.Database.ExecuteSqlRawAsync("TRUNCATE TABLE \"AspNetUserTokens\", \"AspNetUserRoles\", \"AspNetUserLogins\", \"AspNetUserClaims\", \"AspNetRoleClaims\", \"AspNetRoles\", \"AspNetUsers\", \"SpecificationItemStatements\", \"SpecificationItems\", \"SpecificationFunctionStatements\", \"SpecificationFunctions\", \"AnalysisRelationSourceStatements\", \"AnalysisRelationTargetStatements\", \"AnalysisStatementSegments\", \"AnalysisRelations\", \"AnalysisStatements\", \"AnalysisTopics\", \"SpecificationAnalyses\", \"TranscriptSegments\", \"VoiceRecordings\", \"Projects\" CASCADE");
    }

    public HttpClient ClientForOwner(string userId = "user-1")
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CreateToken(userId));
        return client;
    }

    public async Task<SpecificationAnalysisJob> ProcessNextAsync(CancellationToken cancellationToken = default)
    {
        var job = await Queue.DequeueAsync(cancellationToken);
        await using var scope = Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ISpecificationAnalysisOrchestrator>().RunAsync(job, cancellationToken);
        return job;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("VALKEY_CONNECTION", ValkeyConnectionString);
        builder.UseSetting("JWT_KEY", "test-key-that-is-long-enough-for-hmac-sha256");
        builder.UseSetting("JWT_ISSUER", "Dionysus");
        builder.UseSetting("JWT_AUDIENCE", "Dionysus");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DATABASE_URL"] = DatabaseConnectionString,
            ["VALKEY_CONNECTION"] = ValkeyConnectionString,
            ["JWT_KEY"] = "test-key-that-is-long-enough-for-hmac-sha256",
            ["JWT_ISSUER"] = "Dionysus",
            ["JWT_AUDIENCE"] = "Dionysus"
        }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.AddDbContext<AppDbContext>(options => options.UseNpgsql(DatabaseConnectionString));
            Remove<Microsoft.Extensions.Caching.Distributed.IDistributedCache>(services);
            Remove<IEmailSender>(services);
            Remove<IMediaConverter>(services);
            Remove<ITranscriptionService>(services);
            Remove<ITextGenerationService>(services);
            Remove<ISpecificationAnalysisQueue>(services);
            RemoveHostedWorker(services);
            services.AddStackExchangeRedisCache(options => options.Configuration = ValkeyConnectionString);
            services.AddSingleton<IEmailSender, DeterministicEmailSender>();
            services.AddSingleton<IMediaConverter, DeterministicMediaConverter>();
            services.AddSingleton<ITranscriptionService, DeterministicTranscriptionService>();
            services.AddSingleton<ITextGenerationService>(new ScriptedTextGenerationService((_, instructions) => throw new InvalidOperationException($"No AI script configured for {instructions}.")));
            services.AddSingleton<ISpecificationAnalysisQueue>(Queue);
        });
    }

    private string CreateToken(string userId) => new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
        issuer: "Dionysus", audience: "Dionysus",
        claims: [new Claim(ClaimTypes.NameIdentifier, userId), new Claim(ClaimTypes.Email, $"{userId}@example.com")],
        expires: DateTime.UtcNow.AddMinutes(15),
        signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes("test-key-that-is-long-enough-for-hmac-sha256")), SecurityAlgorithms.HmacSha256)));

    private static void Remove<T>(IServiceCollection services) =>
        services.Where(x => x.ServiceType == typeof(T)).ToList().ForEach(x => services.Remove(x));

    private static void RemoveHostedWorker(IServiceCollection services) =>
        services.Where(x => x.ServiceType == typeof(IHostedService) && x.ImplementationType == typeof(SpecificationAnalysisWorker)).ToList().ForEach(x => services.Remove(x));
}

public sealed class DeterministicEmailSender : IEmailSender
{
    public Task SendAsync(string to, string subject, string body) => Task.CompletedTask;
}

public sealed class DeterministicMediaConverter : IMediaConverter
{
    public Task<byte[]> ExtractAudioAsync(byte[] video, string extension, CancellationToken cancellationToken) => Task.FromResult(video);
}

public sealed class DeterministicTranscriptionService : ITranscriptionService
{
    public Task<TranscriptionResult> TranscribeAsync(byte[] audio, string fileName, CancellationToken cancellationToken) =>
        Task.FromResult(new TranscriptionResult("Need corporate sign-in.", "en", [new TranscriptionSegment(0, 12.5, "Need corporate sign-in.")]));
}
