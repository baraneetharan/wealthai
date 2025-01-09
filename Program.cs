using Azure;
using Azure.AI.Inference;
using Azure.AI.OpenAI;
using DotNetEnv;
using Microsoft.Extensions.AI;
using wealthai;

// Get keys from configuration
Env.Load(".env");
string githubKey = Env.GetString("GITHUB_KEY");

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add the chat client as Singleton
IChatClient innerChatClient = new ChatCompletionsClient(
    endpoint: new Uri("https://models.inference.ai.azure.com"),
    new AzureKeyCredential(githubKey))
    .AsChatClient("gpt-4o-mini");

builder.Services.AddSingleton<IChatClient>(provider => innerChatClient);

builder.Services.AddChatClient(chatClientBuilder => chatClientBuilder
    .UseFunctionInvocation()
    .Use(innerChatClient));

// Register embedding generator
builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
    new AzureOpenAIClient(new Uri("https://models.inference.ai.azure.com"),
        new AzureKeyCredential(githubKey))
        .AsEmbeddingGenerator(modelId: "text-embedding-3-large"));

builder.Services.AddLogging(loggingBuilder =>
    loggingBuilder.AddConsole().SetMinimumLevel(LogLevel.Debug));

// Register services
builder.Services.AddSingleton<WealthService>();
builder.Services.AddSingleton<WealthServiceWoLLM>();    
builder.Services.AddSingleton<WealthServicefromDB>();

// Register WealthAgent as scoped
builder.Services.AddScoped<WealthAgent>();

// Add WealthAgentFactory as singleton
builder.Services.AddSingleton<WealthAgentFactory>();

// Register chat options before building the service provider
var chatOptions = new ChatOptions
{
    Tools = new[]
    {
        AIFunctionFactory.Create((WealthServicefromDB ws) => ws.AnswerFromDB),
        // Add other tools here as needed
    }
};

builder.Services.AddSingleton(chatOptions);

// Build the service provider once all services are registered
var app = builder.Build();
var serviceProvider = app.Services;

// Get service instances
var wealthService = serviceProvider.GetRequiredService<WealthService>();
var wealthServiceWoLLM = serviceProvider.GetRequiredService<WealthServiceWoLLM>();
var wealthServicefromDB = serviceProvider.GetRequiredService<WealthServicefromDB>();
var wealthAgentFactory = serviceProvider.GetRequiredService<WealthAgentFactory>();
var wealthAgent = wealthAgentFactory.Create();

// Initialize agent goals
wealthAgent.AddGoal("Maximize portfolio returns", 1.0);
wealthAgent.AddGoal("Minimize transaction costs", 0.8);
wealthAgent.AddGoal("Ensure regulatory compliance", 0.9);
wealthAgent.AddGoal("Optimize client satisfaction", 0.95);

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();
app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();
