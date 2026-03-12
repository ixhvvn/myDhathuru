namespace MyDhathuru.Application.Common.Validation;

public static class ValidationPatterns
{
    public const string Name = @"^[\p{L}][\p{L}\s.'&-]*$";
    public const string Phone = @"^\+?[0-9]{7,15}$";
    public const string AccountNumber = @"^[0-9]{6,30}$";
}
