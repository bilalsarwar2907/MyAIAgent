using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MyAIAgent.Models;
using MyAIAgent.Services;
using MyAIAgent.Tools;
using MyAIAgent.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=memory.db"));

// Register services
builder.Services.AddScoped<AIService>();
builder.Services.AddSingleton<NoteTool>();
builder.Services.AddSingleton<ITool, NoteTool>();

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
//app.MapPost("/chat", async (ChatRequest request, AIService ai, NoteTool noteTool) =>
//{
//    var userMessage = request.messages?
//        .LastOrDefault()?.content;
//    var lowerMessage = userMessage.ToLower();

//    if (string.IsNullOrWhiteSpace(userMessage))
//    {
//        return Results.BadRequest(new
//        {
//            error = "No user message found"
//        });
//    }
//    if (
//    lowerMessage.Contains("remember") ||
//    lowerMessage.Contains("save this") ||
//    lowerMessage.Contains("note this")
//)
//    {
//        noteTool.SaveNote(userMessage);

//        return Results.Ok(new
//        {
//            message = "AI Agent saved your note automatically."
//        });
//    }

//    var reply = await ai.AskAI(userMessage);

//    return Results.Ok(new ChatResponse
//    {
//        message = new Message
//        {
//            role = "assistant",
//            content = reply
//        }
//    });
//});

app.MapPost("/chat", async (ChatRequestV2 request, AIService ai, IEnumerable<ITool> tools) =>
{
    // Validate inputs
    if (string.IsNullOrWhiteSpace(request.Message))
        return Results.BadRequest("Message is empty");

    if (string.IsNullOrWhiteSpace(request.ConversationId))
        return Results.BadRequest("ConversationId is required");

    // STEP 97 & 98: Dynamic tool decision and execution
    // The AI decides which tool (if any) to use based on the user's message.
    var toolDecision = await ai.DecideTool(request.Message);

    if (toolDecision.UseTool)
    {
        // STEP 98: Replace hardcoded tool name check with dynamic lookup
        // Find the tool that matches the name returned by the AI
        var tool = tools.FirstOrDefault(t => t.Name == toolDecision.ToolName);

        if (tool != null)
        {
            // Execute the found tool with the input provided by the AI
            var result = tool.Execute(toolDecision.ToolInput);

            // Return success response indicating which tool was used
            return Results.Ok(new
            {
                toolUsed = true,
                tool = tool.Name,
                result = result
            });
        }
        // Optional: handle case where tool name is invalid or not found
        // For now, we fall through to the regular AI reply
    }

    // No tool used – get a normal AI response
    var reply = await ai.AskAI(request.Message, request.ConversationId);

    return Results.Ok(new
    {
        conversationId = request.ConversationId,
        message = reply
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