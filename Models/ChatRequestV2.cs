using System;

namespace MyAIAgent.Models
{
    public class ChatRequestV2
    {
        public string ConversationId { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
    }
}
