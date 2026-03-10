using Azure.AI.Language.Text;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;
using System.ComponentModel;

var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
var model = "gpt-5-nano";
var client = new OpenAIClient(apiKey);
ChatClient chatClient = client.GetChatClient(model);
string prompt = string.Empty;
var connectionString = Environment.GetEnvironmentVariable("AZURE_COSMOSDB_CONN");
var textAnalysisUri = Environment.GetEnvironmentVariable("TEXT_ANALYSIS_URI");
var textAnalysisKey = Environment.GetEnvironmentVariable("TEXT_ANALYSIS_KEY");

TextAnalysisClient textAnalysisClient = new(new Uri(textAnalysisUri), new Azure.AzureKeyCredential(textAnalysisKey));

var skillsProvider = new FileAgentSkillsProvider(
    skillPath: Path.Combine(AppContext.BaseDirectory, "skills"));
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


ChatClientAgentOptions agentOptions = new()
{
    Name = "Agente experto en gastos y reembolsos",
    //ChatHistoryProvider = historyProvider,
    AIContextProviders = [skillsProvider, sentimentAdaptionProvider],
    ChatOptions = new ChatOptions()
    {
        Instructions = """
            Eres un agente que ayuda a responder preguntas acerca de las 
            políticas de viaje de la empresa. No contestes nada relacionado a otra cosa.
            No sugieras ni preguntes nada más. Contesta de forma concisa y concreta.
            Usa las herramientas que tengas disponibles. No uses tu conocimiento base.
            """,
        Tools = [ AIFunctionFactory.Create(GetAllowancePerDay),
                  AIFunctionFactory.Create(get_hotel_budget)
                ],
        MaxOutputTokens = 1000,
        RawRepresentationFactory = _ => new ChatCompletionOptions()
        {
            ReasoningEffortLevel = ChatReasoningEffortLevel.Low
        }
    }
};

ChatClientAgent aiAgent = new(chatClient.AsIChatClient(), agentOptions);

var session = await aiAgent.CreateSessionAsync();

while (true)
{
    Console.WriteLine("Prompt:");
    prompt = Console.ReadLine();

    var message = new Microsoft.Extensions.AI.ChatMessage(ChatRole.User, prompt);

    await foreach (var item in aiAgent.RunStreamingAsync(message, session: session))
    {
        Console.Write(item.Text);

        /*if (item.Contents.Any())
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
        }*/
    }
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