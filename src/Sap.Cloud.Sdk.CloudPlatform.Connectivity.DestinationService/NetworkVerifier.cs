using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Sap.Cloud.Sdk.CloudPlatform.Connectivity.Exceptions;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity;

/// <summary>Verifies that a host accepts TCP connections on a port.</summary>
internal interface INetworkVerifier
{
    /// <exception cref="DestinationAccessException">The host cannot be resolved or does not accept the connection.</exception>
    Task VerifyHostConnectivityAsync(string host, int port, CancellationToken cancellationToken);
}

/// <summary>
/// Performs a TCP connection to the host and port and closes it immediately, with a timeout of five seconds.
/// </summary>
internal sealed class NetworkVerifier : INetworkVerifier
{
    private static readonly TimeSpan HostReachTimeout = TimeSpan.FromSeconds(5);
    private static readonly ILogger Log = SdkLogging.CreateLogger<NetworkVerifier>();

    public async Task VerifyHostConnectivityAsync(string host, int port, CancellationToken cancellationToken)
    {
        if (Log.IsEnabled(LogLevel.Information))
        {
            Log.LogInformation("Verifying that transparent proxy host is reachable on {Host}:{Port}", host, port);
        }
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(HostReachTimeout);
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(host, port, timeout.Token).ConfigureAwait(false);
            if (Log.IsEnabled(LogLevel.Information))
            {
                Log.LogInformation("Successfully verified that transparent proxy host is reachable on {Host}:{Port}", host, port);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new DestinationAccessException(
                $"Host [{host}] on port [{port}] is not reachable. Caused by: connection attempt timed out after {HostReachTimeout.TotalSeconds:0} seconds.");
        }
        catch (SocketException e) when (e.SocketErrorCode is SocketError.HostNotFound or SocketError.TryAgain or SocketError.NoData)
        {
            throw new DestinationAccessException($"Host [{host}] could not be resolved. Caused by: {e.Message}", e);
        }
        catch (SocketException e)
        {
            throw new DestinationAccessException($"Host [{host}] on port [{port}] is not reachable. Caused by: {e.Message}", e);
        }
    }
}
