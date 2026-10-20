namespace Sap.Cloud.Sdk.Result;

/// <summary>Creates <see cref="IResultElement"/>s from a source representation.</summary>
/// <typeparam name="TResultElement">The source representation.</typeparam>
public interface IResultElementFactory<in TResultElement>
{
    /// <summary>Creates a result element.</summary>
    /// <param name="resultElement">The source element.</param>
    /// <returns>The result element, or <see langword="null"/> if the source is <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException">The source cannot be converted.</exception>
    IResultElement? Create(TResultElement? resultElement);
}
