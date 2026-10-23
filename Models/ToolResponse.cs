using System;
using System.Collections.Generic;
using System.Text;

namespace MyAIAgent.Models
{
    public class ToolResponse
    {
        public bool UseTool { get; set; }

        // Default to "" so a model reply that omits these never yields a null
        // that later blows up in ITool.ExecuteAsync(input.Trim()).
        public string ToolName { get; set; } = "";

        public string ToolInput { get; set; } = "";
    }
}
