using Azure.AI.Language.Text;
using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Responses;
using OpenTelemetry;
using OpenTelemetry.Trace;
using System.ClientModel;
using System.ComponentModel;
using System.Text.Json;


var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
var model = "gpt-5-nano";
var client = new OpenAIClient(apiKey);


//ChatClient chatClient = client.GetChatClient(model);
ResponsesClient responsesClient = client.GetResponsesClient();
var conversationClient = client.GetConversationClient();
ClientResult conversation = await conversationClient
                    .CreateConversationAsync(BinaryContent.Create(BinaryData.FromString("{}")));
var conversationId = JsonDocument.Parse(conversation.GetRawResponse().ContentStream)
    .RootElement
    .GetProperty("id")
    .GetString();


string prompt = string.Empty;
var connectionString = Environment.GetEnvironmentVariable("AZURE_COSMOSDB_CONN");
var textAnalysisUri = Environment.GetEnvironmentVariable("TEXT_ANALYSIS_URI");
var textAnalysisKey = Environment.GetEnvironmentVariable("TEXT_ANALYSIS_KEY");
var azureAppInsightsConn = Environment.GetEnvironmentVariable("AZURE_APPINSIGHTS_CONN");

TextAnalysisClient textAnalysisClient = new(new Uri(textAnalysisUri), new Azure.AzureKeyCredential(textAnalysisKey));

/*var fileBasedSkillsProvider = new AgentSkillsProvider(
    skillPath: Path.Combine(AppContext.BaseDirectory, "skills"));*/
var sentimentAdaptionProvider = new SentimentAdaptionProvider(textAnalysisClient);

var historyProvider = new CosmosChatHistoryProvider(
      connectionString,
      databaseId: "agents",
      containerId: "history",
      stateInitializer: session => new CosmosChatHistoryProvider.State(
          conversationId: "conv-123",
          tenantId: "tenant-a",
          userId: "user-1"
 ));


var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddConsole()
           .AddFilter("*", LogLevel.Information)
           .AddFilter("Microsoft.Agents.AI.Compaction", LogLevel.Information);
});

//var compactionStrategy = 
//    new SummarizationCompactionStrategy(chatClient.AsIChatClient(),
//                    CompactionTriggers.TokensExceed(3000));


//var compactionProvider = new CompactionProvider(compactionStrategy, loggerFactory: loggerFactory);

/*// Inline skill
var inlineSkill = new AgentInlineSkill(
    name: "currency-to-points-converter", 
    description: "Convierte el valor de una divisa a puntos.", 
    instructions: "Usa los recursos.")
      .AddResource("conversion-table", """
            | Currency Range | Points Conversion |
              |----------------|------------------|
              | 0 – 200        | 1.7              |
              | 201 – 500      | 1.8              |
              | 501 – 1000     | 2.0              |
      """)
      .AddResource("conversion-policy", () => $"Generated at {DateTime.UtcNow:O}")
      .AddScript("convert", (double value, double factor) =>
          JsonSerializer.Serialize(new { result = value * factor }));

var skillsProvider = new AgentSkillsProvider(inlineSkill);*/

/*await using var mcpClient = await McpClient.CreateAsync(
    new StdioClientTransport(new()
{
    Name = "MCPServer",
    Command = "npx",
    Arguments = ["-y", "--verbose", "@modelcontextprotocol/server-github"],
}));*/

await using McpClient mcpClient = await McpClient.CreateAsync(new HttpClientTransport(new()
{
    Endpoint = new Uri("https://frmcp-hbgpabc8degjc4ce.westus-01.azurewebsites.net/mcp"),
    Name = "FabuRobotics MCP",
}));


IList<McpClientTool> mcpTools = await mcpClient.ListToolsAsync();

var skillsProvider = new AgentSkillsProvider(new CurrencyToPointConverterSkill());

ChatClientAgentOptions agentOptions = new()
{
    Name = "Agente experto en FabuRobotics",
    //ChatHistoryProvider = historyProvider,
    //AIContextProviders = [skillsProvider, sentimentAdaptionProvider],
    ChatOptions = new ChatOptions()
    {
        Instructions = """
            Eres un agente super útil.
            """,
        Tools = [   
                    /*AIFunctionFactory.Create(GetAllowancePerDay),
                    new ApprovalRequiredAIFunction(AIFunctionFactory.Create(get_hotel_budget)),
                    new HostedWebSearchTool(),
                    new HostedCodeInterpreterTool()*/
                    .. mcpTools
                ],
        MaxOutputTokens = 20000,
        Reasoning = new ReasoningOptions() {  Effort = ReasoningEffort.Low },
        /*RawRepresentationFactory = _ => new ChatCompletionOptions()
        {
            ReasoningEffortLevel = ChatReasoningEffortLevel.None
        }*/
    }
};




var sourceName = "myfirstagent";

var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddSource(sourceName)
    .AddAzureMonitorTraceExporter(options =>
    {
        options.ConnectionString = azureAppInsightsConn;
    })
    .Build();


ChatClientAgent theAgent = responsesClient
                            .AsAIAgent(options: agentOptions, model: model);

//ChatClientAgent theAgent 
//    = new(responsesClient.AsIChatClientWithStoredOutputDisabled(model), 
//    agentOptions);

/*var aiAgent = theAgent.AsBuilder()
                      .UseLogging(loggerFactory: loggerFactory)
                      .UseOpenTelemetry(sourceName)
                      .Build();*/


var session = await theAgent.CreateSessionAsync(conversationId);

while (true)
{
    Console.WriteLine("Prompt:");
    prompt = Console.ReadLine();

    var message = new Microsoft.Extensions.AI.ChatMessage(ChatRole.User, prompt);

    await foreach (var item in theAgent.RunStreamingAsync(message, session: session))
    {
        Console.Write(item.Text);

        if (item.Contents.Any())
        {
            if (item.Contents[0] is ToolApprovalRequestContent toolApprovalRequestContent)
            {
                Console.WriteLine($"'{toolApprovalRequestContent.ToolCall.CallId}' requiere aprobación.");
                Console.WriteLine("¿Aprobar? S/N");
                var approvalResponse = Console.ReadLine();
                bool approved = false;
                approved = approvalResponse.ToLowerInvariant() == "s";
                
                var toolApprovalResponseContent
                        = new ToolApprovalResponseContent(toolApprovalRequestContent.RequestId, approved,
                        toolApprovalRequestContent.ToolCall);

                var newChatMessage = new Microsoft.Extensions.AI.ChatMessage();
                newChatMessage.Contents.Add(toolApprovalResponseContent);
                newChatMessage.Contents.Add(new TextContent(approved ? "" : "No lo aprobó el usuario"));

                await foreach (var item2 in theAgent.RunStreamingAsync(newChatMessage, session: session))
                {
                    Console.Write(item2.Text);

                    ReportFunctionCallContent(item2);
                    ReportFunctionResultContent(item2);
                    ReportUsage(item2);
                }
            }

            ReportFunctionCallContent(item);
            ReportFunctionResultContent(item);
            ReportUsage(item);
        }
    }
    //tracerProvider.ForceFlush(5000);
    Console.WriteLine();
    Console.WriteLine();
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

void ReportUsage(AgentResponseUpdate item)
{
    if (item.Contents.Any() && item.Contents[0] is UsageContent usageContent)
    {
        PrintUsage(usageContent.Details);
    }
}

void ReportFunctionCallContent(AgentResponseUpdate item)
{
    if (item.Contents.Any() 
            && item.Contents[0] is FunctionCallContent functionCallContent)
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
}

void ReportFunctionResultContent(AgentResponseUpdate item)
{
    if (item.Contents.Any() 
        && item.Contents[0] is FunctionResultContent functionResultContent)
    {
        foreach (FunctionResultContent frc in item.Contents)
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine(frc.CallId);
            Console.WriteLine(frc.Result);
            Console.ForegroundColor = ConsoleColor.Gray;
        }
    }
}

record Money(decimal Amount, string Currency = "USD");

public class SentimentAdaptionProvider(TextAnalysisClient textAnalysisClient) : AIContextProvider
{
    protected override ValueTask<AIContext> ProvideAIContextAsync(InvokingContext context, 
        CancellationToken cancellationToken = default)
    {
        string sentiment = "neutral";

        if (context.Session.StateBag.TryGetValue(StateKeys[0], out string storedSentiment) == true)
        {
            sentiment = storedSentiment;
        }

        var instructions = sentiment switch
        {
            "negative" => "El usuario está enojado. Sé empático y conciso. Nada de bromas.",
            "mixed" => "El usuario tiene sentimientos encontrados. Ayúdalo y reconoce sus sentimientos.",
            "positive" => "El usuario está de buen humor.  Debes bromear y usar emojis.",
            _ => null
        };

        return new ValueTask<AIContext>(new AIContext() { Instructions = instructions });
    }

    protected override async ValueTask StoreAIContextAsync(InvokedContext context, 
        CancellationToken cancellationToken = default)
    {
        var lastUserMessage = context.RequestMessages.LastOrDefault(m => m.Role == ChatRole.User)?.Text;

        if (string.IsNullOrWhiteSpace(lastUserMessage))
        {
            return;
        }

        var sentimentInput = new TextSentimentAnalysisInput()
        {
            TextInput = new MultiLanguageTextInput()
            {
                MultiLanguageInputs =
                 {
                     new MultiLanguageInput("1", lastUserMessage)
                 }
            }
        };

        var result = await textAnalysisClient.AnalyzeTextAsync(sentimentInput,
            cancellationToken: cancellationToken);

        if (result.Value is AnalyzeTextSentimentResult sentimentResult)
        {
            var sentimentText = sentimentResult.Results.Documents.First().Sentiment.ToString();
            context.Session.StateBag.SetValue(StateKeys[0], sentimentText.ToLowerInvariant());
        }
    }
}