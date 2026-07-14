namespace MyAIAgent.Services
{
    /// <summary>
    /// 60 symbols across 10 sectors for the large-scale go/no-go backtest.
    ///
    /// SECTOR SELECTION NOTES (honest, not marketing):
    ///
    /// - Mix of "obvious winners" (AAPL, MSFT, NVDA) AND companies that have
    ///   had real multi-year struggles (F, GM, PFE, DAL) so the result isn't
    ///   purely survivorship bias.
    ///
    /// - 6 stocks per sector: enough to see intra-sector variation, small enough
    ///   to not take all night to fetch.
    ///
    /// - Survivorship bias is NOT fully solved here — all 60 are still-listed
    ///   companies. A fully survivorship-bias-free test needs a historical
    ///   constituent list (e.g. S&P 500 as-of-2019) which requires a paid data
    ///   source. This is the best we can do cheaply, and it's good enough to
    ///   answer "does the signal have any edge at all" for v1.
    ///
    /// - DO NOT cherry-pick symbols after seeing results. The universe is
    ///   defined here, before running, and should not change based on output.
    /// </summary>
    public static class StockUniverse
    {
        public static readonly Dictionary<string, string[]> BySector = new()
        {
            ["tech"] = new[]
            {
                "AAPL",  // mega-cap, long history, liquid
                "MSFT",  // mega-cap
                "GOOGL", // mega-cap
                "NVDA",  // volatile, GPU AI boom — tests high-RSI environments
                "INTC",  // struggling relative to NVDA — survivorship counterweight
                "IBM"    // mature, slow-moving — tests low-volatility environment
            },

            ["banks"] = new[]
            {
                "JPM",   // strongest US bank
                "BAC",   // mid-tier
                "WFC",   // had major scandal, recovery period in data
                "C",     // volatile, underperformed peers
                "GS",    // investment bank, different profile to retail banks
                "MS"     // investment bank
            },

            ["auto"] = new[]
            {
                "TSLA",  // extreme volatility, high RSI swings
                "F",     // struggling legacy
                "GM",    // struggling legacy
                "TM",    // Toyota — stable Japanese OEM, less volatile
                "RACE",  // Ferrari — luxury, different demand profile
                "RIVN"   // EV startup, high risk — tests recent IPO behaviour
            },

            ["pharma"] = new[]
            {
                "PFE",   // COVID boom-bust visible in data
                "JNJ",   // defensive, slow-moving
                "MRK",   // steady grower
                "ABBV",  // dividend-heavy
                "BMY",   // mid-tier
                "LLY"    // Ozempic boom — tests explosive upside RSI behaviour
            },

            ["energy"] = new[]
            {
                "XOM",   // large integrated
                "CVX",   // large integrated
                "COP",   // independent E&P
                "SLB",   // oilfield services — more volatile than majors
                "OXY",   // Buffett-famous, mid-cap E&P
                "BP"     // international, UK-listed but trades on NYSE
            },

            ["retail"] = new[]
            {
                "WMT",   // defensive giant
                "TGT",   // had sharp drop in 2022 — real drawdown in data
                "COST",  // membership model, steady
                "AMZN",  // also tech, but retail/consumer dominant revenue
                "HD",    // home improvement
                "LOW"    // home improvement — pair with HD for comparison
            },

            ["utilities"] = new[]
            {
                "NEE",   // largest US utility, had sharp 2023 correction
                "DUK",   // mid-size, stable
                "SO",    // Southeast US, very slow mover
                "AEP",   // Midwest utility
                "EXC",   // mixed utility/generation
                "PCG"    // PG&E — had bankruptcy, tests extreme event in data
            },

            ["reits"] = new[]
            {
                "O",     // Realty Income, monthly dividend, very stable
                "PLD",   // industrial/warehouse REIT, boom from e-commerce
                "SPG",   // mall REIT — stress-tested by COVID in data
                "WELL",  // healthcare REIT
                "AMT",   // cell tower REIT, different from property REITs
                "VNO"    // office REIT — struggling post-COVID, counterweight
            },

            ["airlines"] = new[]
            {
                "DAL",   // Delta, largest by revenue
                "UAL",   // United, volatile
                "LUV",   // Southwest, different cost model
                "AAL",   // American, most leveraged — highest risk
                "ALK",   // Alaska Air, smaller
                "JBLU"   // JetBlue — low-cost carrier, went through post-COVID stress, different cost model to legacy airlines
            },

            ["industrial"] = new[]
            {
                "CAT",   // Caterpillar, cyclical
                "DE",    // Deere, agriculture + construction
                "GE",    // had major restructuring visible in data
                "HON",   // diversified industrial
                "MMM",   // 3M — had legal issues, underperformed
                "UPS"    // logistics, COVID boom-bust visible
            }
        };

        /// <summary>All 60 symbols as a flat list for RunBatchAsync.</summary>
        public static string[] All => BySector.Values.SelectMany(s => s).ToArray();
    }
}