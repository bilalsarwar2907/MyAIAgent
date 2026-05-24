using MyAIAgent.Models;
using Newtonsoft.Json;
using System.Text;

namespace MyAIAgent.Services
{
    public class AIService
    {
        private readonly HttpClient _httpClient;

        // MEMORY
        private List<Message> _messages = new List<Message>();
        private readonly string _filePath = "Memory/messages.json";

        public AIService()
        {
            _httpClient = new HttpClient();

            // Increase timeout for local AI models
            _httpClient.Timeout = TimeSpan.FromMinutes(10);

            _messages.Add(new Message
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
            });

            if (File.Exists(_filePath))
            {
                var json = File.ReadAllText(_filePath);

                var savedMessages =
                    JsonConvert.DeserializeObject<List<Message>>(json);

                if (savedMessages != null)
                {
                    _messages = savedMessages;
                }
            }
        }

        public async Task<string> AskAI(string userMessage)
        {
            // Save USER message into memory
            _messages.Add(new Message
            {
                role = "user",
                content = userMessage
            });

            var requestBody = new ChatRequest
            {
                model = "llama3",
                stream = false,

                // Send FULL conversation memory
                messages = _messages
            };

            var json = JsonConvert.SerializeObject(requestBody);

            var content = new StringContent
            (
                json,
                Encoding.UTF8,
                "application/json"
            );

            var response = await _httpClient.PostAsync
            (
                "http://localhost:11434/api/chat",
                content
            );

            var responseJson = await response.Content.ReadAsStringAsync();

            Console.WriteLine(responseJson);

            if (!response.IsSuccessStatusCode)
            {
                return "API Error: " + responseJson;
            }

            var result = JsonConvert.DeserializeObject<ChatResponse>(responseJson);

            // Save AI response into memory
            _messages.Add(new Message
            {
                role = "assistant",
                content = result.message.content


            });
            File.WriteAllText
            (
               _filePath,
               JsonConvert.SerializeObject(_messages, Formatting.Indented)
             );

            return result.message.content;
        }
    }
}