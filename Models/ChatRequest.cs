using System;
using System.Collections.Generic;
using System.Text;

namespace MyAIAgent.Models
{
    // Wire models for the Ollama /api/chat endpoint (lower-case names match its JSON).
    public class ChatRequest
    {
        public string model { get; set; } = "";
        public List<Message> messages { get; set; } = new();
        public bool stream { get; set; }
    }

    public class Message
    {
        public string role { get; set; } = "";

        // Nullable on purpose: AIService treats a missing content in Ollama's
        // reply as "empty response" and throws, so it must be able to be null.
        public string? content { get; set; }
    }
}
