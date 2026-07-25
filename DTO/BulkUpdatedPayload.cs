using System.Collections.Generic;

namespace CosmosUpdater.Models
{
    public class BulkUpdatePayload
    {
        public required string Query { get; set; }
        public int Limit { get; set; } = 100; 
        public bool IncludeDependedQuery { get; set; } = false;
        public required Dictionary<string, object> RequiredChange { get; set; }
    }
}