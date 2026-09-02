namespace MyAIAgent.Common
{
    /// <summary>
    /// Compounds a series of per-trade percentage returns into a total return and
    /// the worst peak-to-trough drawdown on that closed-trade equity curve.
    ///
    /// This is the exact loop that BacktestEngine, ResearchService and
    /// VolatilityFactorService each carried their own copy of — extracted so a
    /// change is made once. Math is byte-for-byte identical to the originals:
    /// equity starts at 1, multiplies by (1 + r/100) per trade, drawdown is
    /// (equity - peak) / peak * 100.
    /// </summary>
    public static class EquityCurve
    {
        public readonly record struct Result(decimal TotalReturnPercent, decimal MaxDrawdownPercent);

        public static Result Compound(IEnumerable<decimal> tradeReturnPercents, int round = 2)
        {
            decimal equity = 1m, peak = 1m, worstDrawdown = 0m;

            foreach (var r in tradeReturnPercents)
            {
                equity *= (1 + (r / 100));
                if (equity > peak) peak = equity;
                var drawdown = (equity - peak) / peak * 100;
                if (drawdown < worstDrawdown) worstDrawdown = drawdown;
            }

            return new Result(
                Math.Round((equity - 1) * 100, round),
                Math.Round(worstDrawdown, round));
        }

        /// <summary>
        /// Buy-and-hold percentage return between two price levels.
        /// </summary>
        public static decimal BuyAndHoldReturnPercent(decimal firstClose, decimal lastClose, int round = 2) =>
            firstClose == 0 ? 0 : Math.Round(((lastClose - firstClose) / firstClose) * 100, round);
    }
}
