
namespace CosmosUpdater.Models;

public class Price_Product_DTO
{
    public string? rowId { get; set; }
    public int? transactionNo { get; set; }
    public string? rawPayload { get; set; }
    public required string recordType { get; set; }
    public required string status { get; set; }
    public int? retryCount { get; set; }
    public string? batchNo { get; set; }
    public int? changeId { get; set; }
    public long? timestamp { get; set; }
    public bool? markedForDelete { get; set; }
    public string? storeId { get; set; }
    public required string sku { get; set; }
    public required string id { get; set; }
    public string? _rid { get; set; }
    public string? _self { get; set; }
    public string? _etag { get; set; }
    public string? _attachments { get; set; }
    public long? _ts { get; set; }
}