namespace Sap.Cloud.Sdk.Result;

internal static class CollectionExtraction
{
#pragma warning disable CA1031 // Failures are reported uniformly as NotSupportedException, as in Java.
    internal static List<T> Collect<T>(IEnumerable<IResultElement> elements, string failureMessage, Func<IResultElement, T> extractor)
    {
        try
        {
            return elements.Select(extractor).ToList();
        }
        catch (Exception e)
        {
            throw new NotSupportedException(failureMessage, e);
        }
    }

    internal static List<T> CollectNested<T>(IEnumerable<IResultElement> elements, string name, Func<IResultElement, T> extractor)
    {
        try
        {
            var result = new List<T>();
            foreach (var element in elements)
            {
                if (element.IsResultObject)
                {
                    var nested = element.GetAsObject().GetElement(name)
                        ?? throw new InvalidOperationException($"Element {name} not found.");
                    result.Add(extractor(nested));
                }
            }
            return result;
        }
        catch (Exception e)
        {
            throw new NotSupportedException($"Failed to collect nested elements with name {name}.", e);
        }
    }
#pragma warning restore CA1031

    internal static IReadOnlySet<T> ToSet<T>(IEnumerable<T> items) => new HashSet<T>(items);
}
