using System.Threading.Tasks;
using MyAIAgent.Services;

namespace MyAIAgent.Tools
{
    public class NoteTool : ITool
    {
        public string Name => "SaveNote";

        public async Task<string> ExecuteAsync(string input)
        {
            var folder = "Memory";

            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            var path = Path.Combine(folder, "notes.txt");

            await File.AppendAllTextAsync(path, input + Environment.NewLine);

            return "Note saved successfully";
        }
    }
}
