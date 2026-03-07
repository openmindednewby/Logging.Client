namespace Logging.Client.Context;

/// <summary>
/// Provides AsyncLocal storage for correlation ID propagation across async call chains.
/// </summary>
public static class CorrelationIdContext
{
    private static readonly AsyncLocal<string?> CurrentValue = new();

    /// <summary>
    /// Gets or sets the current correlation ID for the async execution context.
    /// </summary>
    public static string? Current
    {
        get => CurrentValue.Value;
        set => CurrentValue.Value = value;
    }
}
