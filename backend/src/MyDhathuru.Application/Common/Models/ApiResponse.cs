namespace MyDhathuru.Application.Common.Models;

public record ApiResponse<T>(bool Success, string Message, T? Data = default, IEnumerable<string>? Errors = null)
{
    public static ApiResponse<T> Ok(T data, string message = "Success") => new(true, message, data);
    public static ApiResponse<T> Fail(string message, IEnumerable<string>? errors = null) => new(false, message, default, errors);
}
