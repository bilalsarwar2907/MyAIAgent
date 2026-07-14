namespace MyAIAgent.Models
{
    /// <summary>
    /// Represents a single paper trade entry in the RSI research portfolio.
    /// Tracks entry/exit, RSI signal values, and B&H benchmark for comparison.
    /// </summary>
    public class PaperTrade
    {
        public int Id { get; set; }
        public string UserName { get; set; } = "";
        public string Symbol { get; set; } = "";
        public string Sector { get; set; } = "";
        // ── Entry ──────────────────────────────────────────────
        public decimal EntryPrice { get; set; }
        public DateTime EntryDate { get; set; }
        public decimal RsiAtEntry { get; set; }      // RSI value that triggered the trade
        public decimal TargetExitRsi { get; set; }   // e.g. 60 = exit when RSI recovers to 60
        // ── Exit (null = position still open) ─────────────────
        public decimal? ExitPrice { get; set; }
        public DateTime? ExitDate { get; set; }
        public decimal? RsiAtExit { get; set; }
        // ── Benchmark (populated on close via Yahoo Finance) ───
        // B&H return = (price on exit date / price on entry date) - 1
        // fetched from HistoricalDataService at close time.
        public decimal? BenchmarkBahReturn { get; set; }  // % e.g. 12.4
        // ── Notes (one-sentence reason for entering the trade) ─
        public string? Notes { get; set; }
        // ── Metadata ──────────────────────────────────────────
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsOpen => ExitDate == null;
    }
}