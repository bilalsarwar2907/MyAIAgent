using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MyAIAgent.Models;
using MyAIAgent.Services;
using MyAIAgent.Tools;

var builder = WebApplication.CreateBuilder(args);

// Register services
builder.Services.AddSingleton<AIService>();
builder.Services.AddSingleton<NoteTool>();

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Swagger middleware
app.UseSwagger();
app.UseSwaggerUI();

// Health endpoint
app.MapGet("/health", () =>
    new
    {
        status = "OK",
        time = DateTime.UtcNow
    });

// Chat endpoint
app.MapPost("/chat", async (ChatRequest request, AIService ai, NoteTool noteTool) =>
{
    var userMessage = request.messages?
        .LastOrDefault()?.content;

    if (string.IsNullOrWhiteSpace(userMessage))
    {
        return Results.BadRequest(new
        {
            error = "No user message found"
        });
    }
    if (userMessage.StartsWith("save note:"))
    {
        var note =
            userMessage.Replace("save note:", "").Trim();

        noteTool.SaveNote(note);

        return Results.Ok(new
        {
            message = "Note saved successfully"
        });
    }

    var reply = await ai.AskAI(userMessage);

    return Results.Ok(new ChatResponse
    {
        message = new Message
        {
            role = "assistant",
            content = reply
        }
    });
});

app.Run();






//namespace MyAIAgent
//{
//    internal class Program
//    {
//        static async Task Main(string[] args)
//        {
//            AIService aiService = new AIService();

//            Console.WriteLine("AI Agent Started!");
//            Console.WriteLine("Type 'exit' to close.");
//            Console.WriteLine();

//            while (true)
//            {
//                Console.Write("You: ");

//                string userInput = Console.ReadLine();

//                if (userInput.ToLower() == "exit")
//                {
//                    break;
//                }

//                string response = await aiService.AskAI(userInput);

//                Console.WriteLine();
//                Console.WriteLine("AI: " + response);
//                Console.WriteLine();
//            }
//        }
//    }
//}