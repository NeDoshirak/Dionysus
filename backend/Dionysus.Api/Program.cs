using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);
var databaseUrl = builder.Configuration["DATABASE_URL"];

builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (databaseUrl is null) options.UseInMemoryDatabase("dionysus");
    else options.UseNpgsql(databaseUrl);
});
builder.Services.AddIdentityCore<AppUser>(options =>
{
    options.User.RequireUniqueEmail = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireDigit = true;
    options.Password.RequireUppercase = true;
}).AddEntityFrameworkStores<AppDbContext>().AddDefaultTokenProviders();
if (builder.Configuration["VALKEY_CONNECTION"] is null) builder.Services.AddDistributedMemoryCache();
else builder.Services.AddStackExchangeRedisCache(options => options.Configuration = builder.Configuration["VALKEY_CONNECTION"]);
builder.Services.AddScoped<CodeService>();
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
builder.Services.AddScoped<IMediaConverter, FfmpegMediaConverter>();
builder.Services.AddScoped<ITranscriptionService, WhisperTranscriptionService>();
builder.Services.AddScoped<ITextGenerationService, YandexAiService>();
builder.Services.AddScoped<ISpecificationContractValidator, SpecificationContractValidator>();
builder.Services.AddScoped<ISpecificationPromptFactory, SpecificationPromptFactory>();
builder.Services.AddScoped<IStructuredSpecificationAiService, StructuredYandexAiService>();
builder.Services.AddScoped<ISpecificationAnalysisOrchestrator, SpecificationOrchestrator>();
builder.Services.AddSingleton<ISpecificationAnalysisQueue, SpecificationAnalysisQueue>();
builder.Services.AddHostedService<SpecificationAnalysisWorker>();
builder.Services.AddHttpClient("whisper", client => { client.BaseAddress = new Uri(builder.Configuration["WHISPER_URL"] ?? "http://localhost:9000"); client.Timeout = TimeSpan.FromMinutes(5); });
builder.Services.AddHttpClient("yandex-ai", client => { client.BaseAddress = new Uri("https://ai.api.cloud.yandex.net/"); client.Timeout = TimeSpan.FromMinutes(2); });
var tokenService = new TokenService(
    builder.Configuration,
    new Microsoft.Extensions.Caching.Distributed.MemoryDistributedCache(
        Microsoft.Extensions.Options.Options.Create(new Microsoft.Extensions.Caching.Memory.MemoryDistributedCacheOptions())),
    builder.Environment);
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options => options.TokenValidationParameters = tokenService.Parameters());
builder.Services.AddAuthorization();
builder.Services.AddProblemDetails();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Введите только access token. Swagger добавит Bearer автоматически."
    });
    options.OperationFilter<AuthorizeOperationFilter>();
});

var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var database = scope.ServiceProvider.GetRequiredService<AppDbContext>().Database;
    database.EnsureCreated();
    if (databaseUrl is not null) database.Migrate();
}
app.UseExceptionHandler();
app.UseSwagger(); app.UseSwaggerUI();
app.UseAuthentication(); app.UseAuthorization();
app.MapControllers();
app.Run();
public partial class Program { }
