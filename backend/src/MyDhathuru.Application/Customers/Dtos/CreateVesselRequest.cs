namespace MyDhathuru.Application.Customers.Dtos;

public class CreateVesselRequest
{
    public required string Name { get; set; }
    public string? RegistrationNumber { get; set; }
    public DateOnly? IssuedDate { get; set; }
    public int? PassengerCapacity { get; set; }
    public string? VesselType { get; set; }
    public string? HomePort { get; set; }
    public string? OwnerName { get; set; }
    public string? ContactPhone { get; set; }
    public string? Notes { get; set; }
}
