using System.Diagnostics;
using System.Net.Sockets;

namespace VSP.Device.Discovery.Rtsp;

public sealed class RtspEndpointProbeService
{
    private readonly IRtspProbeTransport _transport;
    private readonly RtspRequestFactory _requestFactory;
    private readonly RtspResponseClassifier _responseClassifier;

    public RtspEndpointProbeService(
        IRtspProbeTransport transport,
        RtspRequestFactory? requestFactory = null,
        RtspResponseClassifier? responseClassifier = null)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _requestFactory = requestFactory ?? new RtspRequestFactory();
        _responseClassifier = responseClassifier ?? new RtspResponseClassifier();
    }

    public async Task<RtspEndpointProbeResult> ProbeAsync(
        RtspEndpointProbeRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        var effectiveRequest = request ?? new RtspEndpointProbeRequest();
        var endpointUri = _requestFactory.CreateEndpointUri(effectiveRequest);
        var timeout = effectiveRequest.Timeout <= TimeSpan.Zero
            ? RtspEndpointProbeRequest.DefaultTimeout
            : effectiveRequest.Timeout;

        using var timeoutCts = new CancellationTokenSource(timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var requestPayload = _requestFactory.CreateOptionsRequest(endpointUri);
            var responsePayload = await _transport.SendAndReceiveAsync(
                requestPayload,
                endpointUri.Host,
                endpointUri.Port,
                linkedCts.Token);

            stopwatch.Stop();

            var classifiedResponse = _responseClassifier.Classify(responsePayload);

            return new RtspEndpointProbeResult
            {
                NormalizedEndpointUri = endpointUri.ToString(),
                Host = endpointUri.Host,
                Port = endpointUri.Port,
                ResponseTime = stopwatch.Elapsed,
                TcpConnectionSucceeded = true,
                ReceivedAnyBytes = classifiedResponse.ReceivedAnyBytes,
                IsRtspResponse = classifiedResponse.IsRtspResponse,
                RtspStatusCode = classifiedResponse.StatusCode,
                RtspReasonPhrase = classifiedResponse.ReasonPhrase,
                ResponseClassification = classifiedResponse.Classification,
                WwwAuthenticate = classifiedResponse.WwwAuthenticate,
                RawStatusLine = classifiedResponse.RawStatusLine
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            stopwatch.Stop();

            return new RtspEndpointProbeResult
            {
                NormalizedEndpointUri = endpointUri.ToString(),
                Host = endpointUri.Host,
                Port = endpointUri.Port,
                ResponseTime = stopwatch.Elapsed,
                ResponseClassification = RtspProbeResponseClassification.Timeout,
                TimeoutOccurred = true,
                ErrorCategory = nameof(OperationCanceledException),
                ErrorMessage = "The RTSP probe timed out."
            };
        }
        catch (SocketException exception)
        {
            stopwatch.Stop();

            return CreateFailureResult(
                endpointUri,
                stopwatch.Elapsed,
                RtspProbeResponseClassification.ConnectionFailed,
                nameof(SocketException),
                exception.Message);
        }
        catch (IOException exception)
        {
            stopwatch.Stop();

            return CreateFailureResult(
                endpointUri,
                stopwatch.Elapsed,
                RtspProbeResponseClassification.ConnectionFailed,
                nameof(IOException),
                exception.Message);
        }
    }

    private static RtspEndpointProbeResult CreateFailureResult(
        Uri endpointUri,
        TimeSpan responseTime,
        RtspProbeResponseClassification classification,
        string errorCategory,
        string errorMessage)
    {
        return new RtspEndpointProbeResult
        {
            NormalizedEndpointUri = endpointUri.ToString(),
            Host = endpointUri.Host,
            Port = endpointUri.Port,
            ResponseTime = responseTime,
            ResponseClassification = classification,
            ErrorCategory = errorCategory,
            ErrorMessage = errorMessage
        };
    }
}
