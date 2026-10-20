namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity;

/// <summary>An HTTP header to add to requests sent to a destination.</summary>
/// <param name="Name">The header name.</param>
/// <param name="Value">The header value.</param>
/// <remarks>The value is not printed by <see cref="ToString"/> because headers frequently carry credentials.</remarks>
public sealed record Header(string Name, string? Value)
{
    /// <summary>Hides the value, which may be confidential.</summary>
    /// <returns>A string representation.</returns>
    public override string ToString() => $"Header(Name={Name}, Value=(hidden))";
}
