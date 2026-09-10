using System.Net.Http.Headers;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpClient("whisper", client => { client.BaseAddress = new Uri(builder.Configuration["WHISPER_URL"] ?? "http://localhost:9000"); client.Timeout = TimeSpan.FromMinutes(5); });
builder.Services.AddEndpointsApiExplorer(); builder.Services.AddSwaggerGen();
var app = builder.Build(); app.UseSwagger(); app.UseSwaggerUI();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapPost("/api/transcribe", async (IFormFile file, IHttpClientFactory clients, CancellationToken ct) =>
{
    if (file.Length == 0 || string.IsNullOrWhiteSpace(file.ContentType) || !file.ContentType.StartsWith("audio/")) return Results.BadRequest(new { detail = "Upload an audio file" });
    using var form = new MultipartFormDataContent(); await using var stream = file.OpenReadStream(); using var content = new StreamContent(stream);
    content.Headers.ContentType = MediaTypeHeaderValue.Parse(file.ContentType); form.Add(content, "audio_file", file.FileName); form.Add(new StringContent("transcribe"), "task"); form.Add(new StringContent("json"), "output");
    try { var response = await clients.CreateClient("whisper").PostAsync("asr", form, ct); if (!response.IsSuccessStatusCode) return Results.Problem("Whisper service returned an error", statusCode: 502); return Results.Content(await response.Content.ReadAsStringAsync(ct), "application/json"); }
    catch (HttpRequestException) { return Results.Problem("Whisper service is unavailable", statusCode: 502); }
});
app.Run(); public partial class Program { }
