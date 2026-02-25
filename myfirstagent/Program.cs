using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;

var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
var model = "gpt-5-nano";
var client = new OpenAIClient(apiKey);
var chatClient = client.GetChatClient(model);
var prompt = "Cuales son las ciudades mas grandes del mundo?";

var aiAgent = 
    chatClient.AsAIAgent(name: "Mi primer agente", 
    instructions: "Eres un agente muy útil.");

var response = await aiAgent.RunAsync(prompt);

Console.WriteLine(response.Text);