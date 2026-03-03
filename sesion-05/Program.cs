using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;
using System.ComponentModel;

var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
var model = "gpt-5-nano";
var client = new OpenAIClient(apiKey);
ChatClient chatClient = client.GetChatClient(model);
var prompt = "cual es el presupuesto de viáticos y hotel para un viaje a Madrid";

ChatClientAgent aiAgent = new(chatClient.AsIChatClient(),
    name: "Mi primer agente",
    instructions: """
            Eres un agente que ayuda a responder preguntas acerca de las 
            políticas de viaje de la empresa. No contestes nada relacionado a otra cosa.
            No sugieras ni preguntes nada más. Contesta de forma concisa y concreta.
            Usa las herramientas que tengas disponibles. No uses tu conocimiento base.
            """,
    tools: [ AIFunctionFactory.Create(GetAllowancePerDay),
             AIFunctionFactory.Create(get_hotel_budget)        
           ]);



var message = new Microsoft.Extensions.AI.ChatMessage(ChatRole.User, prompt);

var chatOptions = new Microsoft.Extensions.AI.ChatOptions()
{
    MaxOutputTokens = 1000,
    RawRepresentationFactory = _ => new ChatCompletionOptions()
    {
        ReasoningEffortLevel = ChatReasoningEffortLevel.Minimal
    }
};

var options = new ChatClientAgentRunOptions(chatOptions);

await foreach (var item in aiAgent.RunStreamingAsync(message, options: options))
{
    if (item.Contents.Any())
    {
        Console.Write(item.Contents[0]);

        if (item.Contents[0] is FunctionCallContent functionCallContent)
        {
            foreach (FunctionCallContent fcc in item.Contents)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine(fcc.CallId);
                Console.WriteLine(fcc.Name);
                foreach (var a in fcc.Arguments)
                {
                    Console.WriteLine($"{a.Key} - {a.Value}");
                }
                Console.ForegroundColor = ConsoleColor.Gray;
            }
        }

        if (item.Contents[0] is FunctionResultContent functionResultContent)
        {
            foreach (FunctionResultContent frc in item.Contents)
            {
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine(frc.CallId);
                Console.WriteLine(frc.Result);
                Console.ForegroundColor = ConsoleColor.Gray;
            }
        }

        if (item.Contents[0] is UsageContent usageContent)
        {
            PrintUsage(usageContent.Details);
        }
    }
}
void PrintUsage(UsageDetails usage)
{
    Console.WriteLine();
    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"Total token count: {usage.TotalTokenCount}");
    Console.WriteLine($"Input token count: {usage.InputTokenCount}");
    Console.WriteLine($"Output token count: {usage.OutputTokenCount}");
    Console.WriteLine($"Reasoning token count: {usage.ReasoningTokenCount}");
    Console.ForegroundColor = ConsoleColor.Gray;
}

Money get_hotel_budget(string city)
{
    return city.ToLowerInvariant() == "aguascalientes" ? new Money(75m) : new Money(150m);
}

[Description("Regresa el presupuesto de viáticos por día para un viaje a la ciudad especificada.")]
Money GetAllowancePerDay([Description("El nombre de la ciudad.")] string city)
{
    return city.ToLowerInvariant() == "aguascalientes" ? new Money(20m) : new Money(100m);
}


record Money(decimal Amount, string Currency = "USD");