using MyAIAgent.Services;
using System;
using System.Collections.Generic;
using System.Text;

namespace MyAIAgent.Tools
{
    public class NoteTool : ITool
    {
        public string Name => "SaveNote";

        public string Execute(string input)
        {
            var folder = "Memory";

            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            var path = Path.Combine(folder, "notes.txt");

            File.AppendAllText
            (
                path,
                input + Environment.NewLine
            );

            return "Note saved successfully";
        }
    }
}
