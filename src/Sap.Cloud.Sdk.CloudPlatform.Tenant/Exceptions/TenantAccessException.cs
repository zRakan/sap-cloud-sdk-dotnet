namespace Sap.Cloud.Sdk.CloudPlatform.Tenant.Exceptions;

/// <summary>Thrown when the current tenant cannot be accessed.</summary>
public class TenantAccessException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="TenantAccessException"/> class.</summary>
    public TenantAccessException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="TenantAccessException"/> class with a message.</summary>
    /// <param name="message">The error message.</param>
    public TenantAccessException(string? message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="TenantAccessException"/> class with a message and a cause.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The cause.</param>
    public TenantAccessException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
