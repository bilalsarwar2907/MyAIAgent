using MyAIAgent.Services;

namespace MyAIAgent
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            AIService aiService = new AIService();

            Console.WriteLine("AI Agent Started!");
            Console.WriteLine("Type 'exit' to close.");
            Console.WriteLine();

            while (true)
            {
                Console.Write("You: ");

                string userInput = Console.ReadLine();

                if (userInput.ToLower() == "exit")
                {
                    break;
                }

                string response = await aiService.AskAI(userInput);

                Console.WriteLine();
                Console.WriteLine("AI: " + response);
                Console.WriteLine();
            }
        }
    }
}