namespace MyAIAgent.Services
{
    /// <summary>
    /// Sector- and factor-level aggregation over <see cref="IResearchService"/>
    /// results. Each method returns the exact anonymous-shaped payload the
    /// /research/* endpoints used to build inline; the endpoints now just wrap
    /// the result in Results.Json.
    /// </summary>
    public interface IResearchQueryService
    {
        Task<object> SectorSummaryAsync(string sectorName);

        Task<object> AllSectorsAsync();

        Task<object> TrendStrengthFactorAsync();

        Task<object> TrendStrengthFactorRangeAsync(int fromYear, int toYear);
    }
}
