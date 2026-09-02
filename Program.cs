using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MyAIAgent.Configuration;
using MyAIAgent.Endpoints;
using MyAIAgent.Models;
using MyAIAgent.Services;
using MyAIAgent.Tools;
using MyAIAgent.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// =====================
// CONFIGURATION (typed options — no secrets in source)
// =====================
builder.Services.Configure<AlphaVantageOptions>(
    builder.Configuration.GetSection(AlphaVantageOptions.SectionName));
builder.Services.Configure<OllamaOptions>(
    builder.Configuration.GetSection(OllamaOptions.SectionName));
builder.Services.Configure<TradingOptions>(
    builder.Configuration.GetSection(TradingOptions.SectionName));

// =====================
// DATABASE
// =====================
var connectionString = builder.Configuration.GetConnectionString("AppDb")
    ?? "Data Source=app.db";
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(connectionString));

// =====================
// HTTP CLIENTS (pooled via IHttpClientFactory — no socket exhaustion)
// =====================
builder.Services.AddHttpClient("alphavantage", c =>
    c.Timeout = TimeSpan.FromSeconds(30));

builder.Services.AddHttpClient("yahoo", c =>
{
    c.Timeout = TimeSpan.FromSeconds(20);
    c.DefaultRequestHeaders.UserAgent.ParseAdd(
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
        "AppleWebKit/537.36 (KHTML, like Gecko) " +
        "Chrome/124.0 Safari/537.36");
});

builder.Services.AddHttpClient("ollama", (sp, c) =>
{
    var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<OllamaOptions>>().Value;
    c.Timeout = TimeSpan.FromMinutes(opts.TimeoutMinutes);
});

// =====================
// SERVICES & TOOLS
// =====================
// Register each service once, then expose its interface as a forwarding
// registration so both the concrete type and the abstraction resolve to the
// same instance during this refactor.
builder.Services.AddScoped<HistoricalDataService>();
builder.Services.AddScoped<IHistoricalDataService>(sp => sp.GetRequiredService<HistoricalDataService>());

builder.Services.AddScoped<BacktestEngine>();
builder.Services.AddScoped<IBacktestEngine>(sp => sp.GetRequiredService<BacktestEngine>());

builder.Services.AddScoped<ResearchService>();
builder.Services.AddScoped<IResearchService>(sp => sp.GetRequiredService<ResearchService>());

builder.Services.AddScoped<IResearchQueryService, ResearchQueryService>();

builder.Services.AddScoped<VolatilityFactorService>();
builder.Services.AddScoped<IVolatilityFactorService>(sp => sp.GetRequiredService<VolatilityFactorService>());

builder.Services.AddScoped<ScreenerService>();
builder.Services.AddScoped<IScreenerService>(sp => sp.GetRequiredService<ScreenerService>());

builder.Services.AddScoped<PaperPortfolioService>();
builder.Services.AddScoped<IPaperPortfolioService>(sp => sp.GetRequiredService<PaperPortfolioService>());

builder.Services.AddScoped<AIService>();
builder.Services.AddScoped<IAiService>(sp => sp.GetRequiredService<AIService>());

builder.Services.AddScoped<ITool, NoteTool>();
builder.Services.AddScoped<ITool, StockTool>();
builder.Services.AddScoped<ITool, StockAnalysisTool>();
builder.Services.AddScoped<ITool, NewsTool>();
builder.Services.AddScoped<ITool, BacktestTool>();
builder.Services.AddScoped<ITool, StockResearchTool>();

// =====================
// MVC CONTROLLERS + SWAGGER + PROBLEM DETAILS
// =====================
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();

// =====================
// CORS
// =====================
builder.Services.AddCors(options =>
{
    options.AddPolicy("VueClient", policy =>
    {
        policy.SetIsOriginAllowed(origin =>
        {
            var uri = new Uri(origin);
            return uri.Host == "localhost" || uri.Host == "127.0.0.1";
        })
        .AllowAnyHeader()
        .AllowAnyMethod();
    });
});

var app = builder.Build();

// Turns unhandled exceptions into RFC 7807 ProblemDetails responses instead of
// each endpoint hand-rolling `Results.Problem(ex.Message)` and leaking internals.
app.UseExceptionHandler();
app.UseStatusCodePages();

// Auto-create tables on startup.
// NOTE: this uses EnsureCreated() which bypasses the EF migrations in /Migrations.
// Left as-is deliberately — switching to db.Database.Migrate() on a database that
// was created by EnsureCreated() needs a manual baseline and is out of scope here.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

app.UseCors("VueClient");
app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();

// =====================
// ENDPOINT GROUPS
// Each feature area is registered from its own file in /Endpoints so this
// file stays a composition root. /chat is still inline below pending a
// dedicated intent-router service.
// =====================
app.MapAccountEndpoints();
app.MapWatchlistEndpoints();
app.MapPortfolioEndpoints();
app.MapAlertEndpoints();
app.MapMarketDataEndpoints();
app.MapBacktestEndpoints();
app.MapResearchEndpoints();
app.MapScreenerEndpoints();
app.MapPaperPortfolioEndpoints();
app.MapConversationEndpoints();

app.MapChatEndpoints();

app.Run();
