using MyAIAgent.Configuration;
using MyAIAgent.Models;
using Newtonsoft.Json;
using System.Text;
using MyAIAgent.Data;
using Microsoft.Extensions.Options;

namespace MyAIAgent.Services
{
    public class AIService : IAiService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly AppDbContext _dbContext;
        private readonly OllamaOptions _ollama;
        private readonly TradingOptions _trading;

        private const string SYSTEM_PROMPT =
            "You are a smart AI assistant with two purposes:\n\n" +
            "1. LEARNING ASSISTANT:\n" +
            "   - Help the user learn programming (C#, Python, JavaScript, Vue.js, APIs).\n" +
            "   - Teach step by step. Explain simply. Do not give full solutions immediately.\n" +
            "   - Help the student think. Use beginner-friendly examples.\n\n" +
            "2. STOCK TRADING ASSISTANT:\n" +
            "   - Help the user analyze stocks and understand market trends.\n" +
            "   - Explain concepts like P/E ratio, moving averages, RSI, support/resistance clearly.\n" +
            "   - Always remind the user that this is NOT financial advice.\n" +
            "   - Help track watchlists, interpret data, and suggest research strategies.\n\n" +
            "Be concise, practical, and encouraging.";

        private const string ANALYSIS_PROMPT =
            "You are an expert stock market analyst.\n\n" +
            "You will be given real market data for one or more stocks.\n" +
            "Your job is to analyze the data and give a clear, structured response.\n\n" +
            "For each stock always cover:\n" +
            "1. TREND: Is the stock going up or down? Is price above or below the 50-day average?\n" +
            "2. MOMENTUM: What does the RSI tell us? (Below 30 = oversold, Above 70 = overbought)\n" +
            "3. VOLUME: Is trading volume unusual? High volume confirms a move.\n" +
            "4. VERDICT: Should the user watch, buy, or avoid this stock right now?\n\n" +
            "If multiple stocks are given, compare them and say which looks stronger.\n\n" +
            "Always end with: 'This is not financial advice. Always do your own research.'\n\n" +
            "Be direct, specific, and use the actual numbers from the data.";

        public AIService(
            AppDbContext dbContext,
            IHttpClientFactory httpClientFactory,
            IOptions<OllamaOptions> ollamaOptions,
            IOptions<TradingOptions> tradingOptions)
        {
            _dbContext = dbContext;
            _httpClientFactory = httpClientFactory;
            _ollama = ollamaOptions.Value;
            _trading = tradingOptions.Value;
        }

        /// <summary>
        /// Saves a tool-based exchange (user message + tool result) to conversation history,
        /// so it appears in the chat history sidebar like normal AI replies.
        /// </summary>
        public async Task SaveToolMessage(string userMessage, string toolResult, string conversationId, string userName)
        {
            _dbContext.ChatMessages.Add(new ChatMessage
            {
                Role = "user",
                Content = userMessage,
                ConversationId = conversationId,
                UserName = userName,
                CreatedAt = DateTime.UtcNow
            });

            _dbContext.ChatMessages.Add(new ChatMessage
            {
                Role = "assistant",
                Content = toolResult,
                ConversationId = conversationId,
                UserName = userName,
                CreatedAt = DateTime.UtcNow
            });

            await _dbContext.SaveChangesAsync();
        }

        /// <summary>
        /// Normal chat with conversation memory, scoped to a specific user.
        /// </summary>
        public async Task<string> AskAI(string userMessage, string conversationId, string userName)
        {
            var oldMessages = _dbContext.ChatMessages
                .Where(x => x.ConversationId == conversationId)
                .OrderBy(x => x.Id)
                .ToList();

            var messages = new List<Message>
            {
                new Message { role = "system", content = SYSTEM_PROMPT }
            };

            foreach (var msg in oldMessages)
            {
                messages.Add(new Message { role = msg.Role, content = msg.Content });
            }

            _dbContext.ChatMessages.Add(new ChatMessage
            {
                Role = "user",
                Content = userMessage,
                ConversationId = conversationId,
                UserName = userName,
                CreatedAt = DateTime.UtcNow
            });
            await _dbContext.SaveChangesAsync();

            messages.Add(new Message { role = "user", content = userMessage });

            var aiReply = await CallOllamaAsync(messages);

            _dbContext.ChatMessages.Add(new ChatMessage
            {
                Role = "assistant",
                Content = aiReply,
                ConversationId = conversationId,
                UserName = userName,
                CreatedAt = DateTime.UtcNow
            });
            await _dbContext.SaveChangesAsync();

            return aiReply;
        }

        /// <summary>
        /// Feeds real stock market data to the AI and asks for a professional analysis.
        /// </summary>
        public async Task<string> AnalyzeStocks(string stockData, string userQuestion)
        {
            var prompt =
                "The user asked: \"" + userQuestion + "\"\n\n" +
                "Here is the real-time market data:\n\n" +
                stockData + "\n\n" +
                "Now analyze this data and answer the user's question with specific insights.";

            var messages = new List<Message>
            {
                new Message { role = "system", content = ANALYSIS_PROMPT },
                new Message { role = "user",   content = prompt }
            };

            return await CallOllamaAsync(messages);
        }

        // ── ADD THIS METHOD to AIService.cs ──────────────────────────────────────
        // Place it alongside AnalyzeStocks() — same pattern, different system prompt.

        /// <summary>
        /// Sends the structured research prompt (built by StockResearchTool) to Ollama
        /// and returns a plain-English explanation of the backtest results.
        /// The AI explains what the data shows — it does not predict or recommend.
        /// </summary>
        public async Task<string> InterpretResearch(string researchPrompt)
        {
            var messages = new List<Message>
    {
        new Message
        {
            role = "system",
            content =
                "You are an honest financial data interpreter.\n\n" +
                "You will be given historical backtest results for a stock.\n" +
                "Your job is to explain what the data shows in plain English.\n\n" +
                "Rules:\n" +
                "- Never say 'buy', 'sell', or give investment recommendations.\n" +
                "- Focus only on what the historical data shows.\n" +
                "- Be specific: use the actual numbers from the data.\n" +
                "- Keep it to 3-4 sentences. Be direct and clear.\n" +
                "- End with: 'Past performance does not predict future results.'"
        },
        new Message
        {
            role = "user",
            content = researchPrompt
        }
    };

            return await CallOllamaAsync(messages);
        }

        /// <summary>
        /// Asks the AI to decide whether the user message requires a tool.
        ///
        /// INTENTIONAL OMISSION: ResearchStock is NOT listed here.
        /// phi3:mini struggles to reliably extract a clean ticker symbol from
        /// natural-language research queries, and ResearchStock is slow (~5s
        /// Yahoo Finance fetch + Ollama call). Instead, research queries are
        /// handled by the dedicated isResearchQuery keyword block in Program.cs,
        /// which extracts the symbol with a regex and calls the tool directly --
        /// bypassing DecideTool entirely. This avoids wasted API calls and
        /// unreliable JSON parsing from the model.
        ///
        /// If you add a new tool that needs AI routing, add it to the prompt
        /// below AND register a keyword block in Program.cs as a fallback.
        /// </summary>
        public async Task<ToolResponse> DecideTool(string userMessage)
        {
            var prompt =
                "You are an AI agent that decides if a tool is needed.\n\n" +
               "Available tools:\n" +
                 "- SaveNote: Use when the user wants to save, remember, or note something.\n" +
                   "- GetStockPrice: Use when the user asks for a stock price. ToolInput must be the stock symbol only (e.g. AAPL, TSLA, MSFT).\n" +
                    "- AnalyzeStock: Use when the user wants to analyze, compare, or get a recommendation on stocks. ToolInput must be comma-separated symbols (e.g. AAPL,MSFT,GOOGL).\n" +
                      "- GetStockNews: Use when the user asks for news, headlines, or recent updates about a stock. ToolInput must be the stock symbol only (e.g. AAPL).\n\n" +
                "Respond ONLY with valid JSON. No markdown, no explanation.\n\n" +
                "If tool is needed:\n" +
                "{ \"useTool\": true, \"toolName\": \"AnalyzeStock\", \"toolInput\": \"AAPL,MSFT\" }\n\n" +
                "If no tool is needed:\n" +
                "{ \"useTool\": false, \"toolName\": \"\", \"toolInput\": \"\" }\n\n" +
                "User message: " + userMessage;

            var messages = new List<Message>
            {
                new Message { role = "user", content = prompt }
            };

            try
            {
                var rawResponse = await CallOllamaAsync(messages);

                var cleanJson = rawResponse
                    .Replace("```json", "")
                    .Replace("```", "")
                    .Trim();

                var toolResponse = JsonConvert.DeserializeObject<ToolResponse>(cleanJson);
                return toolResponse ?? new ToolResponse { UseTool = false };
            }
            catch (Exception ex)
            {
                Console.WriteLine("[DecideTool] Failed to parse tool response: " + ex.Message);
                return new ToolResponse { UseTool = false };
            }
        }

        /// <summary>
        /// Reads trading_output.txt (written daily by daily_agent.py) and returns
        /// a formatted context block to inject into the system prompt.
        /// Returns empty string if the file doesn't exist yet.
        /// </summary>
        private string BuildPortfolioContext()
        {
            var reportPath = _trading.PortfolioReportPath;
            try
            {
                if (string.IsNullOrWhiteSpace(reportPath) || !File.Exists(reportPath)) return "";
                var content = File.ReadAllText(reportPath);
                return
                    "\n\n--- CURRENT PORTFOLIO STATE (auto-updated daily by daily_agent.py) ---\n" +
                    content +
                    "\n--- END PORTFOLIO STATE ---\n\n" +
                    "When the user asks about their trades, positions, RSI, P&L, or portfolio status, " +
                    "answer using the data above. Do not ask the user to provide this information.";
            }
            catch
            {
                return "";
            }
        }

        /// <summary>
        /// Same as AskAI() but injects the current portfolio state from trading_output.txt
        /// into the system prompt. Called for position-related queries only.
        /// </summary>
        public async Task<string> AskAIWithPortfolioContext(string userMessage, string conversationId, string userName)
        {
            var enrichedSystemPrompt = SYSTEM_PROMPT + BuildPortfolioContext();

            var oldMessages = _dbContext.ChatMessages
                .Where(x => x.ConversationId == conversationId)
                .OrderBy(x => x.Id)
                .ToList();

            var messages = new List<Message>
            {
                new Message { role = "system", content = enrichedSystemPrompt }
            };

            foreach (var msg in oldMessages)
                messages.Add(new Message { role = msg.Role, content = msg.Content });

            _dbContext.ChatMessages.Add(new ChatMessage
            {
                Role = "user",
                Content = userMessage,
                ConversationId = conversationId,
                UserName = userName,
                CreatedAt = DateTime.UtcNow
            });
            await _dbContext.SaveChangesAsync();

            messages.Add(new Message { role = "user", content = userMessage });

            var aiReply = await CallOllamaAsync(messages);

            _dbContext.ChatMessages.Add(new ChatMessage
            {
                Role = "assistant",
                Content = aiReply,
                ConversationId = conversationId,
                UserName = userName,
                CreatedAt = DateTime.UtcNow
            });
            await _dbContext.SaveChangesAsync();

            return aiReply;
        }

        /// <summary>
        /// Sends messages to Ollama and returns the AI text reply.
        /// </summary>
        private async Task<string> CallOllamaAsync(List<Message> messages)
        {
            var requestBody = new ChatRequest
            {
                model = _ollama.Model,
                stream = false,
                messages = messages
            };

            var json = JsonConvert.SerializeObject(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var http = _httpClientFactory.CreateClient("ollama");
            var response = await http.PostAsync(_ollama.Url, content);
            var responseJson = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception("Ollama API error " + response.StatusCode + ": " + responseJson);

            var result = JsonConvert.DeserializeObject<ChatResponse>(responseJson);

            if (result?.message?.content == null)
                throw new Exception("Ollama returned an empty response.");

            return result.message.content;
        }
    }
}