using QType.COMMON;
using QType.Web.Services;

var builder = WebApplication.CreateBuilder(args);
// appsettings.Local.json is gitignored — developers override secrets there.
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<DictionaryService>();

var app = builder.Build();

var connectionString = builder.Configuration["Site:ConnectionString"];
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("Site:ConnectionString missing in appsettings.json");
QSingleton.GetInstance().SetConnectionString(connectionString);

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api", () => Results.Text(
    """
    qType — Kazakh language API

    GET /health
    GET /api/stats
    GET /api/spellcheck?word=<word>
    GET /api/suggest?word=<word>&k=5
    GET /api/define?word=<word>
    GET /api/random
    """, "text/plain; charset=utf-8"));

app.MapGet("/health", () => new { status = "ok" });

app.MapGet("/api/stats", async (DictionaryService svc) => await svc.StatsAsync());

app.MapGet("/api/spellcheck", async (string word, DictionaryService svc) =>
{
    if (string.IsNullOrWhiteSpace(word))
        return Results.BadRequest(new { error = "word query parameter required" });
    return Results.Ok(await svc.SpellCheckAsync(word));
});

app.MapGet("/api/suggest", async (string word, int? k, DictionaryService svc) =>
{
    if (string.IsNullOrWhiteSpace(word))
        return Results.BadRequest(new { error = "word query parameter required" });
    return Results.Ok(await svc.SuggestAsync(word, k ?? 5));
});

app.MapGet("/api/define", async (string word, DictionaryService svc) =>
{
    if (string.IsNullOrWhiteSpace(word))
        return Results.BadRequest(new { error = "word query parameter required" });
    return Results.Ok(await svc.DefineAsync(word));
});

app.MapGet("/api/random", async (DictionaryService svc) => await svc.RandomAsync());

app.Run();
