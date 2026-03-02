using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;

var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
var model = "gpt-5-nano";
var client = new OpenAIClient(apiKey);
ChatClient chatClient = client.GetChatClient(model);
var prompt = "quien es Tod Uphill";
/*
ChatClientAgent aiAgent = 
    chatClient.AsAIAgent(name: "Mi primer agente", 
    instructions: "Eres un agente muy útil.");*/

ChatClientAgent aiAgent = new(chatClient.AsIChatClient(),
    name: "Mi primer agente",
    instructions: "Eres un agente muy útil.");



var imageBytes = await File.ReadAllBytesAsync("eye.pdf");
var rom = new ReadOnlyMemory<byte>(imageBytes);

var contents = new List<AIContent>
{
    new TextContent(prompt),
    new DataContent(rom, "application/pdf")
};

var message = new Microsoft.Extensions.AI.ChatMessage(ChatRole.User, contents);

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
        Console.Write(item.Text);

        if (item.Contents[0] is UsageContent usageContent)
        {
            PrintUsage(usageContent.Details);
        }
    }
}


/*if (response.Result.Any())
{
    foreach (var item in response.Result)
    {
        Console.WriteLine($"{item.Name} - {item.Area}");
    }
}

PrintUsage(response.Usage!);*/

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

record LargestCity(string Name, decimal Area);