using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;

var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
var model = "gpt-5-nano";
var client = new OpenAIClient(apiKey);
ChatClient chatClient = client.GetChatClient(model);
var prompt = "Cuales son las 10 ciudades mas grandes del mundo? Responde de forma concreta.";
/*
ChatClientAgent aiAgent = 
    chatClient.AsAIAgent(name: "Mi primer agente", 
    instructions: "Eres un agente muy útil.");*/

ChatClientAgent aiAgent = new(chatClient.AsIChatClient(),
    name: "Mi primer agente",
    instructions: "Eres un agente muy útil.");

var message = new Microsoft.Extensions.AI.ChatMessage(ChatRole.User,
                                            prompt);


var chatOptions = new Microsoft.Extensions.AI.ChatOptions()
{
    MaxOutputTokens = 1000,
    RawRepresentationFactory = _ => new ChatCompletionOptions()
    {
        ReasoningEffortLevel = ChatReasoningEffortLevel.Minimal
    }
};

var options = new ChatClientAgentRunOptions(chatOptions);

AgentResponse response = await aiAgent.RunAsync(message, options: options);

Console.WriteLine(response.Text);
PrintUsage(response.Usage!);

void PrintUsage(UsageDetails usage)
{
    Console.WriteLine();
    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"Total token count: {usage.TotalTokenCount}");
    Console.WriteLine($"Input token count: {usage.InputTokenCount}");
    Console.WriteLine($"Output token count: {usage.OutputTokenCount}");
    Console.WriteLine($"Reasoning token count: {usage.ReasoningTokenCount}");
    Console.ForegroundColor = ConsoleColor.DarkGray;
}