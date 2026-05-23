using System;
using System.Collections.Generic;
using System.Text;

namespace MyAIAgent.Models
{
    public class ChatRequest
    {
        public string model { get; set; }
        public List<Message> messages { get; set; }
        public bool stream { get; set; }
    }

    public class Message
    {
        public string role { get; set; }
        public string content { get; set; }
    }
}