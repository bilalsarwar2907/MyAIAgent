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

app.Run();
