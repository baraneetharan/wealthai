## 20-12-2024 working code
```c#
using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.AI;
using Npgsql;
using Azure;
using Azure.AI.Inference;
using Azure.AI.OpenAI;

namespace wealthai
{
    // using Microsoft.SemanticKernel.Connectors.InMemory;
    public class WealthService
    {
        private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;

        public WealthService(IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator)
        {
            _embeddingGenerator = embeddingGenerator;
        }

        // List<MyTransaction> transactions;
        public async Task StoreVectors()
        {
            string filePath = @"D:\baraneetharan\myworks\SemanticKernel\meai\wealthai\TransactionReport500.csv";
            string connectionString = "Host=localhost;Username=postgres;Password=Kgisl@12345;Database=wealthai";

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
                    var vector = await _embeddingGenerator.GenerateEmbeddingVectorAsync(transaction.Description);
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

        public async Task<string> SearchVectors(string query)
        {
            string connectionString = "Host=localhost;Username=postgres;Password=Kgisl@12345;Database=wealthai";

            using (var connection = new NpgsqlConnection(connectionString))
            {
                await connection.OpenAsync();

                // Generate embedding vector
                var queryEmbedding = await _embeddingGenerator.GenerateEmbeddingVectorAsync(query);
                double[] queryVectorArray = Array.ConvertAll(queryEmbedding.Span.ToArray(), item => (double)item);

                List<(DateTime TransactionDate, string PortfolioCode, string PortfolioName, string FamilyName, string FolioNo, string RTASchemeCode, string SchemeName, decimal Units, string TransactionType, decimal TransactionAmount, decimal CostPerUnit, decimal Load, decimal STT, decimal StampDuty, string Product, string RMName, string Description, double[] Vector)> records = new List<(DateTime, string, string, string, string, string, string, decimal, string, decimal, decimal, decimal, decimal, decimal, string, string, string, double[])>();

                var cmdText = "SELECT TransactionDate, PortfolioCode, PortfolioName, FamilyName, FolioNo, RTASchemeCode, SchemeName, Units, TransactionType, TransactionAmount, CostPerUnit, Load, STT, StampDuty, Product, RMName, Description, Vector FROM MyTransaction";
                using (var cmd = new NpgsqlCommand(cmdText, connection))
                {
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            records.Add((
                                reader.GetDateTime(0),
                                reader.GetString(1),
                                reader.GetString(2),
                                reader.GetString(3),
                                reader.GetString(4),
                                reader.GetString(5),
                                reader.GetString(6),
                                reader.GetDecimal(7),
                                reader.GetString(8),
                                reader.GetDecimal(9),
                                reader.GetDecimal(10),
                                reader.GetDecimal(11),
                                reader.GetDecimal(12),
                                reader.GetDecimal(13),
                                reader.GetString(14),
                                reader.GetString(15),
                                reader.GetString(16),
                                Array.ConvertAll(reader.GetFieldValue<float[]>(17), item => (double)item)
                            ));
                        }
                    }
                }

                var results = records
                    .Select(r => new
                    {
                        r.TransactionDate,
                        r.PortfolioCode,
                        r.PortfolioName,
                        r.FamilyName,
                        r.FolioNo,
                        r.RTASchemeCode,
                        r.SchemeName,
                        r.Units,
                        r.TransactionType,
                        r.TransactionAmount,
                        r.CostPerUnit,
                        r.Load,
                        r.STT,
                        r.StampDuty,
                        r.Product,
                        r.RMName,
                        r.Description,
                        Distance = CalculateDistance(queryVectorArray, r.Vector)
                    })
                    .OrderBy(r => r.Distance)
                    .ToList();

                var response = "Here are the top results:\n";
                foreach (var result in results)
                {
                    // response += $"Transaction Date: {result.TransactionDate}, Portfolio Code: {result.PortfolioCode}, Portfolio Name: {result.PortfolioName}, Family Name: {result.FamilyName}, Folio No: {result.FolioNo}, RTA Scheme Code: {result.RTASchemeCode}, Scheme Name: {result.SchemeName}, Units: {result.Units}, Transaction Type: {result.TransactionType}, Transaction Amount: {result.TransactionAmount}, Cost Per Unit: {result.CostPerUnit}, Load: {result.Load}, STT: {result.STT}, Stamp Duty: {result.StampDuty}, Product: {result.Product}, RM Name: {result.RMName}, Description: {result.Description}, Distance: {result.Distance}\n";
                    response += $"Description: {result.Description}\n";

                }

                // Console.WriteLine(response);
                return response;
            }
        }

        // Utility method to calculate cosine similarity (if needed)
        private double CalculateDistance(double[] vectorA, double[] vectorB)
        {
            if (vectorA.Length != vectorB.Length)
                throw new ArgumentException("Vectors must be of the same length.");

            double dotProduct = 0;
            double magnitudeA = 0;
            double magnitudeB = 0;

            for (int i = 0; i < vectorA.Length; i++)
            {
                dotProduct += vectorA[i] * vectorB[i];
                magnitudeA += vectorA[i] * vectorA[i];
                magnitudeB += vectorB[i] * vectorB[i];
            }

            if (magnitudeA == 0 || magnitudeB == 0)
                return 0;

            return dotProduct / (Math.Sqrt(magnitudeA) * Math.Sqrt(magnitudeB));
        }


        private float CosineSimilarity(float[] vectorA, float[] vectorB)
        {
            if (vectorA.Length != vectorB.Length)
                throw new ArgumentException("Vectors must be of same length.");

            float dotProduct = 0;
            float magnitudeA = 0;
            float magnitudeB = 0;

            for (int i = 0; i < vectorA.Length; i++)
            {
                dotProduct += vectorA[i] * vectorB[i];
                magnitudeA += vectorA[i] * vectorA[i];
                magnitudeB += vectorB[i] * vectorB[i];
            }

            if (magnitudeA == 0 || magnitudeB == 0)
                return 0;

            return dotProduct / (float)(Math.Sqrt(magnitudeA) * Math.Sqrt(magnitudeB));
        }

        private async Task CreateTableIfNotExists()
        {
            string connectionString = "Host=localhost;Username=postgres;Password=Kgisl@12345;Database=wealthai";
            using (var connection = new NpgsqlConnection(connectionString))
            {
                await connection.OpenAsync();
                var command = new NpgsqlCommand(
                    @"CREATE TABLE IF NOT EXISTS MyTransaction (
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

        public List<MyTransaction> DirectAnswer()
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

## 09-01-2025
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
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add the chat client
IChatClient innerChatClient = new ChatCompletionsClient(
    endpoint: new Uri("https://models.inference.ai.azure.com"),
    new AzureKeyCredential(githubKey))
    .AsChatClient("gpt-4o-mini");

// Register the chat client as singleton
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

// Register WealthAgent as a singleton
builder.Services.AddSingleton<WealthAgent>();

// Build the service provider once
var app = builder.Build();
var serviceProvider = app.Services;

// Get service instances
var wealthService = serviceProvider.GetRequiredService<WealthService>();
var wealthServiceWoLLM = serviceProvider.GetRequiredService<WealthServiceWoLLM>();
var wealthServicefromDB = serviceProvider.GetRequiredService<WealthServicefromDB>();
var wealthAgent = serviceProvider.GetRequiredService<WealthAgent>();

// Initialize agent goals
wealthAgent.AddGoal("Maximize portfolio returns", 1.0);
wealthAgent.AddGoal("Minimize transaction costs", 0.8);
wealthAgent.AddGoal("Ensure regulatory compliance", 0.9);
wealthAgent.AddGoal("Optimize client satisfaction", 0.95);

// Create tools
var storeVectorsTool = AIFunctionFactory.Create(wealthService.StoreVectors);
var searchVectorsTool = AIFunctionFactory.Create(wealthService.SearchVectors);
var directAnswerTool = AIFunctionFactory.Create(wealthService.DirectAnswer);

var getUniqueClientsTool = AIFunctionFactory.Create(wealthServiceWoLLM.GetUniqueClients);
var getRMNamesTool = AIFunctionFactory.Create(wealthServiceWoLLM.GetRMNames);
var getClientRMNamesTool = AIFunctionFactory.Create(wealthServiceWoLLM.GetClientRMNames);
var getAssestwiseTotalTool = AIFunctionFactory.Create(wealthServiceWoLLM.GetAssestwiseTotal);
var getTopClientTool = AIFunctionFactory.Create(wealthServiceWoLLM.GetTopClient);

var answerFromDBTool = AIFunctionFactory.Create(wealthServicefromDB.AnswerFromDB);

// Register chat options
var chatOptions = new ChatOptions
{
    Tools = new[]
    {
        answerFromDBTool,
        // directAnswerTool,
        // storeVectorsTool,
        // searchVectorsTool,
        // getUniqueClientsTool,
        // getRMNamesTool,
        // getClientRMNamesTool,
        // getAssestwiseTotalTool,
        // getTopClientTool
    }
};

builder.Services.AddSingleton(chatOptions);

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
```