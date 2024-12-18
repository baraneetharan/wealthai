using CsvHelper.Configuration.Attributes;
using Microsoft.Extensions.VectorData;

namespace wealthai
{
    public class MyTransaction
    {
        [Name("Transaction Date")]
        public DateTime TransactionDate { get; set; }

        [Name("Portfolio Code")]
        [VectorStoreRecordKey] // Marking this as the key property

        public string? PortfolioCode { get; set; }

        [Name("Portfolio Name")]
        public string? PortfolioName { get; set; }

        [Name("Family Name")]
        public string? FamilyName { get; set; }

        [Name("Folio No")]
        public string? FolioNo { get; set; }

        [Name("RTA Scheme Code")]
        public string? RTASchemeCode { get; set; }

        [Name("Scheme Name")]
        public string? SchemeName { get; set; }

        public decimal Units { get; set; }

        [Name("Transaction Type")]
        public string? TransactionType { get; set; }

        [Name("Transaction Amount")]
        public decimal TransactionAmount { get; set; }

        [Name("Cost / Price per Unit")]
        public decimal CostPerUnit { get; set; }

        public decimal Load { get; set; }

        public decimal STT { get; set; }

        [Name("Stamp Duty")]
        public decimal StampDuty { get; set; }

        public string? Product { get; set; }

        [Name("RM Name")]
        public string? RMName { get; set; }

        // public string Description => $"Client: {PortfolioName}, Portfolio: {TransactionAmount}, Investments: {SchemeName}";
        // Updated Description with all columns values 
        public string Description => $"Transaction Date: {TransactionDate}, " + $"Portfolio Code: {PortfolioCode}, " + $"Portfolio Name: {PortfolioName}, " + $"Family Name: {FamilyName}, " + $"Folio No: {FolioNo}, " + $"RTA Scheme Code: {RTASchemeCode}, " + $"Scheme Name: {SchemeName}, " + $"Units: {Units}, " + $"Transaction Type: {TransactionType}, " + $"Transaction Amount: {TransactionAmount}, " + $"Cost / Price per Unit: {CostPerUnit}, " + $"Load: {Load}, " + $"STT: {STT}, " + $"Stamp Duty: {StampDuty}, " + $"Product: {Product}, " + $"RM Name: {RMName}";
        [Ignore]
        [VectorStoreRecordVector(384)] // Specify the dimensions of the vector
        public ReadOnlyMemory<float> Vector { get; set; }

    }
}