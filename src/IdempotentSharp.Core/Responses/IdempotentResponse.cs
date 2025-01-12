namespace IdempotentSharp.Core.Responses;

/// <summary>
/// Idempotent Response Object.
/// </summary>
/// <param name="statusCode">
/// HttpStatusCode of the response. <see cref="StatusCode"/>
/// </param>
/// <param name="value">
/// Value of the response. <see cref="Value"/>
/// </param>
public class IdempotentResponse(int statusCode, object? value)
{
    public int StatusCode { get; } = statusCode;
    public object? Value { get; } = value;
}