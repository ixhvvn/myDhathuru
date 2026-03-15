using MyDhathuru.Domain.Common;

namespace MyDhathuru.Domain.Entities;

public class ReceivedInvoiceAttachment : TenantEntity
{
    public Guid ReceivedInvoiceId { get; set; }
    public ReceivedInvoice ReceivedInvoice { get; set; } = null!;
    public required string FileName { get; set; }
    public required string ContentType { get; set; }
    public long SizeBytes { get; set; }
    public byte[] Content { get; set; } = Array.Empty<byte>();
}
