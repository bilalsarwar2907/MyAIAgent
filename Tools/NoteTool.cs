using System;
using System.Collections.Generic;
using System.Text;

namespace MyAIAgent.Tools
{
    public class NoteTool
    {
        //save notes permanently.
        private readonly string _notePath = "Memory/notes.txt";

        public void SaveNote(string text)
        {
            File.AppendAllText
            (
                _notePath,
                text + Environment.NewLine
            );
        }
    }
}
