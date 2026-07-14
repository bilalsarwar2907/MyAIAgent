using System;

namespace MyAIAgent.Data
{
    public class ChatMessage
    {
        public int Id { get; set; }

        // Which conversation this message belongs to
        public string ConversationId { get; set; } = string.Empty;

        // Which user owns this conversation
        public string UserName { get; set; } = string.Empty;

        public string Role { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;

        // When the message was sent
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}