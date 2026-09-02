using System.Threading.Tasks;

namespace MyAIAgent.Services
{
    /// <summary>
    /// A named capability the chat layer can invoke. Implementations do I/O
    /// (HTTP, DB, filesystem), so the contract is asynchronous — callers must
    /// never block on it.
    /// </summary>
    public interface ITool
    {
        string Name { get; }

        Task<string> ExecuteAsync(string input);
    }
}
