using MyDhathuru.Domain.Common;
using MyDhathuru.Domain.Enums;

namespace MyDhathuru.Domain.Entities;

public class StaffConductExportDocument : TenantEntity
{
    public Guid StaffConductFormId { get; set; }
    public StaffConductForm StaffConductForm { get; set; } = null!;

    public StaffConductFormType FormType { get; set; }
    public StaffConductExportLanguage Language { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/pdf";
    public long FileSizeBytes { get; set; }
    public byte[] Content { get; set; } = Array.Empty<byte>();
    public string? ContentHash { get; set; }
}
