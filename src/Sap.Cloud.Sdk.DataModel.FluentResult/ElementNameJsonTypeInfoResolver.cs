using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Sap.Cloud.Sdk.Result;

/// <summary>
/// Creates JSON type info resolvers that honor <see cref="ElementNameAttribute"/>.
/// </summary>
/// <remarks>Replaces <c>ElementNameGsonFieldNamingStrategy</c> and <c>AnnotatedFieldGsonExclusionStrategy</c>.</remarks>
public static class ElementNameJsonTypeInfoResolver
{
    /// <summary>Creates a resolver that renames members carrying <see cref="ElementNameAttribute"/>.</summary>
    /// <param name="onlyAnnotated">Whether members without the attribute are skipped.</param>
    /// <returns>The resolver.</returns>
    public static IJsonTypeInfoResolver Create(bool onlyAnnotated = false)
    {
        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(typeInfo =>
        {
            if (typeInfo.Kind != JsonTypeInfoKind.Object)
            {
                return;
            }
            for (var i = typeInfo.Properties.Count - 1; i >= 0; i--)
            {
                var property = typeInfo.Properties[i];
                var attribute = property.AttributeProvider?.GetCustomAttributes(typeof(ElementNameAttribute), true)
                    .OfType<ElementNameAttribute>().FirstOrDefault();
                if (attribute is null)
                {
                    if (onlyAnnotated)
                    {
                        typeInfo.Properties.RemoveAt(i);
                    }
                    continue;
                }
                if (!string.IsNullOrEmpty(attribute.Value))
                {
                    property.Name = attribute.Value;
                }
            }
        });
        return resolver;
    }

    /// <summary>Creates serializer options that honor <see cref="ElementNameAttribute"/>.</summary>
    /// <param name="onlyAnnotated">Whether members without the attribute are skipped.</param>
    /// <returns>New options; cache the instance.</returns>
    public static JsonSerializerOptions CreateOptions(bool onlyAnnotated = false) =>
        new(JsonSerializerDefaults.General) { TypeInfoResolver = Create(onlyAnnotated), IncludeFields = true };
}
