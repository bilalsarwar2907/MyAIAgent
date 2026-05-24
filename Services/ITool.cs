using System;
using System.Collections.Generic;
using System.Text;

namespace MyAIAgent.Services
{
    public interface ITool
    {
        string Name { get; }

        string Execute(string input);
    }
}
