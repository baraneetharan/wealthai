using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.AI;
using Npgsql;
using Azure;
using Azure.AI.Inference;
using Azure.AI.OpenAI;
using DotNetEnv;
using System.Text.Json;

namespace wealthai
{
    public class WealthServicefromDB
    {
        private static readonly string _tableSchema = @"
Table: mytransaction
Columns:
- transactiondate (date)
- portfoliocode (varchar)
- portfolioname (varchar)
- familyname (varchar)
- foliono (varchar)
- rtaschemecode (varchar)
- schemename (varchar)
- units (numeric)
- transactiontype (varchar)
- transactionamount (numeric)
- costperunit (numeric)
- load (numeric)
- stt (numeric)
- stampduty (numeric)
- product (varchar)
- rmname (varchar)
- description (text)
- vector (double precision[])
";
        private static readonly string _connectionString = "Host=localhost;Database=riche;Username=postgres;Password=Kgisl@12345";

        public async Task<string> AnswerFromDB(string query)
        {

            Env.Load(".env");
            string githubKey = Env.GetString("GITHUB_KEY");
            // Add the chat client
            IChatClient client =
                new AzureOpenAIClient(
                    new Uri("https://models.inference.ai.azure.com"),
                        new AzureKeyCredential(githubKey))
                        .AsChatClient(modelId: "gpt-4o-mini");
            // Test the chatWithDatabase function
            // "give me the rmname, number of clients order by count in the mytransaction table.";
            // Console.WriteLine("Enter a prompt to test the chatWithDatabase function:");
            // var testPrompt = Console.ReadLine();
            var response = await ChatWithDatabase(client, query);
            // Console.WriteLine(response);
            return response;
        }
        static async Task<string> ChatWithDatabase(IChatClient client, string prompt)
        {
            // Use the model to generate a SQL query
            var response = await client.CompleteAsync($"{_tableSchema}\n{prompt}");
            // Extract the SQL query from the response
            var sqlQuery = ExtractSqlQuery(response.Message.Text);
            Console.WriteLine("Query:" + sqlQuery);
            // Execute the SQL query and return the result
            if (sqlQuery.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
            {
                var result = await ExecuteQuery(sqlQuery);
                return $"Query result: {JsonSerializer.Serialize(result)}";
            }
            else
            {
                return "Only SELECT queries are supported.";
            }
        }
        static string ExtractSqlQuery(string response)
        {
            var startIndex = response.IndexOf("```sql", StringComparison.OrdinalIgnoreCase);
            if (startIndex == -1) return "";
            startIndex += 7; // Move past "```sql"
            var endIndex = response.IndexOf("```", startIndex, StringComparison.OrdinalIgnoreCase);
            if (endIndex == -1) return "";
            return response.Substring(startIndex, endIndex - startIndex).Trim();
        }
        static async Task<List<Dictionary<string, object>>> ExecuteQuery(string query)
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();
            using var command = new NpgsqlCommand(query, connection);
            using var reader = await command.ExecuteReaderAsync();
            var results = new List<Dictionary<string, object>>();
            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    row[reader.GetName(i)] = reader.GetValue(i);
                }
                results.Add(row);
            }
            return results;
        }
    }
}