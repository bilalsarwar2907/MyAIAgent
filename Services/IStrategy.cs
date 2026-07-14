namespace MyAIAgent.Services
{
    /// <summary>
    /// Contract every strategy must implement.
    /// BacktestEngine runs any IStrategy against historical bars —
    /// adding a new strategy means implementing this interface,
    /// not touching the engine.
    /// </summary>
    public interface IStrategy
    {
        /// <summary>Short display name shown in reports and the UI.</summary>
        string Name { get; }

        /// <summary>
        /// One-sentence description of the entry/exit rule, shown alongside results
        /// so users know exactly what was tested — no black boxes.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Run the strategy against a pre-loaded bar series (oldest first).
        /// Returns every completed trade; open positions at end of data are discarded.
        /// </summary>
        List<SimulatedTrade> Run(List<DailyBar> bars);
    }
}