using System;
using System.Collections.Generic;
using System.Text;

namespace MyAIAgent.Data
{
    public class ChatMessage
    {
        public int Id { get; set; }
        public string Role { get; set; }= string.Empty;
        public string Content { get; set; } = string.Empty;
    }
}
