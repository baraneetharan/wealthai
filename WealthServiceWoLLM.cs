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
    public class WealthServiceWoLLM
    {
        private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;

        public WealthServiceWoLLM(IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator)
        {
            _embeddingGenerator = embeddingGenerator;
            QnA().Wait(); // Call QnA method to fill transactions list
        }

        List<MyTransaction> transactions;
        public async Task QnA()
        {
            string filePath = @"D:\baraneetharan\myworks\SemanticKernel\meai\wealthai\TransactionReport500.csv";

            Console.WriteLine("WealthServiceWoLLM StoreVectors start processing...");

            using var reader = new StreamReader(filePath);
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture));
            transactions = csv.GetRecords<MyTransaction>().ToList();

        }

        public async Task<string> GetUniqueClients()
        {
            Console.WriteLine("GetUniqueClients");

            var uniqueClients = await Task.Run(() =>
                transactions
                .Select(t => t.PortfolioName)
                .Distinct()
                .ToList()
            );

            // Join the client names with a comma
            string clients = string.Join(", ", uniqueClients);
            Console.WriteLine(clients);
            return clients;
        }


        public async Task<string> GetRMNames()
        {
            Console.WriteLine("GetRMNames");

            var rmNames = await Task.Run(() =>
                transactions
                .Select(t => t.RMName)
                .Distinct()
                .ToList()
            );

            // Join the RM names with a comma
            string rmNamesList = string.Join(", ", rmNames);
            Console.WriteLine(rmNamesList);
            return rmNamesList;
        }

        public async Task<string> GetTopClient()
        {
            Console.WriteLine("GetTopClient");

            var topClient = await Task.Run(() =>
                transactions
                .GroupBy(t => t.PortfolioName)
                .OrderByDescending(g => g.Sum(t => t.TransactionAmount))
                .FirstOrDefault()
                ?.Key
            );

            Console.WriteLine(topClient);
            return topClient ?? "No clients found.";
        }

        public async Task<string> GetClientRMNames(string clientName)
        {
            Console.WriteLine("GetClientRMNames");

            var clientRMNames = await Task.Run(() =>
                transactions
                .Where(t => t.PortfolioName == clientName)
                .Select(t => t.RMName)
                .Distinct()
                .ToList()
            );

            // Join the RM names with a comma
            string rmNamesList = string.Join(", ", clientRMNames);
            Console.WriteLine(rmNamesList);
            return rmNamesList;
        }

        public async Task<string> GetAssestwiseTotal()
        {
            Console.WriteLine("GetAssestwiseTotal");

            var assetwiseTotal = await Task.Run(() =>
                transactions
                .GroupBy(t => t.Product)
                .Select(g => new { Product = g.Key, TotalAmount = g.Sum(t => t.TransactionAmount) })
                .OrderByDescending(a => a.TotalAmount)
                .ToList()
            );

            var result = string.Join(Environment.NewLine, assetwiseTotal.Select(a => $"{a.Product}: {a.TotalAmount}"));
            Console.WriteLine(result);
            return result;
        }

    }
}