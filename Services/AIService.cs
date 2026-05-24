using MyAIAgent.Models;
using Newtonsoft.Json;
using System.Text;
using MyAIAgent.Data;
using Microsoft.EntityFrameworkCore;

namespace MyAIAgent.Services
{
    public class AIService
    {
        private readonly HttpClient _httpClient;
        private readonly AppDbContext _dbContext;

        /// <summary>
        /// Initializes the AI service with an HTTP client and database context.
        /// </summary>
        /// <param name="dbContext">Database context for storing and retrieving chat messages.</param>
        public AIService(AppDbContext dbContext)
        {
            _dbContext = dbContext;
            _httpClient = new HttpClient();
            // Increase timeout for local AI models (e.g., Ollama)
            _httpClient.Timeout = TimeSpan.FromMinutes(10);
        }

        /// <summary>
        /// Sends a user message to the AI and returns the assistant's reply.
        /// Implements conversation-specific memory by filtering and saving messages with a conversation ID.
        /// </summary>
        /// <param name="userMessage">The message from the user.</param>
        /// <param name="conversationId">Unique identifier for the conversation (e.g., a GUID or session ID).</param>
        /// <returns>The AI's response text.</returns>
        public async Task<string> AskAI(string userMessage, string conversationId)
        {
            // ========== STEP 75: Load conversation memory filtered by ConversationId ==========
            // Instead of loading all messages from DB, we only load those belonging to the current conversation.
            var oldMessages = _dbContext.ChatMessages
                .Where(x => x.ConversationId == conversationId)
                .ToList();

            // Build the message list for the AI request.
            // Start with the system instruction (fixed for all conversations).
            var messages = new List<Message>
        {
            new Message
            {
                role = "system",
                content = @"You are a senior C# tutor and software mentor.

Teach step by step.
Explain simply.
Do not give full solutions immediately.
Help the student think.
Explain debugging carefully.
Use beginner-friendly examples.
Focus on C#, APIs, Razor Pages, SQL, Vue, Python, axios, JavaScript and object-oriented programming."
            }
        };

            // Add all previously saved messages from THIS conversation to the context.
            foreach (var msg in oldMessages)
            {
                messages.Add(new Message
                {
                    role = msg.Role,
                    content = msg.Content
                });
            }

            // ========== STEP 76: Save user message WITH ConversationId ==========
            // Store the user's message in the database, associating it with the current conversation.
            _dbContext.ChatMessages.Add(new ChatMessage
            {
                Role = "user",
                Content = userMessage,
                ConversationId = conversationId
            });
            _dbContext.SaveChanges();

            // Add the user message to the in-memory list that will be sent to the AI.
            messages.Add(new Message { role = "user", content = userMessage });

            // Prepare the request body for Ollama API.
            var requestBody = new ChatRequest
            {
                model = "llama3",
                stream = false,
                messages = messages
            };

            // Serialize request to JSON.
            var json = JsonConvert.SerializeObject(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // Send the request to the local Ollama server.
            var response = await _httpClient.PostAsync("http://localhost:11434/api/chat", content);
            var responseJson = await response.Content.ReadAsStringAsync();

            // Handle API errors.
            if (!response.IsSuccessStatusCode)
            {
                return "API Error: " + responseJson;
            }

            // Deserialize the AI response.
            var result = JsonConvert.DeserializeObject<ChatResponse>(responseJson);

            // ========== STEP 76 (cont'd): Save assistant message WITH ConversationId ==========
            // Store the AI's reply in the database, linked to the same conversation.
            _dbContext.ChatMessages.Add(new ChatMessage
            {
                Role = "assistant",
                Content = result.message.content,
                ConversationId = conversationId
            });
            _dbContext.SaveChanges();

            // Return only the AI's message content to the caller.
            return result.message.content;
        }
    }
}