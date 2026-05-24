using System;
using System.Collections.Generic;
using System.Text;

namespace MyAIAgent.Models
{
    public class ToolResponse
    {
        public bool UseTool { get; set; }

        public string ToolName { get; set; }

        public string ToolInput { get; set; }
    }
}
