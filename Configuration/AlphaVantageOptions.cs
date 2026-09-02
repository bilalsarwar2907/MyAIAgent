namespace MyAIAgent.Configuration
{
    /// <summary>
    /// Bound from the "AlphaVantage" config section (appsettings.json /
    /// appsettings.Development.json / environment variables).
    /// Keeps the API key out of source code.
    /// </summary>
    public class AlphaVantageOptions
    {
        public const string SectionName = "AlphaVantage";

        public string ApiKey { get; set; } = "";
        public string BaseUrl { get; set; } = "https://www.alphavantage.co/query";
    }
}
