namespace Sap.Cloud.Sdk.DataModel.OData.Client.Expression;

internal static class ValueNumericFactory
{
    internal static IValueNumeric LiteralBoxed(object value) =>
        new ValueNumericImpl((protocol, _) => protocol.NumberSerializer(value));
}
