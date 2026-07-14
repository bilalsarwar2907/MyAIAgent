using MyAIAgent.Services;
using System.Text.Json;

namespace MyAIAgent.Tools
{
    public class DecisionRow
    {
        public string Factor { get; set; } = "";
        public string Value { get; set; } = "";
        public string Rule { get; set; } = "";
        public string Result { get; set; } = "";
        public bool Passed { get; set; }
    }

    public class DecisionTable
    {
        public string Symbol { get; set; } = "";
        public List<DecisionRow> Rows { get; set; } = new();
        public int PassedCount { get; set; }
        public string VerdictAction { get; set; } = "";
        public string Error { get; set; } = "";
    }

    public class StockAnalysisResult
    {
        public string Symbol { get; set; } = "";
        public string Price { get; set; } = "N/A";
        public string Change { get; set; } = "N/A";
        public string ChangePercent { get; set; } = "N/A";
        public string High { get; set; } = "N/A";
        public string Low { get; set; } = "N/A";
        public string Volume { get; set; } = "N/A";
        public string RSI { get; set; } = "N/A";
        public string MovingAverage50 { get; set; } = "N/A";
        public string MovingAverage200 { get; set; } = "N/A";
        public string LatestTradingDay { get; set; } = "N/A";
        public string Error { get; set; } = "";
    }

    public class StockAnalysisTool : ITool
    {
        public string Name => "AnalyzeStock";

        // ✅ Replace with your Alpha Vantage API key
        private const string API_KEY = "RZRQ76MU2EMPJWJN";
        private const string BASE_URL = "https://www.alphavantage.co/query";

        private readonly HttpClient _httpClient;

        public StockAnalysisTool()
        {
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(20)
            };
        }

        /// <summary>
        /// Input: single symbol like "AAPL" or multiple like "AAPL,MSFT,GOOGL"
        /// </summary>
        public string Execute(string input)
        {
            return AnalyzeAsync(input.Trim().ToUpper()).GetAwaiter().GetResult();
        }

        private async Task<string> AnalyzeAsync(string input)
        {
            // Support multiple symbols separated by comma
            var symbols = input.Split(',')
                .Select(s => s.Trim().ToUpper())
                .Where(s => !string.IsNullOrEmpty(s))
                .Take(3) // Max 3 to stay within API limits
                .ToList();

            var results = new List<StockAnalysisResult>();

            foreach (var symbol in symbols)
            {
                var result = await FetchFullAnalysis(symbol);
                results.Add(result);

                // Small delay to avoid hitting API rate limits
                if (symbols.Count > 1)
                    await Task.Delay(1200);
            }

            // Build a rich text summary to send to the AI
            return BuildSummary(results);
        }

        private async Task<StockAnalysisResult> FetchFullAnalysis(string symbol)
        {
            var result = new StockAnalysisResult { Symbol = symbol };

            try
            {
                // ── 1. GLOBAL QUOTE (price, change, volume) ──────────────
                var quoteUrl = BASE_URL +
                    "?function=GLOBAL_QUOTE&symbol=" + symbol + "&apikey=" + API_KEY;

                var quoteJson = await _httpClient.GetStringAsync(quoteUrl);
                var quoteDoc = JsonDocument.Parse(quoteJson);

                // ✅ Check for rate limit BEFORE trying to read quote data
                if (quoteDoc.RootElement.TryGetProperty("Information", out var infoMsg))
                {
                    var msg = infoMsg.GetString() ?? "";
                    if (msg.Contains("rate limit") || msg.Contains("25 requests"))
                    {
                        result.Error = "Daily API limit reached (25 requests/day). Resets in 24 hours.";
                        return result;
                    }
                    result.Error = "API notice: " + msg;
                    return result;
                }

                if (quoteDoc.RootElement.TryGetProperty("Error Message", out _))
                {
                    result.Error = "Invalid symbol: " + symbol;
                    return result;
                }

                if (quoteDoc.RootElement.TryGetProperty("Global Quote", out var quote)
                    && quote.EnumerateObject().Count() > 0)
                {
                    result.Price = GetVal(quote, "05. price");
                    result.Change = GetVal(quote, "09. change");
                    result.ChangePercent = GetVal(quote, "10. change percent");
                    result.High = GetVal(quote, "03. high");
                    result.Low = GetVal(quote, "04. low");
                    result.Volume = GetVal(quote, "06. volume");
                    result.LatestTradingDay = GetVal(quote, "07. latest trading day");
                }
                else
                {
                    result.Error = "Symbol not found: " + symbol;
                    return result;
                }

                // Small delay between API calls
                await Task.Delay(1200);

                // ── 2. RSI (momentum indicator) ──────────────────────────
                var rsiUrl = BASE_URL +
                    "?function=RSI&symbol=" + symbol +
                    "&interval=daily&time_period=14&series_type=close&apikey=" + API_KEY;

                var rsiJson = await _httpClient.GetStringAsync(rsiUrl);
                var rsiDoc = JsonDocument.Parse(rsiJson);

                if (rsiDoc.RootElement.TryGetProperty("Technical Analysis: RSI", out var rsiData))
                {
                    // Get the most recent RSI value
                    var firstEntry = rsiData.EnumerateObject().FirstOrDefault();
                    if (firstEntry.Value.ValueKind != JsonValueKind.Undefined)
                    {
                        result.RSI = GetVal(firstEntry.Value, "RSI");
                    }
                }

                await Task.Delay(1200);

                // ── 3. SMA 50-day moving average ─────────────────────────
                var sma50Url = BASE_URL +
                    "?function=SMA&symbol=" + symbol +
                    "&interval=daily&time_period=50&series_type=close&apikey=" + API_KEY;

                var sma50Json = await _httpClient.GetStringAsync(sma50Url);
                var sma50Doc = JsonDocument.Parse(sma50Json);

                if (sma50Doc.RootElement.TryGetProperty("Technical Analysis: SMA", out var sma50Data))
                {
                    var firstEntry = sma50Data.EnumerateObject().FirstOrDefault();
                    if (firstEntry.Value.ValueKind != JsonValueKind.Undefined)
                    {
                        result.MovingAverage50 = GetVal(firstEntry.Value, "SMA");
                    }
                }
            }
            catch (Exception ex)
            {
                result.Error = "Error fetching data: " + ex.Message;
            }

            return result;
        }

        /// <summary>
        /// Builds a structured text summary of all stocks.
        /// This is what gets sent to the AI for analysis.
        /// </summary>
        /// <summary>
        /// Builds a structured, rule-based decision table for ONE symbol.
        /// Uses the same data already fetched by FetchFullAnalysis — no extra API calls.
        /// Every verdict comes from fixed C# rules below, not from the AI,
        /// so the logic is checkable and consistent every time.
        /// </summary>
        public DecisionTable BuildDecisionTable(string symbol)
        {
            var data = FetchFullAnalysis(symbol).GetAwaiter().GetResult();
            var table = new DecisionTable { Symbol = symbol };

            if (!string.IsNullOrEmpty(data.Error))
            {
                table.Error = data.Error;
                return table;
            }

            decimal price = 0, sma = 0, rsi = 0, volume = 0, change = 0;

            bool priceOk = decimal.TryParse(data.Price, System.Globalization.CultureInfo.InvariantCulture, out price);
            bool smaOk = decimal.TryParse(data.MovingAverage50, System.Globalization.CultureInfo.InvariantCulture, out sma);
            bool rsiOk = decimal.TryParse(data.RSI, System.Globalization.CultureInfo.InvariantCulture, out rsi);
            bool volumeOk = decimal.TryParse(data.Volume, System.Globalization.CultureInfo.InvariantCulture, out volume);
            bool changeOk = decimal.TryParse(data.Change, System.Globalization.CultureInfo.InvariantCulture, out change);

            bool parsedOk = priceOk && smaOk && rsiOk && volumeOk && changeOk;

            if (!parsedOk)
            {
                table.Error = "Could not parse all required numbers for " + symbol + ". Some data may be missing.";
                return table;
            }

            // --- TREND ---
            bool trendPass = price > sma;
            var pctVsSma = sma != 0 ? Math.Round(((price - sma) / sma) * 100, 1) : 0;
            table.Rows.Add(new DecisionRow
            {
                Factor = "Trend",
                Value = "Price $" + price + " vs 50-day avg $" + Math.Round(sma, 2),
                Rule = "Price above 50-day avg suggests buyers are in control. Price below suggests sellers are in control.",
                Result = "$" + price + " is " + Math.Abs(pctVsSma) + "% " + (trendPass ? "above" : "below") +
                         " 50-day avg → " + (trendPass ? "Bullish trend" : "Bearish trend"),
                Passed = trendPass
            });

            // --- MOMENTUM ---
            bool momentumPass = rsi >= 40 && rsi <= 60;
            table.Rows.Add(new DecisionRow
            {
                Factor = "Momentum",
                Value = "RSI " + Math.Round(rsi, 0) + " on 14-day scale",
                Rule = "RSI between 40-60 = healthy, stable momentum (pass). Outside that range = too stretched in either direction (fail).",
                Result = Math.Round(rsi, 0) + " is " + (momentumPass ? "within 40-60 → Momentum passes" : "outside 40-60 → Momentum fails"),
                Passed = momentumPass
            });

            // --- VOLUME (real 90-day average, 1 extra API call) ---
            decimal avgVolume90 = 0;
            bool gotAvgVolume = false;
            string volumeFetchError = "";

            try
            {
                var volUrl = BASE_URL +
                    "?function=TIME_SERIES_DAILY&symbol=" + symbol +
                    "&outputsize=compact&apikey=" + API_KEY;

                var volJson = _httpClient.GetStringAsync(volUrl).GetAwaiter().GetResult();
                var volDoc = JsonDocument.Parse(volJson);

                if (volDoc.RootElement.TryGetProperty("Time Series (Daily)", out var series))
                {
                    var volumes = new List<decimal>();
                    foreach (var day in series.EnumerateObject())
                    {
                        if (day.Value.TryGetProperty("5. volume", out var volProp)
                            && decimal.TryParse(volProp.GetString(), System.Globalization.CultureInfo.InvariantCulture, out var v))
                        {
                            volumes.Add(v);
                        }
                    }

                    // compact gives ~100 days; average whatever we have, capped at 90
                    var sample = volumes.Take(90).ToList();
                    if (sample.Count > 0)
                    {
                        avgVolume90 = sample.Average();
                        gotAvgVolume = true;
                    }
                }
            }
            catch (Exception volEx)
            {
                // Captured here, used below once volumeBasis actually exists.
                volumeFetchError = volEx.Message;
            }

            bool priceDown = change < 0;
            bool highVolume;
            string volumeBasis;

            if (gotAvgVolume && avgVolume90 > 0)
            {
                highVolume = volume > avgVolume90 * 1.2m; // 20% above the real 90-day average
                volumeBasis = "vs real 90-day avg " + avgVolume90.ToString("N0");
            }
            else
            {
                // Fallback only if the extra API call failed (e.g. rate limit hit mid-check)
                highVolume = volume > 20000000;
                volumeBasis = string.IsNullOrEmpty(volumeFetchError)
                    ? "vs fixed 20M threshold (90-day avg returned no data)"
                    : "vs fixed 20M threshold (90-day avg failed: " + volumeFetchError + ")";
            }

            bool volumePass = highVolume && !priceDown;
            string volumeResult;
            if (!highVolume)
            {
                volumeResult = "Volume " + volume.ToString("N0") + " (" + volumeBasis + ") is not unusually high → No strong conviction either way";
            }
            else if (priceDown)
            {
                volumeResult = "High volume " + volume.ToString("N0") + " (" + volumeBasis + ") on a down day → Bearish: strong selling pressure";
            }
            else
            {
                volumeResult = "High volume " + volume.ToString("N0") + " (" + volumeBasis + ") on an up day → Bullish: strong buying pressure";
            }

            table.Rows.Add(new DecisionRow
            {
                Factor = "Volume",
                Value = volume.ToString("N0") + " shares today, price " + (priceDown ? "down" : "up") + " today",
                Rule = "High volume (20%+ above the real 90-day average) confirms the day's price direction is real, not noise. High volume + price down = strong selling. High volume + price up = strong buying.",
                Result = volumeResult,
                Passed = volumePass
            });

            // --- VERDICT ---
            table.PassedCount = table.Rows.Count(r => r.Passed);
            bool shouldBuy = table.PassedCount >= 2;

            var failedFactors = table.Rows.Where(r => !r.Passed).Select(r => r.Factor).ToList();
            var alertPrice = Math.Round(sma, 2);

            table.VerdictAction = shouldBuy
                ? "Status: " + table.PassedCount + " of 3 buy rules met. Action: Conditions support a closer look — this is not a buy signal on its own."
                : "Status: " + table.PassedCount + " of 3 buy rules met. " +
                  (failedFactors.Count > 0 ? string.Join(" failed, ", failedFactors) + " failed. " : "") +
                  "Action: Do not buy. Consider an alert for closing price above $" + alertPrice + ".";

            return table;
        }

        private string BuildSummary(List<StockAnalysisResult> results)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== STOCK MARKET DATA FOR AI ANALYSIS ===");
            sb.AppendLine();

            foreach (var r in results)
            {
                if (!string.IsNullOrEmpty(r.Error))
                {
                    if (r.Error.Contains("rate limit") || r.Error.Contains("25 requests"))
                    {
                        sb.AppendLine("⏱️ " + r.Symbol + ": API rate limit reached for today (25 requests/day free plan). Inform the user this resets in 24 hours and they should try again tomorrow.");
                    }
                    else
                    {
                        sb.AppendLine("❌ " + r.Symbol + ": " + r.Error);
                    }
                    continue;
                }

                sb.AppendLine("📊 " + r.Symbol);
                sb.AppendLine("   Price:          $" + r.Price);
                sb.AppendLine("   Change:         " + r.Change + " (" + r.ChangePercent + ")");
                sb.AppendLine("   High / Low:     $" + r.High + " / $" + r.Low);
                sb.AppendLine("   Volume:         " + r.Volume);
                sb.AppendLine("   RSI (14):       " + r.RSI);
                sb.AppendLine("   50-Day SMA:     $" + r.MovingAverage50);
                sb.AppendLine("   Trading Day:    " + r.LatestTradingDay);
                sb.AppendLine();
            }

            sb.AppendLine("=== END OF DATA ===");
            return sb.ToString();
        }

        private string GetVal(JsonElement element, string key)
        {
            if (element.TryGetProperty(key, out var value))
                return value.GetString() ?? "N/A";
            return "N/A";
        }
    }
}