
```c#
Load CSV data into Vectors from LoadCSVVectorsInDB
```

## 1

```c#
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
// IChatClient innerChatClient = new ChatCompletionsClient(
//     endpoint: new Uri("https://models.inference.ai.azure.com"),
//     new AzureKeyCredential(githubKey))
//     .AsChatClient("gpt-4o-mini");

IChatClient innerChatClient = 
    new OllamaChatClient(new Uri("http://localhost:11434/"), "llama3");    

builder.Services.AddChatClient(chatClientBuilder => chatClientBuilder
    .UseFunctionInvocation()
    .UseLogging()
    .Use(innerChatClient));

// Register embedding generator
// builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
//     new AzureOpenAIClient(new Uri("https://models.inference.ai.azure.com"),
//         new AzureKeyCredential(githubKey))
//         .AsEmbeddingGenerator(modelId: "text-embedding-3-large"));

builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(provider =>
    new OllamaEmbeddingGenerator(new Uri("http://localhost:11434/"), "all-minilm"));


builder.Services.AddLogging(loggingBuilder =>
    loggingBuilder.AddConsole().SetMinimumLevel(LogLevel.Debug));

var wealthService = new WealthService();

// var storeVectorsInDBTool = AIFunctionFactory.Create(wealthService.StoreVectorsInDB);
var readCsvDataTool = AIFunctionFactory.Create(wealthService.ReadCsvData);


var chatOptions = new ChatOptions
{
    Tools = new[]
    {
        readCsvDataTool,
        // storeVectorsInDBTool
        }
};

builder.Services.AddSingleton(wealthService);
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


```

```c#
using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.AI;
using Npgsql;

namespace wealthai
{
    // using Microsoft.SemanticKernel.Connectors.InMemory;

    public class WealthService
    {
        // List<MyTransaction> transactions;


        public async Task StoreVectorsInDB(IEmbeddingGenerator<string, Embedding<float>> generator)
        {
            string filePath = @"D:\baraneetharan\myworks\SemanticKernel\meai\wealthai\TransactionReport5.csv";
            string connectionString = "Host=localhost;Username=postgres;Password=Kgisl@12345;Database=riche";

            Console.WriteLine("WealthService StoreVectors start processing...");
            await CreateTableIfNotExists();

            using var reader = new StreamReader(filePath);
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture));
            var transactions = csv.GetRecords<MyTransaction>().ToList();

            using (var connection = new NpgsqlConnection(connectionString))
            {
                await connection.OpenAsync();

                foreach (var transaction in transactions)
                {
                    var vector = await generator.GenerateEmbeddingVectorAsync(transaction.Description);
                    transaction.Vector = new ReadOnlyMemory<float>(vector.ToArray());

                    // Store movie data and vector in PostgreSQL
                    var command = new NpgsqlCommand("INSERT INTO MyTransaction (TransactionDate, PortfolioCode, PortfolioName, FamilyName, FolioNo, RTASchemeCode, SchemeName, Units, TransactionType, TransactionAmount, CostPerUnit, Load, STT, StampDuty, Product, RMName, Description, Vector) VALUES (@TransactionDate, @PortfolioCode, @PortfolioName, @FamilyName, @FolioNo, @RTASchemeCode, @SchemeName, @Units, @TransactionType, @TransactionAmount, @CostPerUnit, @Load, @STT, @StampDuty, @Product, @RMName, @Description, @Vector)", connection);

                    command.Parameters.AddWithValue("@TransactionDate", transaction.TransactionDate);
                    command.Parameters.AddWithValue("@PortfolioCode", transaction.PortfolioCode ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@PortfolioName", transaction.PortfolioName ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@FamilyName", transaction.FamilyName ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@FolioNo", transaction.FolioNo ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@RTASchemeCode", transaction.RTASchemeCode ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@SchemeName", transaction.SchemeName ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@Units", transaction.Units);
                    command.Parameters.AddWithValue("@TransactionType", transaction.TransactionType ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@TransactionAmount", transaction.TransactionAmount);
                    command.Parameters.AddWithValue("@CostPerUnit", transaction.CostPerUnit);
                    command.Parameters.AddWithValue("@Load", transaction.Load);
                    command.Parameters.AddWithValue("@STT", transaction.STT);
                    command.Parameters.AddWithValue("@StampDuty", transaction.StampDuty);
                    command.Parameters.AddWithValue("@Product", transaction.Product ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@RMName", transaction.RMName ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@Description", transaction.Description);
                    command.Parameters.AddWithValue("@Vector", transaction.Vector.ToArray());

                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        private async Task CreateTableIfNotExists()
        {
            string connectionString = "Host=localhost;Username=postgres;Password=Kgisl@12345;Database=riche";

            using (var connection = new NpgsqlConnection(connectionString))
            {
                await connection.OpenAsync();

                var command = new NpgsqlCommand(
                    @"CREATE TABLE MyTransaction (
    TransactionDate DATE,
    PortfolioCode VARCHAR(255),
    PortfolioName VARCHAR(255),
    FamilyName VARCHAR(255),
    FolioNo VARCHAR(255),
    RTASchemeCode VARCHAR(255),
    SchemeName VARCHAR(255),
    Units DECIMAL,
    TransactionType VARCHAR(255),
    TransactionAmount DECIMAL,
    CostPerUnit DECIMAL,
    Load DECIMAL,
    STT DECIMAL,
    StampDuty DECIMAL,
    Product VARCHAR(255),
    RMName VARCHAR(255),
    Description TEXT,
    Vector FLOAT8[]
                )", connection);

                await command.ExecuteNonQueryAsync();
            }
        }

        public List<MyTransaction> ReadCsvData()
        {
            Console.WriteLine("WealthService ReadCsvData start processing...");

            using var reader = new StreamReader(@"D:\baraneetharan\myworks\SemanticKernel\meai\wealthai\TransactionReport5.csv");
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture));
            return csv.GetRecords<MyTransaction>().ToList();
        }

        public void GetAnswer()
        {
            Console.WriteLine("WealthService GetAnswer is working ...");
        }
    }
}
```