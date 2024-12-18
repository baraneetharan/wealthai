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
// builder.Services.AddDbContext<LightContext>(opt =>
//     opt.UseInMemoryDatabase("LightList"));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add the chat client
IChatClient innerChatClient = new ChatCompletionsClient(
    endpoint: new Uri("https://models.inference.ai.azure.com"),
    new AzureKeyCredential(githubKey))
    .AsChatClient("gpt-4o-mini");

// IChatClient innerChatClient = 
//     new OllamaChatClient(new Uri("http://localhost:11434/"), "llama3");

builder.Services.AddChatClient(chatClientBuilder => chatClientBuilder
    .UseFunctionInvocation()
    .UseLogging()
    .Use(innerChatClient));

// Register embedding generator
builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
    new AzureOpenAIClient(new Uri("https://models.inference.ai.azure.com"),
        new AzureKeyCredential(githubKey))
        .AsEmbeddingGenerator(modelId: "text-embedding-3-large"));

// builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(provider =>
//     new OllamaEmbeddingGenerator(new Uri("http://localhost:11434/"), "all-minilm"));

builder.Services.AddLogging(loggingBuilder =>
    loggingBuilder.AddConsole().SetMinimumLevel(LogLevel.Debug));

// Register WealthService with its dependency
builder.Services.AddSingleton<WealthService>(sp =>
    new WealthService(sp.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>()));

var wealthService = builder.Services.BuildServiceProvider().GetRequiredService<WealthService>();

var storeVectorsTool = AIFunctionFactory.Create(wealthService.StoreVectors);
var searchVectorsTool = AIFunctionFactory.Create(wealthService.SearchVectors);
var directAnswerTool = AIFunctionFactory.Create(wealthService.DirectAnswer);

var chatOptions = new ChatOptions
{
    Tools = new[]
    {
        directAnswerTool,
        storeVectorsTool,
        searchVectorsTool
    }
};

builder.Services.AddSingleton(chatOptions);

var app = builder.Build();
app.Services.GetRequiredService<WealthService>();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseStaticFiles(); // Enable serving static files
app.UseRouting(); // Must come before UseEndpoints
app.UseAuthorization();
app.MapControllers();
// Serve index.html as the default page 
app.MapFallbackToFile("index.html");

app.Run();
