using System.Numerics;
using System.Reflection;

namespace Sap.Cloud.Sdk.Result;

/// <summary>
/// Creates instances of <typeparamref name="T"/> from a primitive result element by calling a single-parameter constructor.
/// </summary>
/// <remarks>
/// Port of <c>PrimitiveBasedObjectExtractor</c>. Additionally, built-in scalar types (which have no such constructor in
/// .NET) are converted directly.
/// </remarks>
/// <typeparam name="T">The target type.</typeparam>
public class PrimitiveBasedObjectExtractor<T>
{
    /// <summary>Extracts a <typeparamref name="T"/> from the primitive.</summary>
    /// <param name="resultElement">The element; must be a primitive.</param>
    /// <returns>The value.</returns>
    /// <exception cref="NotSupportedException">The conversion failed or no suitable constructor exists.</exception>
    public T Extract(IResultElement resultElement)
    {
        ArgumentNullException.ThrowIfNull(resultElement);
        var objectType = typeof(T);
        if (resultElement is not IResultPrimitive)
        {
            throw new NotSupportedException($"Failed to instantiate {objectType.FullName} with non-primitive value {resultElement}.");
        }
        try
        {
            if (TryConvert(resultElement, objectType, out var direct) && objectType.IsInstanceOfType(direct))
            {
                return (T)direct!;
            }
            (ConstructorInfo Constructor, object Value)? construction = null;
            foreach (var constructor in objectType.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                var parameters = constructor.GetParameters();
                if (parameters.Length != 1)
                {
                    continue;
                }
                if (TryConvert(resultElement, parameters[0].ParameterType, out var value) && value is not null
                    && (construction is null || parameters[0].ParameterType.IsPrimitive))
                {
                    construction = (constructor, value);
                }
            }
            if (construction is { } c)
            {
                return (T)c.Constructor.Invoke([c.Value]);
            }
        }
#pragma warning disable CA1031 // Failures are reported uniformly as NotSupportedException, as in Java.
        catch (Exception e)
        {
            throw new NotSupportedException(
                $"Failed to instantiate {objectType.FullName} with constructor by value {resultElement} - {e.Message}.", e);
        }
#pragma warning restore CA1031
        throw new NotSupportedException($"Unsupported constructor of {objectType.FullName} to value {resultElement}.");
    }

    private static bool TryConvert(IResultElement element, Type parameter, out object? value)
    {
        value = null;
        parameter = Nullable.GetUnderlyingType(parameter) ?? parameter;
        if (parameter == typeof(string) || parameter == typeof(object))
        {
            value = element.AsString();
        }
        else if (parameter == typeof(decimal))
        {
            value = element.AsBigDecimal();
        }
        else if (parameter == typeof(BigInteger))
        {
            value = element.AsBigInteger();
        }
        else if (parameter == typeof(char))
        {
            value = element.AsCharacter();
        }
        else if (parameter == typeof(double))
        {
            value = element.AsDouble();
        }
        else if (parameter == typeof(float))
        {
            value = element.AsFloat();
        }
        else if (parameter == typeof(long))
        {
            value = element.AsLong();
        }
        else if (parameter == typeof(int))
        {
            value = element.AsInteger();
        }
        else if (parameter == typeof(short))
        {
            value = element.AsShort();
        }
        else if (parameter == typeof(byte))
        {
            value = element.AsByte();
        }
        else if (parameter == typeof(bool))
        {
            value = element.AsBoolean();
        }
        return value is not null;
    }
}
