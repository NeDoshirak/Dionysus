using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

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
builder.Services.AddHttpClient("whisper", client => { client.BaseAddress = new Uri(builder.Configuration["WHISPER_URL"] ?? "http://localhost:9000"); client.Timeout = TimeSpan.FromMinutes(5); });
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
builder.Services.AddSwaggerGen();

var app = builder.Build();
using (var scope = app.Services.CreateScope()) { scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated(); }
app.UseExceptionHandler();
app.UseSwagger(); app.UseSwaggerUI();
app.UseAuthentication(); app.UseAuthorization();
app.MapControllers();
app.Run();
public partial class Program { }
