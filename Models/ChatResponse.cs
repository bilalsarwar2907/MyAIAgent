using System;
using System.Collections.Generic;
using System.Text;

namespace MyAIAgent.Models
{
    public class ChatResponse
    {
        // Null when Ollama returns no message; AIService checks for that.
        public Message? message { get; set; }
    }
}
