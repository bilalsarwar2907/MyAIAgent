namespace MyAIAgent.Configuration
{
    /// <summary>
    /// Bound from the "Ollama" config section. Endpoint + model for the local LLM.
    /// </summary>
    public class OllamaOptions
    {
        public const string SectionName = "Ollama";

        public string Url { get; set; } = "http://localhost:11434/api/chat";
        public string Model { get; set; } = "phi3:mini";

        /// <summary>Request timeout in minutes for a single Ollama call.</summary>
        public int TimeoutMinutes { get; set; } = 15;
    }
}
