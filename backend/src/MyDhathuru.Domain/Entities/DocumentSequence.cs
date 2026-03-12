using MyDhathuru.Domain.Common;
using MyDhathuru.Domain.Enums;
namespace MyDhathuru.Domain.Entities;
public class DocumentSequence : TenantEntity
{
    public DocumentType DocumentType { get; set; }
    public int Year { get; set; }
    public int NextNumber { get; set; } = 1;
}
