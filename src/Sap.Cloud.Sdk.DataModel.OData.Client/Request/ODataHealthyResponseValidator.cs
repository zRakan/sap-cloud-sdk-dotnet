using Sap.Cloud.Sdk.DataModel.OData.Client.Exceptions;
using Sap.Cloud.Sdk.Result;

namespace Sap.Cloud.Sdk.DataModel.OData.Client.Request;

internal static class ODataHealthyResponseValidator
{
    private const int HttpBadRequest = 400;

    internal static async Task RequireHealthyResponseAsync(IODataRequestResult result, CancellationToken cancellationToken)
    {
        var originalRequest = result.ODataRequest;
        var httpResponse = result.HttpResponse;
        if (httpResponse.StatusCode < HttpBadRequest)
        {
            return;
        }

        // The exception reports the body, so it has to be read now.
        await httpResponse.BufferAsync(cancellationToken).ConfigureAwait(false);

        var requestRelevantForException = FindPotentialBatchItem(httpResponse, originalRequest) ?? originalRequest;
        var msg = $"The HTTP response code ({httpResponse.StatusCode}) indicates an error.";

        ODataServiceError? odataError = null;
        try
        {
            odataError = await LoadErrorFromResponseAsync(result, cancellationToken).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // As in Java: a response without a readable OData error is reported as plain response exception.
        catch (Exception e) when (e is not OperationCanceledException)
        {
            _ = e;
        }
#pragma warning restore CA1031

        if (odataError is not null)
        {
            var msgError = msg + " The OData service responded with an error message.";
            throw new ODataServiceErrorException(requestRelevantForException, httpResponse, msgError, null, odataError);
        }
        throw new ODataResponseException(requestRelevantForException, httpResponse, msg, null);
    }

    private static ODataRequestGeneric? FindPotentialBatchItem(ODataHttpResponse httpResponse, ODataRequestGeneric request)
    {
        if (request is not ODataRequestBatch requestBatch || httpResponse is not MultipartHttpResponse { ContentId: { } failedBatchRequestNumber })
        {
            return null;
        }
        foreach (var item in requestBatch.Requests)
        {
            if (item is ODataRequestBatch.BatchItemChangeset changeset)
            {
                foreach (var single in changeset.Requests)
                {
                    if (single.ContentId == failedBatchRequestNumber)
                    {
                        return single.Request;
                    }
                }
            }
            else if (item is ODataRequestBatch.BatchItemSingle single && single.ContentId == failedBatchRequestNumber)
            {
                return single.Request;
            }
        }
        return null;
    }

    private static Task<ODataServiceError> LoadErrorFromResponseAsync(IODataRequestResult result, CancellationToken cancellationToken)
    {
        var elementFactory = new JsonResultElementFactory(ODataJsonOptions.Get());
        return HttpEntityReader.ReadAsync(
            result,
            root =>
            {
                var error = root.GetProperty("error");
                var errorObject = new JsonResultObject(error, elementFactory);
                return ODataServiceError.FromResultObject(errorObject, result.ODataRequest.Protocol);
            },
            cancellationToken);
    }
}
