// ═══ LOCKED BASE: Strategies ═══
// Verified correct as of June 2026 audit.
//
// RsiStrategy: pluggable buy/sell thresholds + optional 200-day MA trend
// filter. Logic mirrors BacktestEngine exactly -- same null-RSI skip,
// same trend-filter guard, same trade construction.
//
// BuyAndHoldStrategy: returns exactly one trade covering the full period.
// Used as the baseline comparator in ResearchService.
//
// New strategies should implement IStrategy and be added here.
// Do not change existing strategy logic -- it would invalidate the
// 59-stock research report results.
// ═════════════════════════════
namespace MyAIAgent.Services
{
    /// <summary>
    /// The strategy already tested: buy when RSI(14) drops below the oversold
    /// threshold, sell when it rises above the overbought threshold.
    /// Optional 200-day MA trend filter blocks entries when price is in an
    /// uptrend (price > 200-day MA), limiting the strategy to mean-reverting
    /// environments.
    /// </summary>
    public class RsiStrategy : IStrategy
    {
        private readonly int _buyThreshold;
        private readonly int _sellThreshold;
        private readonly bool _trendFilter;

        public RsiStrategy(int buyThreshold = 30, int sellThreshold = 70, bool trendFilter = false)
        {
            _buyThreshold = buyThreshold;
            _sellThreshold = sellThreshold;
            _trendFilter = trendFilter;
        }

        public string Name => _trendFilter
            ? $"RSI({_buyThreshold}/{_sellThreshold}) + 200-day MA filter"
            : $"RSI({_buyThreshold}/{_sellThreshold})";

        public string Description => _trendFilter
            ? $"Buy when RSI(14) < {_buyThreshold} AND price < 200-day MA; sell when RSI(14) > {_sellThreshold}"
            : $"Buy when RSI(14) < {_buyThreshold} (oversold); sell when RSI(14) > {_sellThreshold} (overbought)";

        public List<SimulatedTrade> Run(List<DailyBar> bars)
        {
            var closes = bars.Select(b => b.Close).ToList();
            var rsiSeries = TechnicalIndicators.CalculateRsiSeries(closes, 14);
            var sma200 = _trendFilter
                ? TechnicalIndicators.CalculateSmaSeries(closes, 200)
                : null;

            var trades = new List<SimulatedTrade>();
            bool holding = false;
            DateTime buyDate = default;
            decimal buyPrice = 0;

            for (int i = 0; i < bars.Count; i++)
            {
                var rsi = rsiSeries[i];
                if (rsi == null) continue;

                bool trendOk = true;
                if (_trendFilter && sma200 != null)
                {
                    var ma = sma200[i];
                    trendOk = ma != null && bars[i].Close < ma;
                }

                if (!holding && rsi < _buyThreshold && trendOk)
                {
                    holding = true;
                    buyDate = bars[i].Date;
                    buyPrice = bars[i].Close;
                }
                else if (holding && rsi > _sellThreshold)
                {
                    var ret = ((bars[i].Close - buyPrice) / buyPrice) * 100;
                    trades.Add(new SimulatedTrade
                    {
                        BuyDate = buyDate,
                        BuyPrice = buyPrice,
                        SellDate = bars[i].Date,
                        SellPrice = bars[i].Close,
                        ReturnPercent = ret
                    });
                    holding = false;
                }
            }

            return trades;
        }
    }

    /// <summary>
    /// Baseline comparator: buy on the first available bar, hold forever.
    /// Every other strategy is measured against this — if you can't beat it,
    /// there's no reason to use the strategy over doing nothing.
    /// Returns exactly one "trade" covering the full period.
    /// </summary>
    public class BuyAndHoldStrategy : IStrategy
    {
        public string Name => "Buy and hold";
        public string Description => "Buy on day 1, hold through the entire period. Baseline comparator.";

        public List<SimulatedTrade> Run(List<DailyBar> bars)
        {
            if (bars.Count < 2) return new List<SimulatedTrade>();

            var ret = ((bars.Last().Close - bars.First().Close) / bars.First().Close) * 100;
            return new List<SimulatedTrade>
            {
                new SimulatedTrade
                {
                    BuyDate  = bars.First().Date,
                    BuyPrice = bars.First().Close,
                    SellDate  = bars.Last().Date,
                    SellPrice = bars.Last().Close,
                    ReturnPercent = ret
                }
            };
        }
    }
}