using MyAIAgent.Data;
using MyAIAgent.Models;
using Microsoft.EntityFrameworkCore;

namespace MyAIAgent.Services
{
    /// <summary>
    /// Paper Portfolio Service — V1
    ///
    /// Open a position:  records entry price, date, RSI at entry, target exit RSI, optional notes.
    /// Close a position: records exit price, date, RSI at exit.
    ///                   Fetches B&H benchmark (Yahoo Finance) for the same period.
    /// P&L on open:      caller supplies last screener run price (no extra API call).
    /// </summary>
    public class PaperPortfolioService
    {
        private readonly AppDbContext _db;
        private readonly HistoricalDataService _data;

        public PaperPortfolioService(AppDbContext db, HistoricalDataService data)
        {
            _db = db;
            _data = data;
        }

        // ── OPEN a new paper trade ─────────────────────────────────────────
        public async Task<PaperTrade> OpenTradeAsync(OpenTradeRequest req)
        {
            var trade = new PaperTrade
            {
                UserName = req.UserName,
                Symbol = req.Symbol.ToUpper(),
                Sector = req.Sector,
                EntryPrice = req.EntryPrice,
                EntryDate = req.EntryDate.Date,
                RsiAtEntry = req.RsiAtEntry,
                TargetExitRsi = req.TargetExitRsi,
                Notes = string.IsNullOrWhiteSpace(req.Notes) ? null : req.Notes.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            _db.PaperTrades.Add(trade);
            await _db.SaveChangesAsync();
            return trade;
        }

        // ── CLOSE an existing paper trade ─────────────────────────────────
        public async Task<PaperTrade?> CloseTradeAsync(CloseTradeRequest req)
        {
            var trade = await _db.PaperTrades
                .FirstOrDefaultAsync(t => t.Id == req.TradeId && t.UserName == req.UserName);

            if (trade == null || !trade.IsOpen) return null;

            trade.ExitPrice = req.ExitPrice;
            trade.ExitDate = req.ExitDate.Date;
            trade.RsiAtExit = req.RsiAtExit;

            // ── Fetch B&H benchmark for the same hold period ──────────────
            try
            {
                var bars = await _data.GetDailyHistoryRangeAsync(
                    trade.Symbol, trade.EntryDate, trade.ExitDate.Value);

                if (bars.Count >= 2)
                {
                    var entryBar = bars.FirstOrDefault(b => b.Date >= trade.EntryDate);
                    var exitBar = bars.LastOrDefault(b => b.Date <= trade.ExitDate.Value);

                    if (entryBar != null && exitBar != null && entryBar.Close > 0)
                    {
                        trade.BenchmarkBahReturn = Math.Round(
                            ((exitBar.Close - entryBar.Close) / entryBar.Close) * 100, 2);
                    }
                }
            }
            catch
            {
                trade.BenchmarkBahReturn = null;
            }

            await _db.SaveChangesAsync();
            return trade;
        }

        // ── GET all trades for a user ──────────────────────────────────────
        public async Task<PortfolioSummary> GetSummaryAsync(
            string userName, List<PriceUpdate>? livePrices = null)
        {
            var trades = await _db.PaperTrades
                .Where(t => t.UserName == userName)
                .OrderByDescending(t => t.EntryDate)
                .ToListAsync();

            var open = trades.Where(t => t.IsOpen).ToList();
            var closed = trades.Where(t => !t.IsOpen).ToList();

            var priceMap = livePrices?.ToDictionary(p => p.Symbol.ToUpper(), p => p.Price)
                           ?? new Dictionary<string, decimal>();

            // ── Open position rows ─────────────────────────────────────────
            var openRows = open.Select(t =>
            {
                decimal? currentPrice = priceMap.TryGetValue(t.Symbol, out var p) ? p : null;
                decimal? unrealizedPct = currentPrice.HasValue && t.EntryPrice > 0
                    ? Math.Round(((currentPrice.Value - t.EntryPrice) / t.EntryPrice) * 100, 2)
                    : null;

                return new OpenTradeRow
                {
                    Id = t.Id,
                    Symbol = t.Symbol,
                    Sector = t.Sector,
                    EntryPrice = t.EntryPrice,
                    EntryDate = t.EntryDate,
                    RsiAtEntry = t.RsiAtEntry,
                    TargetExitRsi = t.TargetExitRsi,
                    CurrentPrice = currentPrice,
                    UnrealizedPct = unrealizedPct,
                    DaysHeld = (DateTime.UtcNow.Date - t.EntryDate).Days,
                    Notes = t.Notes
                };
            }).ToList();

            // ── Closed trade rows ──────────────────────────────────────────
            var closedRows = closed.Select(t =>
            {
                decimal? tradePct = t.ExitPrice.HasValue && t.EntryPrice > 0
                    ? Math.Round(((t.ExitPrice.Value - t.EntryPrice) / t.EntryPrice) * 100, 2)
                    : null;

                decimal? vsbenchmark = tradePct.HasValue && t.BenchmarkBahReturn.HasValue
                    ? Math.Round(tradePct.Value - t.BenchmarkBahReturn.Value, 2)
                    : null;

                return new ClosedTradeRow
                {
                    Id = t.Id,
                    Symbol = t.Symbol,
                    Sector = t.Sector,
                    EntryPrice = t.EntryPrice,
                    EntryDate = t.EntryDate,
                    ExitPrice = t.ExitPrice!.Value,
                    ExitDate = t.ExitDate!.Value,
                    RsiAtEntry = t.RsiAtEntry,
                    RsiAtExit = t.RsiAtExit,
                    TradePct = tradePct,
                    BenchmarkBahReturn = t.BenchmarkBahReturn,
                    VsBenchmark = vsbenchmark,
                    DaysHeld = (t.ExitDate.Value - t.EntryDate).Days,
                    BeatBenchmark = vsbenchmark.HasValue && vsbenchmark.Value > 0,
                    Notes = t.Notes
                };
            }).ToList();

            // ── Aggregate stats (closed trades only) ──────────────────────
            int closedCount = closedRows.Count;
            int wins = closedRows.Count(r => r.TradePct > 0);
            int beatBench = closedRows.Count(r => r.BeatBenchmark);
            decimal? avgReturn = closedCount > 0
                ? Math.Round(closedRows.Where(r => r.TradePct.HasValue)
                    .Average(r => r.TradePct!.Value), 2)
                : null;
            decimal? avgVsBench = closedCount > 0 && closedRows.Any(r => r.VsBenchmark.HasValue)
                ? Math.Round(closedRows.Where(r => r.VsBenchmark.HasValue)
                    .Average(r => r.VsBenchmark!.Value), 2)
                : null;

            return new PortfolioSummary
            {
                OpenCount = open.Count,
                ClosedCount = closedCount,
                WinRate = closedCount > 0
                    ? Math.Round((decimal)wins / closedCount * 100, 1) : null,
                BeatBenchmarkRate = closedCount > 0
                    ? Math.Round((decimal)beatBench / closedCount * 100, 1) : null,
                AvgTradeReturn = avgReturn,
                AvgVsBenchmark = avgVsBench,
                OpenTrades = openRows,
                ClosedTrades = closedRows
            };
        }

        // ── DELETE a trade (only open trades can be deleted) ──────────────
        public async Task<bool> DeleteTradeAsync(int tradeId, string userName)
        {
            var trade = await _db.PaperTrades
                .FirstOrDefaultAsync(t => t.Id == tradeId && t.UserName == userName);

            if (trade == null || !trade.IsOpen) return false;

            _db.PaperTrades.Remove(trade);
            await _db.SaveChangesAsync();
            return true;
        }
    }

    // ── Request / Response models ──────────────────────────────────────────
    public record OpenTradeRequest(
        string UserName,
        string Symbol,
        string Sector,
        decimal EntryPrice,
        DateTime EntryDate,
        decimal RsiAtEntry,
        decimal TargetExitRsi,
        string? Notes = null);

    public record CloseTradeRequest(
        int TradeId,
        string UserName,
        decimal ExitPrice,
        DateTime ExitDate,
        decimal RsiAtExit);

    public record PriceUpdate(string Symbol, decimal Price);

    public class PortfolioSummary
    {
        public int OpenCount { get; set; }
        public int ClosedCount { get; set; }
        public decimal? WinRate { get; set; }
        public decimal? BeatBenchmarkRate { get; set; }
        public decimal? AvgTradeReturn { get; set; }
        public decimal? AvgVsBenchmark { get; set; }
        public List<OpenTradeRow> OpenTrades { get; set; } = new();
        public List<ClosedTradeRow> ClosedTrades { get; set; } = new();
    }

    public class OpenTradeRow
    {
        public int Id { get; set; }
        public string Symbol { get; set; } = "";
        public string Sector { get; set; } = "";
        public decimal EntryPrice { get; set; }
        public DateTime EntryDate { get; set; }
        public decimal RsiAtEntry { get; set; }
        public decimal TargetExitRsi { get; set; }
        public decimal? CurrentPrice { get; set; }
        public decimal? UnrealizedPct { get; set; }
        public int DaysHeld { get; set; }
        public string? Notes { get; set; }
    }

    public class ClosedTradeRow
    {
        public int Id { get; set; }
        public string Symbol { get; set; } = "";
        public string Sector { get; set; } = "";
        public decimal EntryPrice { get; set; }
        public DateTime EntryDate { get; set; }
        public decimal ExitPrice { get; set; }
        public DateTime ExitDate { get; set; }
        public decimal RsiAtEntry { get; set; }
        public decimal? RsiAtExit { get; set; }
        public decimal? TradePct { get; set; }
        public decimal? BenchmarkBahReturn { get; set; }
        public decimal? VsBenchmark { get; set; }
        public int DaysHeld { get; set; }
        public bool BeatBenchmark { get; set; }
        public string? Notes { get; set; }
    }
}