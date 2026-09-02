using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MyAIAgent.Configuration;
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
// HEALTH CHECK
// =====================
app.MapGet("/health", () => new { status = "OK", time = DateTime.UtcNow });

// =====================
// REGISTER
// =====================
app.MapPost("/register", async (RegisterRequest request, AppDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Password))
        return Results.BadRequest("Username and password are required.");

    var existingUser = db.Users.FirstOrDefault(x => x.UserName == request.UserName);
    if (existingUser != null)
        return Results.BadRequest("Username already exists.");

    db.Users.Add(new User { UserName = request.UserName, Password = request.Password });
    await db.SaveChangesAsync();
    return Results.Ok("User registered successfully.");
});

// =====================
// LOGIN
// =====================
app.MapPost("/login", async (LoginRequest request, AppDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        return Results.BadRequest("Username and password are required.");

    var user = db.Users.FirstOrDefault(
        x => x.UserName == request.Username && x.Password == request.Password);

    if (user == null) return Results.Unauthorized();
    return Results.Ok(new { message = "Login successful.", userName = user.UserName });
});

// =====================
// WATCHLIST
// =====================
app.MapGet("/watchlist/{userName}", (string userName, AppDbContext db) =>
{
    var items = db.WatchlistItems
        .Where(x => x.UserName == userName)
        .OrderByDescending(x => x.AddedAt)
        .ToList();
    return Results.Ok(items);
});

app.MapPost("/watchlist", async (MyAIAgent.Models.Requests.AddWatchlistItemRequest request, AppDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Symbol))
        return Results.BadRequest("UserName and Symbol are required.");

    var symbol = request.Symbol.ToUpper();
    var exists = db.WatchlistItems.Any(x => x.UserName == request.UserName && x.Symbol == symbol);
    if (exists)
        return Results.BadRequest(symbol + " is already in your watchlist.");

    var item = new WatchlistItem
    {
        UserName = request.UserName,
        Symbol = symbol,
        Note = request.Note ?? string.Empty,
        AddedAt = DateTime.UtcNow
    };
    db.WatchlistItems.Add(item);
    await db.SaveChangesAsync();
    return Results.Ok(new { message = item.Symbol + " added to watchlist.", item });
});

app.MapDelete("/watchlist/{id}", async (int id, AppDbContext db) =>
{
    var item = db.WatchlistItems.FirstOrDefault(x => x.Id == id);
    if (item == null) return Results.NotFound("Item not found.");
    db.WatchlistItems.Remove(item);
    await db.SaveChangesAsync();
    return Results.Ok(new { message = item.Symbol + " removed from watchlist." });
});

// =====================
// PORTFOLIO
// =====================
app.MapGet("/portfolio/{userName}", (string userName, AppDbContext db) =>
{
    var items = db.PortfolioItems
        .Where(x => x.UserName == userName)
        .OrderByDescending(x => x.BoughtAt)
        .ToList();
    return Results.Ok(items);
});

app.MapPost("/portfolio", async (MyAIAgent.Models.Requests.AddPortfolioItemRequest request, AppDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Symbol))
        return Results.BadRequest("UserName and Symbol are required.");
    if (request.Shares <= 0) return Results.BadRequest("Shares must be greater than 0.");
    if (request.BuyPrice <= 0) return Results.BadRequest("Buy price must be greater than 0.");

    var item = new PortfolioItem
    {
        UserName = request.UserName,
        Symbol = request.Symbol.ToUpper(),
        Shares = request.Shares,
        BuyPrice = request.BuyPrice,
        Note = request.Note ?? string.Empty,
        BoughtAt = DateTime.UtcNow
    };
    db.PortfolioItems.Add(item);
    await db.SaveChangesAsync();
    return Results.Ok(new { message = item.Symbol + " added to portfolio.", item });
});

app.MapDelete("/portfolio/{id}", async (int id, AppDbContext db) =>
{
    var item = db.PortfolioItems.FirstOrDefault(x => x.Id == id);
    if (item == null) return Results.NotFound("Item not found.");
    db.PortfolioItems.Remove(item);
    await db.SaveChangesAsync();
    return Results.Ok(new { message = item.Symbol + " removed from portfolio." });
});

// =====================
// PRICE ALERTS — GET all alerts for a user
// =====================
app.MapGet("/alerts/{userName}", (string userName, AppDbContext db) =>
{
    var alerts = db.PriceAlerts
        .Where(x => x.UserName == userName)
        .OrderByDescending(x => x.CreatedAt)
        .ToList();
    return Results.Ok(alerts);
});

// =====================
// PRICE ALERTS — CREATE a new alert
// =====================
app.MapPost("/alerts", async (MyAIAgent.Models.Requests.CreatePriceAlertRequest request, AppDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Symbol))
        return Results.BadRequest("UserName and Symbol are required.");

    if (request.TargetPrice <= 0)
        return Results.BadRequest("Target price must be greater than 0.");

    if (request.Direction != "above" && request.Direction != "below")
        return Results.BadRequest("Direction must be 'above' or 'below'.");

    var alert = new PriceAlert
    {
        UserName = request.UserName,
        Symbol = request.Symbol.ToUpper(),
        TargetPrice = request.TargetPrice,
        Direction = request.Direction,
        CreatedAt = DateTime.UtcNow,
        IsTriggered = false
    };

    db.PriceAlerts.Add(alert);
    await db.SaveChangesAsync();

    return Results.Ok(new { message = "Alert created for " + alert.Symbol, alert });
});

// =====================
// PRICE ALERTS — DELETE an alert
// =====================
app.MapDelete("/alerts/{id}", async (int id, AppDbContext db) =>
{
    var alert = db.PriceAlerts.FirstOrDefault(x => x.Id == id);
    if (alert == null) return Results.NotFound("Alert not found.");
    db.PriceAlerts.Remove(alert);
    await db.SaveChangesAsync();
    return Results.Ok(new { message = "Alert removed." });
});

// =====================
// PRICE ALERTS — CHECK all alerts against live prices
// Call this from frontend periodically (e.g. every 60s)
// =====================
app.MapPost("/alerts/check/{userName}", async (string userName, AppDbContext db, IEnumerable<ITool> tools) =>
{
    var stockTool = tools.FirstOrDefault(t => t.Name == "GetStockPrice");
    if (stockTool == null) return Results.Problem("Stock tool not available.");

    var activeAlerts = db.PriceAlerts
        .Where(x => x.UserName == userName && !x.IsTriggered)
        .ToList();

    var newlyTriggered = new List<PriceAlert>();

    // Group by symbol to avoid duplicate API calls for the same stock
    var symbolGroups = activeAlerts.GroupBy(a => a.Symbol);

    foreach (var group in symbolGroups)
    {
        var symbol = group.Key;
        var raw = await stockTool.ExecuteAsync(symbol);

        // Extract price from formatted string "💰 Price: $291.13"
        var match = System.Text.RegularExpressions.Regex.Match(raw, @"Price:\s*\$?([\d.]+)");
        if (!match.Success) continue;

        var currentPrice = decimal.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);

        foreach (var alert in group)
        {
            bool shouldTrigger =
                (alert.Direction == "above" && currentPrice >= alert.TargetPrice) ||
                (alert.Direction == "below" && currentPrice <= alert.TargetPrice);

            if (shouldTrigger)
            {
                alert.IsTriggered = true;
                alert.TriggeredPrice = currentPrice;
                alert.TriggeredAt = DateTime.UtcNow;
                newlyTriggered.Add(alert);
            }
        }

        // Small delay between API calls to respect rate limits
        await Task.Delay(1200);
    }

    if (newlyTriggered.Count > 0)
    {
        await db.SaveChangesAsync();
    }

    return Results.Ok(new
    {
        alertsChecked = activeAlerts.Count,
        triggered = newlyTriggered
    });
});
//====================================
//QUCIK DECISION ON A STOCK IN A TABLE
//====================================
app.MapGet("/decision/{symbol}", async (string symbol, IEnumerable<ITool> tools) =>
{
    var analysisTool = tools.FirstOrDefault(t => t.Name == "AnalyzeStock") as StockAnalysisTool;
    if (analysisTool == null) return Results.Problem("Analysis tool not available.");

    var table = await analysisTool.BuildDecisionTableAsync(symbol.Trim().ToUpper());
    return Results.Ok(table);
});

//============================
// QUICK NEWS HEADLINES
//============================
app.MapGet("/news/{symbol}", async (string symbol, IEnumerable<ITool> tools) =>
{
    var newsTool = tools.FirstOrDefault(t => t.Name == "GetStockNews");
    if (newsTool == null) return Results.Problem("News tool not available.");
    var result = await newsTool.ExecuteAsync(symbol);
    return Results.Ok(new { symbol = symbol.ToUpper(), result });
});

// ========================
// HISTORICAL BACKTEST ENGINE (Stooq-free, no 25/day cap)
// backtestEngine + historicalData + researchService are now resolved per-request
// from DI (see the IBacktestEngine / IHistoricalDataService / IResearchService
// parameters on each endpoint below) instead of a shared hand-built instance.
// ========================

// GET /backtest/large
// Runs all 60 symbols across 10 sectors — no trend filter.
app.MapGet("/backtest/large", async (IBacktestEngine backtestEngine) =>
{
    var summary = await backtestEngine.RunBatchAsync(StockUniverse.All);
    return Results.Text(backtestEngine.FormatReport(summary), "text/plain");
});

// GET /backtest/large-filtered
// Same 60 symbols with 200-day MA trend filter (Experiment B).
// Skips RSI<30 entries when price is above the 200-day MA.
// Compare output against /backtest/large to measure the filter's effect.
app.MapGet("/backtest/large-filtered", async (IBacktestEngine backtestEngine) =>
{
    var summary = await backtestEngine.RunBatchAsync(StockUniverse.All, useTrendFilter: true);
    return Results.Text(backtestEngine.FormatReport(summary), "text/plain");
});

// ========================
// MULTI-PERIOD VALIDATION ENDPOINTS
// Run the same 59 stocks on a custom date range (e.g. 2006–2016)
// to check if the Airlines/Energy pattern holds in a different market regime.
// ========================

// GET /backtest/period/{fromYear}/{toYear}
// Example: GET http://localhost:60363/backtest/period/2006/2016
app.MapGet("/backtest/period/{fromYear}/{toYear}", async (int fromYear, int toYear, IBacktestEngine backtestEngine) =>
{
    var from = new DateTime(fromYear, 1, 1);
    var to = new DateTime(toYear, 12, 31);
    var summary = await backtestEngine.RunBatchRangeAsync(StockUniverse.All, from, to);
    return Results.Text(backtestEngine.FormatReport(summary), "text/plain");
});

// GET /backtest/period/{fromYear}/{toYear}/sector/{sectorName}
// Example: GET http://localhost:60363/backtest/period/2006/2016/sector/airlines
app.MapGet("/backtest/period/{fromYear}/{toYear}/sector/{sectorName}",
    async (int fromYear, int toYear, string sectorName, IBacktestEngine backtestEngine) =>
    {
        if (!StockUniverse.BySector.TryGetValue(sectorName.ToLower(), out var symbols))
            return Results.Text(
                $"Unknown sector '{sectorName}'. Available: {string.Join(", ", StockUniverse.BySector.Keys)}",
                "text/plain");

        var from = new DateTime(fromYear, 1, 1);
        var to = new DateTime(toYear, 12, 31);
        var summary = await backtestEngine.RunBatchRangeAsync(symbols, from, to);
        return Results.Text(backtestEngine.FormatReport(summary), "text/plain");
    });

// GET /backtest/sector-v2/{sectorName}
// Available sectors: tech, banks, auto, pharma, energy, retail, utilities, reits, airlines, industrial
// Append ?filtered=true to run with the 200-day MA trend filter.
app.MapGet("/backtest/sector-v2/{sectorName}", async (string sectorName, IBacktestEngine backtestEngine, bool filtered = false) =>
{
    if (!StockUniverse.BySector.TryGetValue(sectorName.ToLower(), out var symbols))
        return Results.Text(
            $"Unknown sector '{sectorName}'. Available: {string.Join(", ", StockUniverse.BySector.Keys)}",
            "text/plain");

    var summary = await backtestEngine.RunBatchAsync(symbols, useTrendFilter: filtered);
    return Results.Text(backtestEngine.FormatReport(summary), "text/plain");
});

// ========================
// LEGACY BACKTEST (Alpha Vantage, ~100 days, 25 req/day cap)
// Keep these for the AI chat tools — they still use Alpha Vantage for live signals.
// ========================
app.MapGet("/backtest/{symbol}", async (string symbol, IEnumerable<ITool> tools) =>
{
    var backtestTool = tools.FirstOrDefault(t => t.Name == "BacktestStrategy");
    if (backtestTool == null) return Results.Problem("Backtest tool not available.");
    return Results.Ok(new { symbol = symbol.ToUpper(), result = await backtestTool.ExecuteAsync(symbol) });
});

app.MapGet("/backtest/sector/{sectorName}", async (string sectorName, IEnumerable<ITool> tools) =>
{
    var backtestTool = tools.FirstOrDefault(t => t.Name == "BacktestStrategy") as BacktestTool;
    if (backtestTool == null) return Results.Problem("Backtest tool not available.");
    return Results.Ok(new { sector = sectorName, result = await backtestTool.ExecuteSectorAsync(sectorName) });
});

// ========================
// RESEARCH PLATFORM ENDPOINTS
// researchService is resolved per-request via the IResearchService parameter.
// ========================

// GET /research/{symbol}
// Plain-text strategy comparison report for a single symbol.
// Example: GET http://localhost:60363/research/AAPL
// Example: GET http://localhost:60363/research/XOM
app.MapGet("/research/{symbol}", async (string symbol, IResearchService researchService) =>
{
    var strategies = new List<MyAIAgent.Services.IStrategy>
    {
        new MyAIAgent.Services.RsiStrategy(30, 70),
        new MyAIAgent.Services.RsiStrategy(30, 70, trendFilter: true)
    };

    var report = await researchService.RunResearchAsync(symbol, strategies);
    return Results.Text(researchService.FormatReport(report), "text/plain");
});

// GET /research/{symbol}/explain
// Runs the same research as /research/{symbol} and pipes the structured
// data into AIService.InterpretResearch, returning a plain-English explanation.
// Called by the "Explain these results" button in ResearchPanel.
// Does NOT touch conversation history -- no sidebar pollution.
app.MapGet("/research/{symbol}/explain", async (string symbol, IAiService ai, IResearchService researchService, IEnumerable<ITool> tools) =>
{
    var strategies = new List<MyAIAgent.Services.IStrategy>
    {
        new MyAIAgent.Services.RsiStrategy(30, 70),
        new MyAIAgent.Services.RsiStrategy(30, 70, trendFilter: true)
    };

    var report = await researchService.RunResearchAsync(symbol, strategies);

    if (!string.IsNullOrEmpty(report.Error))
        return Results.Json(new { error = report.Error });

    var researchTool = tools.FirstOrDefault(t => t.Name == "ResearchStock") as MyAIAgent.Tools.StockResearchTool;
    var prompt = researchTool != null
        ? await researchTool.ExecuteAsync(symbol)
        : researchService.FormatForAI(report);

    var explanation = await ai.InterpretResearch(prompt);
    return Results.Json(new { symbol = symbol.ToUpper(), explanation });
});

// GET /research/batch/{sector}
// Research report for a full sector â all symbols side by side.
// Example: GET http://localhost:60363/research/batch/energy
app.MapGet("/research/batch/{sector}", async (string sector, IResearchService researchService) =>
{
    if (!MyAIAgent.Services.StockUniverse.BySector.TryGetValue(sector.ToLower(), out var symbols))
        return Results.Text(
            $"Unknown sector '{sector}'. Available: {string.Join(", ", MyAIAgent.Services.StockUniverse.BySector.Keys)}",
            "text/plain");

    var strategies = new List<MyAIAgent.Services.IStrategy>
    {
        new MyAIAgent.Services.RsiStrategy(30, 70),
        new MyAIAgent.Services.RsiStrategy(30, 70, trendFilter: true)
    };

    var sb = new System.Text.StringBuilder();
    foreach (var symbol in symbols)
    {
        var report = await researchService.RunResearchAsync(symbol, strategies);
        sb.AppendLine(researchService.FormatReport(report));
        sb.AppendLine(new string('─', 60));
    }

    return Results.Text(sb.ToString(), "text/plain");
});
// ========================
// SECTOR RESEARCH ENDPOINTS
// Add to Program.cs alongside the existing /research endpoints.
// Uses the same researchService and historicalData already declared above.
// ========================

// GET /research/sector/{sectorName}
// Aggregates research results for all stocks in a sector.
// Returns a structured JSON summary — consumed by SectorResearchPanel.vue.
// Example: GET http://localhost:60363/research/sector/energy
app.MapGet("/research/sector/{sectorName}", async (string sectorName, IResearchService researchService) =>
{
    if (!MyAIAgent.Services.StockUniverse.BySector.TryGetValue(sectorName.ToLower(), out var symbols))
        return Results.Json(new { error = $"Unknown sector '{sectorName}'. Available: {string.Join(", ", MyAIAgent.Services.StockUniverse.BySector.Keys)}" });

    var strategies = new List<MyAIAgent.Services.IStrategy>
    {
        new MyAIAgent.Services.RsiStrategy(30, 70),
        new MyAIAgent.Services.RsiStrategy(30, 70, trendFilter: true)
    };

    var perSymbol = new List<object>();
    int beatCount = 0;
    var advantages = new List<decimal>();

    foreach (var symbol in symbols)
    {
        var report = await researchService.RunResearchAsync(symbol, strategies);
        if (!string.IsNullOrEmpty(report.Error)) continue;

        var baseline = report.Results.FirstOrDefault(r => r.Verdict == "Baseline");
        var best = report.Results
            .Where(r => r.Verdict != "Baseline")
            .OrderByDescending(r => r.TotalReturnPercent)
            .FirstOrDefault();

        if (baseline == null || best == null) continue;

        decimal advantage = Math.Round(best.TotalReturnPercent - baseline.TotalReturnPercent, 2);
        bool beat = best.TotalReturnPercent > baseline.TotalReturnPercent;
        if (beat) beatCount++;
        advantages.Add(advantage);

        perSymbol.Add(new
        {
            symbol,
            bahReturn = Math.Round(baseline.TotalReturnPercent, 2),
            bestStrategy = best.StrategyName,
            stratReturn = Math.Round(best.TotalReturnPercent, 2),
            advantage,
            beat,
            trades = best.TotalTrades,
            winRate = best.WinRate,
            maxDrawdown = best.MaxDrawdownPercent
        });
    }

    decimal medianAdvantage = MyAIAgent.Common.Stats.Median(advantages);

    return Results.Json(new
    {
        sector = sectorName,
        symbolsTested = perSymbol.Count,
        beatCount,
        medianAdvantage,
        verdict = medianAdvantage >= 0 && beatCount >= perSymbol.Count / 2
                            ? "Outperformed Benchmark"
                            : "Underperformed Benchmark",
        perSymbol
    });
});

// GET /research/all-sectors
// Runs every sector and returns an aggregated summary table.
// Used by SectorResearchPanel.vue for the full market overview.
// Takes ~3-4 minutes for all 10 sectors × 6 stocks. Run once, cache mentally.
app.MapGet("/research/all-sectors", async (IResearchService researchService) =>
{
    var strategies = new List<MyAIAgent.Services.IStrategy>
    {
        new MyAIAgent.Services.RsiStrategy(30, 70),
        new MyAIAgent.Services.RsiStrategy(30, 70, trendFilter: true)
    };

    var sectorSummaries = new List<object>();

    foreach (var (sector, symbols) in MyAIAgent.Services.StockUniverse.BySector)
    {
        int beatCount = 0;
        var advantages = new List<decimal>();
        string bestSymbol = "", worstSymbol = "";
        decimal bestAdv = decimal.MinValue, worstAdv = decimal.MaxValue;

        foreach (var symbol in symbols)
        {
            var report = await researchService.RunResearchAsync(symbol, strategies);
            if (!string.IsNullOrEmpty(report.Error)) continue;

            var baseline = report.Results.FirstOrDefault(r => r.Verdict == "Baseline");
            var best = report.Results
                .Where(r => r.Verdict != "Baseline")
                .OrderByDescending(r => r.TotalReturnPercent)
                .FirstOrDefault();

            if (baseline == null || best == null) continue;

            decimal adv = Math.Round(best.TotalReturnPercent - baseline.TotalReturnPercent, 2);
            if (best.TotalReturnPercent > baseline.TotalReturnPercent) beatCount++;
            advantages.Add(adv);

            if (adv > bestAdv) { bestAdv = adv; bestSymbol = symbol; }
            if (adv < worstAdv) { worstAdv = adv; worstSymbol = symbol; }
        }

        decimal median = MyAIAgent.Common.Stats.Median(advantages);

        sectorSummaries.Add(new
        {
            sector,
            symbolsTested = advantages.Count,
            beatCount,
            medianAdvantage = median,
            verdict = median >= 0 && beatCount >= advantages.Count / 2
                                ? "Outperformed"
                                : "Underperformed",
            bestSymbol,
            bestAdvantage = bestAdv == decimal.MinValue ? 0 : bestAdv,
            worstSymbol,
            worstAdvantage = worstAdv == decimal.MaxValue ? 0 : worstAdv
        });
    }

    return Results.Json(new
    {
        sectorsRun = sectorSummaries.Count,
        generatedAt = DateTime.UtcNow,
        sectors = sectorSummaries.OrderByDescending(s => ((dynamic)s).medianAdvantage)
    });
});
// ========================
// FACTOR RESEARCH — TREND STRENGTH
// Tests whether buy-and-hold return (proxy for trend strength)
// predicts RSI strategy success.
// Hypothesis: RSI works better on weak-trend stocks than strong-trend stocks.
// GET /research/factor/trend-strength
// ========================
app.MapGet("/research/factor/trend-strength", async (IResearchService researchService) =>
{
    var strategies = new List<MyAIAgent.Services.IStrategy>
    {
        new MyAIAgent.Services.RsiStrategy(30, 70)
    };

    var perStock = new List<object>();

    foreach (var symbol in MyAIAgent.Services.StockUniverse.All)
    {
        var report = await researchService.RunResearchAsync(symbol, strategies);
        if (!string.IsNullOrEmpty(report.Error)) continue;

        var baseline = report.Results.FirstOrDefault(r => r.Verdict == "Baseline");
        var rsi = report.Results.FirstOrDefault(r => r.Verdict != "Baseline");
        if (baseline == null || rsi == null) continue;

        decimal bahReturn = baseline.TotalReturnPercent;
        decimal rsiReturn = rsi.TotalReturnPercent;
        decimal advantage = Math.Round(rsiReturn - bahReturn, 2);
        bool beat = rsiReturn > bahReturn;

        // Trend bucket based on buy-and-hold return over the 10y period
        string trendBucket = MyAIAgent.Common.TrendBucket.For(bahReturn);

        perStock.Add(new
        {
            symbol,
            bahReturn = Math.Round(bahReturn, 1),
            rsiReturn = Math.Round(rsiReturn, 1),
            advantage,
            beat,
            trendBucket,
            trades = rsi.TotalTrades,
            winRate = rsi.WinRate
        });
    }

    // Group into buckets and compute stats
    var buckets = new[]
    {
        MyAIAgent.Common.TrendBucket.Weak,
        MyAIAgent.Common.TrendBucket.Medium,
        MyAIAgent.Common.TrendBucket.Strong
    };
    var bucketStats = buckets.Select(bucket =>
    {
        var stocks = perStock.Cast<dynamic>().Where(s => s.trendBucket == bucket).ToList();
        if (!stocks.Any()) return null;

        int total = stocks.Count;
        int beatCount = stocks.Count(s => (bool)s.beat);
        decimal median = MyAIAgent.Common.Stats.Median(
            ((IEnumerable<dynamic>)stocks).Select(s => (decimal)s.advantage), round: 1);

        return (object)new
        {
            bucket,
            total,
            beatCount,
            beatRate = Math.Round((decimal)beatCount / total * 100, 1),
            medianAdvantage = median
        };
    }).Where(b => b != null).ToList();

    return Results.Json(new
    {
        hypothesis = "RSI mean-reversion works better on weak-trend stocks than strong-trend stocks",
        totalStocks = perStock.Count,
        generatedAt = DateTime.UtcNow,
        buckets = bucketStats,
        perStock = perStock.Cast<dynamic>()
                        .OrderBy(s => (decimal)s.bahReturn)
                        .ToList()
    });
});
// ========================
// FACTOR RESEARCH — TREND STRENGTH (DATE RANGE)
// Same as /research/factor/trend-strength but for a custom period.
// Used for multi-period validation of the trend strength factor.
// GET /research/factor/trend-strength/{fromYear}/{toYear}
// Example: GET /research/factor/trend-strength/2006/2016
// ========================
app.MapGet("/research/factor/trend-strength/{fromYear}/{toYear}",
    async (int fromYear, int toYear, IHistoricalDataService historicalData) =>
    {
        var from = new DateTime(fromYear, 1, 1);
        var to = new DateTime(toYear, 12, 31);

        var strategies = new List<MyAIAgent.Services.IStrategy>
    {
        new MyAIAgent.Services.RsiStrategy(30, 70)
    };

        var perStock = new List<object>();

        foreach (var symbol in MyAIAgent.Services.StockUniverse.All)
        {
            List<MyAIAgent.Services.DailyBar> bars;
            try { bars = await historicalData.GetDailyHistoryRangeAsync(symbol, from, to); }
            catch { continue; }

            if (bars.Count < 60) continue;

            // Run buy-and-hold manually on the range bars
            var firstPrice = bars.First().Close;
            var lastPrice = bars.Last().Close;
            decimal bahReturn = Math.Round(((lastPrice - firstPrice) / firstPrice) * 100, 2);

            // Run RSI strategy on range bars
            var rsiStrategy = new MyAIAgent.Services.RsiStrategy(30, 70);
            var trades = rsiStrategy.Run(bars);

            decimal rsiReturn = MyAIAgent.Common.EquityCurve
                .Compound(trades.Select(t => t.ReturnPercent)).TotalReturnPercent;
            decimal advantage = Math.Round(rsiReturn - bahReturn, 2);
            bool beat = rsiReturn > bahReturn;

            string trendBucket = MyAIAgent.Common.TrendBucket.For(bahReturn);

            perStock.Add(new
            {
                symbol,
                bahReturn,
                rsiReturn,
                advantage,
                beat,
                trendBucket,
                trades = trades.Count,
                winRate = trades.Count > 0
                    ? Math.Round((decimal)trades.Count(t => t.ReturnPercent > 0) / trades.Count * 100, 1)
                    : 0
            });
        }

        var buckets = new[]
        {
            MyAIAgent.Common.TrendBucket.Weak,
            MyAIAgent.Common.TrendBucket.Medium,
            MyAIAgent.Common.TrendBucket.Strong
        };
        var bucketStats = buckets.Select(bucket =>
        {
            var stocks = perStock.Cast<dynamic>().Where(s => s.trendBucket == bucket).ToList();
            if (!stocks.Any()) return null;

            int total = stocks.Count;
            int beatCount = stocks.Count(s => (bool)s.beat);
            decimal median = MyAIAgent.Common.Stats.Median(
                ((IEnumerable<dynamic>)stocks).Select(s => (decimal)s.advantage), round: 1);

            return (object)new
            {
                bucket,
                total,
                beatCount,
                beatRate = Math.Round((decimal)beatCount / total * 100, 1),
                medianAdvantage = median
            };
        }).Where(b => b != null).ToList();

        return Results.Json(new
        {
            hypothesis = "RSI mean-reversion works better on weak-trend stocks than strong-trend stocks",
            period = $"{fromYear}–{toYear}",
            totalStocks = perStock.Count,
            generatedAt = DateTime.UtcNow,
            buckets = bucketStats,
            perStock = perStock.Cast<dynamic>()
                            .OrderBy(s => (decimal)s.bahReturn)
                            .ToList()
        });
    });

// Register StockResearchTool so the AI chat can call it too.
// Add "new StockResearchTool(researchService)" to wherever you build your
// tools list in Program.cs — same pattern as StockTool and StockAnalysisTool.

// ========================
// QUICK STOCK PRICE
// ========================
app.MapGet("/stock/{symbol}", async (string symbol, IEnumerable<ITool> tools) =>
{
    var stockTool = tools.FirstOrDefault(t => t.Name == "GetStockPrice");
    if (stockTool == null) return Results.Problem("Stock tool not available.");
    return Results.Ok(new { symbol = symbol.ToUpper(), result = await stockTool.ExecuteAsync(symbol) });
});
// =====================
// DEEP STOCK ANALYSIS
// =====================
app.MapPost("/analyze", async (AnalyzeRequest request, IAiService ai, IEnumerable<ITool> tools) =>
{
    if (string.IsNullOrWhiteSpace(request.Symbols))
        return Results.BadRequest("Symbols are required.");

    var analysisTool = tools.FirstOrDefault(t => t.Name == "AnalyzeStock");
    if (analysisTool == null) return Results.Problem("Analysis tool not available.");

    var stockData = await analysisTool.ExecuteAsync(request.Symbols);
    var userQuestion = string.IsNullOrWhiteSpace(request.Question)
        ? "Analyze these stocks and tell me which looks strongest right now."
        : request.Question;

    try
    {
        var analysis = await ai.AnalyzeStocks(stockData, userQuestion);
        return Results.Ok(new { symbols = request.Symbols.ToUpper(), rawData = stockData, analysis });
    }
    catch (Exception ex)
    {
        return Results.Problem("Analysis error: " + ex.Message);
    }
});

// =====================
// CHAT
// =====================
app.MapPost("/chat", async (ChatRequestV2 request, IAiService ai, IEnumerable<ITool> tools) =>
{
    if (string.IsNullOrWhiteSpace(request.Message))
        return Results.BadRequest("Message cannot be empty.");

    if (string.IsNullOrWhiteSpace(request.ConversationId))
        return Results.BadRequest("ConversationId is required.");

    var messageLower = request.Message.ToLower();

    bool isAnalysisQuery =
        messageLower.Contains("analyze") ||
        messageLower.Contains("analyse") ||
        messageLower.Contains("compare") ||
        messageLower.Contains("recommend") ||
        messageLower.Contains("should i buy") ||
        messageLower.Contains("should i sell") ||
        messageLower.Contains("which is better") ||
        messageLower.Contains("vs") ||
        messageLower.Contains("versus");

    bool isStockQuery =
        messageLower.Contains("price of") ||
        messageLower.Contains("stock price") ||
        messageLower.Contains("how much is");

    bool isNoteQuery =
        messageLower.Contains("remember") ||
        messageLower.Contains("save") ||
        messageLower.Contains("note");

    bool isNewsQuery =
    messageLower.Contains("news") ||
    messageLower.Contains("headlines") ||
    messageLower.Contains("latest on");

    if (isAnalysisQuery)
    {
        // Fast-path: extract symbols directly — bypasses slow DecideTool Ollama call
        var actionMatch = System.Text.RegularExpressions.Regex.Match(
            request.Message,
            @"(?:analyze|analyse|compare|recommend)\s+((?:[A-Z]{1,5}[,\s]*)+?)(?:\s+(?:and|vs|versus|against|with|give|for|please)|$)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        string extractedInput = "";
        if (actionMatch.Success)
        {
            extractedInput = string.Join(",",
                actionMatch.Groups[1].Value
                    .Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                    .Where(w => w.Length >= 1 && w.Length <= 5 && w.All(char.IsLetter))
                    .Select(w => w.ToUpper()));
        }

        if (!string.IsNullOrWhiteSpace(extractedInput))
        {
            var analysisTool = tools.FirstOrDefault(t => t.Name == "AnalyzeStock");
            if (analysisTool != null)
            {
                var stockData = await analysisTool.ExecuteAsync(extractedInput);
                var analysis = await ai.AnalyzeStocks(stockData, request.Message);
                await ai.SaveToolMessage(request.Message, analysis, request.ConversationId, request.UserName);
                return Results.Ok(new
                {
                    toolUsed = true,
                    tool = "AnalyzeStock",
                    result = analysis,
                    conversationId = request.ConversationId
                });
            }
        }

        // Fallback: use DecideTool for complex queries where regex didn't find a symbol
        var toolDecision = await ai.DecideTool(request.Message);
        if (toolDecision.UseTool && toolDecision.ToolName == "AnalyzeStock")
        {
            var analysisTool = tools.FirstOrDefault(t => t.Name == "AnalyzeStock");
            if (analysisTool != null)
            {
                var stockData = await analysisTool.ExecuteAsync(toolDecision.ToolInput);
                var analysis = await ai.AnalyzeStocks(stockData, request.Message);
                await ai.SaveToolMessage(request.Message, analysis, request.ConversationId, request.UserName);
                return Results.Ok(new
                {
                    toolUsed = true,
                    tool = "AnalyzeStock",
                    result = analysis,
                    conversationId = request.ConversationId
                });
            }
        }
    }

    if (isStockQuery)
    {
        var toolDecision = await ai.DecideTool(request.Message);
        if (toolDecision.UseTool)
        {
            var tool = tools.FirstOrDefault(t => t.Name == toolDecision.ToolName);
            if (tool != null)
            {
                var result = await tool.ExecuteAsync(toolDecision.ToolInput);

                await ai.SaveToolMessage(request.Message, result, request.ConversationId, request.UserName);

                return Results.Ok(new
                {
                    toolUsed = true,
                    tool = tool.Name,
                    result = result,
                    conversationId = request.ConversationId
                });
            }
        }
    }

    if (isNoteQuery)
    {
        var toolDecision = await ai.DecideTool(request.Message);
        if (toolDecision.UseTool)
        {
            var tool = tools.FirstOrDefault(t => t.Name == toolDecision.ToolName);
            if (tool != null)
            {
                var result = await tool.ExecuteAsync(toolDecision.ToolInput);

                await ai.SaveToolMessage(request.Message, result, request.ConversationId, request.UserName);

                return Results.Ok(new
                {
                    toolUsed = true,
                    tool = tool.Name,
                    result = result,
                    conversationId = request.ConversationId
                });
            }
        }
    }
    if (isNewsQuery)
    {
        var toolDecision = await ai.DecideTool(request.Message);
        if (toolDecision.UseTool && toolDecision.ToolName == "GetStockNews")
        {
            var tool = tools.FirstOrDefault(t => t.Name == "GetStockNews");
            if (tool != null)
            {
                var result = await tool.ExecuteAsync(toolDecision.ToolInput);

                await ai.SaveToolMessage(request.Message, result, request.ConversationId, request.UserName);

                return Results.Ok(new
                {
                    toolUsed = true,
                    tool = tool.Name,
                    result = result,
                    conversationId = request.ConversationId
                });
            }
        }
    }
    // ── ADD THIS BLOCK in Program.cs, inside the /chat endpoint ──────────────
    // Place it just before the final try { var reply = await ai.AskAI(...) } block.

    bool isResearchQuery =
        messageLower.Contains("research") ||
        messageLower.Contains("backtest") ||
        messageLower.Contains("historical") ||
        messageLower.Contains("how did") ||
        messageLower.Contains("strategy") ||
        messageLower.Contains("how has") ||
        messageLower.Contains("performance");

    if (isResearchQuery)
    {
        // Extract symbol directly — don't trust phi3:mini to pick the right tool.
        // Require 2-5 uppercase chars (excludes single letters like I, A).
        // Skip known non-ticker uppercase words that appear in natural language.
        var nonTickerWords = new System.Collections.Generic.HashSet<string>
        {
            "AI", "US", "UK", "EU", "RSI", "SMA", "EMA", "EV", "ETF", "GDP",
            "CEO", "IPO", "PE", "EPS", "YOY", "QOQ", "MOM", "ATH", "ATL",
            "NYSE", "NASDAQ", "SP", "DOW", "FED", "SEC", "IRS", "ESG",
            "FAQ", "API", "URL", "JSON", "SQL", "CSS", "HTML", "UX"
        };

        // Walk matches until we find one that isn't a known non-ticker word.
        var symbolMatch = System.Text.RegularExpressions.Regex.Match(
            request.Message, @"\b([A-Z]{2,5})\b");
        while (symbolMatch.Success && nonTickerWords.Contains(symbolMatch.Groups[1].Value))
            symbolMatch = symbolMatch.NextMatch();

        if (symbolMatch.Success)
        {
            var symbol = symbolMatch.Groups[1].Value;
            var tool = tools.FirstOrDefault(t => t.Name == "ResearchStock");

            if (tool != null)
            {
                var researchPrompt = await tool.ExecuteAsync(symbol);
                var explanation = await ai.InterpretResearch(researchPrompt);

                await ai.SaveToolMessage(
                    request.Message, explanation,
                    request.ConversationId, request.UserName);

                return Results.Ok(new
                {
                    toolUsed = true,
                    tool = "ResearchStock",
                    result = explanation,
                    conversationId = request.ConversationId
                });
            }
        }
    }
    // ── PORTFOLIO QUERY — inject daily_agent.py report into context ─────────
    // Fires when the user asks about their open trades, RSI status, or P&L.
    // Uses trading_output.txt (written each weekday at 09:15 by daily_agent.py).
    bool isPortfolioQuery =
        messageLower.Contains("my trade") ||
        messageLower.Contains("my position") ||
        messageLower.Contains("my portfolio") ||
        messageLower.Contains("open position") ||
        messageLower.Contains("how am i doing") ||
        messageLower.Contains("how are my") ||
        messageLower.Contains("ibm") ||
        messageLower.Contains("intc") ||
        messageLower.Contains("exit trigger") ||
        messageLower.Contains("should i hold") ||
        messageLower.Contains("should i exit") ||
        messageLower.Contains("p&l") ||
        messageLower.Contains("my rsi");

    if (isPortfolioQuery)
    {
        try
        {
            var reply = await ai.AskAIWithPortfolioContext(request.Message, request.ConversationId, request.UserName);
            return Results.Ok(new
            {
                toolUsed = false,
                conversationId = request.ConversationId,
                message = reply
            });
        }
        catch (Exception ex)
        {
            return Results.Problem("AI service error: " + ex.Message);
        }
    }

    try
    {
        var reply = await ai.AskAI(request.Message, request.ConversationId, request.UserName);
        return Results.Ok(new
        {
            toolUsed = false,
            conversationId = request.ConversationId,
            message = reply
        });
    }
    catch (Exception ex)
    {
        return Results.Problem("AI service error: " + ex.Message);
    }
});

// =====================
// CONVERSATIONS — list all past conversations for a user
// =====================
app.MapGet("/conversations/{userName}", (string userName, AppDbContext db) =>
{
    var conversations = db.ChatMessages
        .Where(x => x.UserName == userName)
        .GroupBy(x => x.ConversationId)
        .Select(g => new
        {
            conversationId = g.Key,
            lastMessage = g.OrderByDescending(m => m.Id).Select(m => m.Content).FirstOrDefault(),
            lastUpdated = g.OrderByDescending(m => m.Id).Select(m => m.CreatedAt).FirstOrDefault(),
            messageCount = g.Count()
        })
        .OrderByDescending(c => c.lastUpdated)
        .ToList();

    return Results.Ok(conversations);
});

// =====================
// CONVERSATIONS — get all messages for a specific conversation
// =====================
app.MapGet("/conversations/{userName}/{conversationId}", (string userName, string conversationId, AppDbContext db) =>
{
    var messages = db.ChatMessages
        .Where(x => x.UserName == userName && x.ConversationId == conversationId)
        .OrderBy(x => x.Id)
        .ToList();

    return Results.Ok(messages);
});

// =====================
// CONVERSATIONS — delete a conversation
// =====================
app.MapDelete("/conversations/{userName}/{conversationId}", async (string userName, string conversationId, AppDbContext db) =>
{
    var messages = db.ChatMessages
        .Where(x => x.UserName == userName && x.ConversationId == conversationId)
        .ToList();

    db.ChatMessages.RemoveRange(messages);
    await db.SaveChangesAsync();

    return Results.Ok(new { message = "Conversation deleted." });
});
// ========================
// VOLATILITY FACTOR RESEARCH
// Now served by Controllers/VolatilityController.cs (routes unchanged:
// POST /api/volatility/run and POST /api/volatility/validate).
// ========================

// ========================
// RSI CANDIDATE SCREENER — v1
// Applies the validated Finding #1 exclusion rule:
//   Exclude stocks with >300% 10-year buy-and-hold return.
// Returns all remaining stocks with current RSI and trend bucket.
// Sorted by RSI ascending (oversold candidates first).
//
// V2 TODO: Add per-stock historical advantage column.
//          Offer as "Deep Analysis" button — ~2–3 min load on demand.
// ========================
app.MapGet("/api/screener/rsi-candidates", async (ScreenerService screener) =>
{
    var result = await screener.RunAsync(StockUniverse.All);
    return Results.Json(result);
});

// ═══════════════════════════════════════════════════════════════
// PASTE 3 — Program.cs ENDPOINTS section
// Add after the screener endpoint block
// ═══════════════════════════════════════════════════════════════

// ========================
// PAPER PORTFOLIO
// Track RSI screener picks without real money.
// Benchmark: B&H from entry date (fetched from Yahoo Finance on close).
// P&L on open positions: supplied by caller from last screener run.
// ========================

// GET /api/paper/{userName}
// Returns open + closed trades with P&L.
// Optional body: [{ "symbol": "SLB", "price": 42.10 }, ...] for live P&L on open positions.
// If no body, open positions show unrealized P&L as null.
app.MapGet("/api/paper/{userName}", async (string userName, PaperPortfolioService svc) =>
{
    var summary = await svc.GetSummaryAsync(userName);
    return Results.Json(summary);
});

// POST /api/paper/{userName}/prices
// Same as GET but accepts a price list in the body for open P&L calculation.
// Call this after running the screener — pass the screener candidate prices.
app.MapPost("/api/paper/{userName}/prices",
    async (string userName, List<PriceUpdate> prices, PaperPortfolioService svc) =>
    {
        var summary = await svc.GetSummaryAsync(userName, prices);
        return Results.Json(summary);
    });

// POST /api/paper/open
// Open a new paper trade.
// Body: { userName, symbol, sector, entryPrice, entryDate, rsiAtEntry, targetExitRsi }
app.MapPost("/api/paper/open", async (OpenTradeRequest req, PaperPortfolioService svc) =>
{
    if (string.IsNullOrWhiteSpace(req.UserName) || string.IsNullOrWhiteSpace(req.Symbol))
        return Results.BadRequest(new { error = "UserName and Symbol are required." });
    if (req.EntryPrice <= 0)
        return Results.BadRequest(new { error = "EntryPrice must be greater than 0." });
    if (req.RsiAtEntry < 0 || req.RsiAtEntry > 100)
        return Results.BadRequest(new { error = "RsiAtEntry must be between 0 and 100." });

    var trade = await svc.OpenTradeAsync(req);
    return Results.Ok(new { message = $"Paper trade opened for {req.Symbol.ToUpper()}.", trade });
});

// POST /api/paper/close
// Close an existing paper trade. Fetches B&H benchmark automatically.
// Body: { tradeId, userName, exitPrice, exitDate, rsiAtExit }
app.MapPost("/api/paper/close", async (CloseTradeRequest req, PaperPortfolioService svc) =>
{
    if (req.ExitPrice <= 0)
        return Results.BadRequest(new { error = "ExitPrice must be greater than 0." });

    var trade = await svc.CloseTradeAsync(req);
    if (trade == null)
        return Results.NotFound(new { error = "Trade not found, already closed, or not owned by this user." });

    return Results.Ok(new { message = $"{trade.Symbol} trade closed.", trade });
});

// DELETE /api/paper/{tradeId}/{userName}
// Delete an open trade (cancel before close). Closed trades cannot be deleted.
app.MapDelete("/api/paper/{tradeId}/{userName}", async (int tradeId, string userName, PaperPortfolioService svc) =>
{
    var deleted = await svc.DeleteTradeAsync(tradeId, userName);
    if (!deleted)
        return Results.NotFound(new { error = "Trade not found, already closed, or not owned by this user." });
    return Results.Ok(new { message = "Paper trade deleted." });
});
// ========================
// RSI LOOKUP — single symbol (any symbol, not just StockUniverse)
// Used by WatchlistPanel, PortfolioPanel and AlertsPanel to show live RSI + price.
// Uses Yahoo Finance (same as screener) — free, no daily cap.
// FIX: Now returns currentPrice (regularMarketPrice) so Portfolio P&L works.
// REPLACE the existing /api/screener/rsi/{symbol} endpoint in Program.cs
// ========================
app.MapGet("/api/screener/rsi/{symbol}", async (string symbol, IHistoricalDataService data, IHttpClientFactory httpClientFactory) =>
{
    var sym = symbol.ToUpper().Trim();

    try
    {
        var bars = await data.GetDailyHistoryAsync(sym);

        if (bars.Count < 20)
            return Results.Json(new
            {
                symbol = sym,
                currentRsi = (decimal?)null,
                currentPrice = (decimal?)null,
                trendBucket = (string?)null,
                bahReturn = (decimal?)null,
                passes = (bool?)null,
                error = $"Insufficient data ({bars.Count} bars)"
            });

        // 10-year B&H return
        var firstClose = bars.First().Close;
        var lastClose = bars.Last().Close;
        decimal bahReturn = Math.Round(((lastClose - firstClose) / firstClose) * 100, 1);

        // Trend bucket
        string trendBucket = MyAIAgent.Common.TrendBucket.For(bahReturn);

        // Current RSI from historical closes
        var closes = bars.Select(b => b.Close).ToList();
        var rsiSeries = TechnicalIndicators.CalculateRsiSeries(closes, period: 14);
        decimal? currentRsi = rsiSeries.LastOrDefault(r => r.HasValue);
        if (currentRsi.HasValue) currentRsi = Math.Round(currentRsi.Value, 1);

        // Finding #1 exclusion
        bool passes = bahReturn <= 300;

        // ── FIX: Fetch live market price from Yahoo Finance quote endpoint ──
        // The historical bars endpoint gives OHLC but not the live price.
        // The quote endpoint returns regularMarketPrice — the current price.
        decimal? currentPrice = null;
        try
        {
            var http = httpClientFactory.CreateClient("yahoo");
            var quoteUrl = $"https://query1.finance.yahoo.com/v8/finance/chart/{sym}?interval=1d&range=1d";
            var quoteRes = await http.GetAsync(quoteUrl);
            if (quoteRes.IsSuccessStatusCode)
            {
                var quoteJson = await quoteRes.Content.ReadAsStringAsync();
                // Parse regularMarketPrice from the meta object
                // Example path: result[0].meta.regularMarketPrice
                var metaMatch = System.Text.RegularExpressions.Regex.Match(
                    quoteJson, @"""regularMarketPrice""\s*:\s*([\d.]+)");
                if (metaMatch.Success &&
                    decimal.TryParse(metaMatch.Groups[1].Value,
                        System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out var price))
                {
                    currentPrice = Math.Round(price, 2);
                }
            }
        }
        catch
        {
            // currentPrice stays null — frontend shows — instead of crashing
        }

        return Results.Json(new
        {
            symbol,
            currentRsi,
            currentPrice,   // ← NEW: used by Portfolio and Watchlist for P&L
            trendBucket,
            bahReturn,
            passes,
            excludeReason = passes ? null : "Strong trend (>300% 10y return) — Finding #1 rule"
        });
    }
    catch (Exception ex)
    {
        return Results.Json(new
        {
            symbol = sym,
            currentRsi = (decimal?)null,
            currentPrice = (decimal?)null,
            trendBucket = (string?)null,
            bahReturn = (decimal?)null,
            passes = (bool?)null,
            error = ex.Message
        });
    }
});

app.Run();