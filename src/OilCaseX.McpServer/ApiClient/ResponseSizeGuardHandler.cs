using System.Net;
using Microsoft.Extensions.Options;
using OilCaseX.McpServer.Configuration;

namespace OilCaseX.McpServer.ApiClient;

public sealed class ResponseSizeGuardHandler(IOptions<McpServerOptions> options) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);
        var maxBytes = options.Value.MaxResponseBodyBytes;
        var contentLength = response.Content.Headers.ContentLength;

        if (contentLength > maxBytes)
        {
            response.Dispose();
            throw new UpstreamResponseTooLargeException(maxBytes);
        }

        response.Content = new SizeLimitedHttpContent(response.Content, maxBytes);
        return response;
    }

    private sealed class SizeLimitedHttpContent : HttpContent
    {
        private readonly HttpContent inner;
        private readonly long maxBytes;

        public SizeLimitedHttpContent(HttpContent inner, long maxBytes)
        {
            this.inner = inner;
            this.maxBytes = maxBytes;
            foreach (var header in inner.Headers)
            {
                Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            return SerializeToStreamAsync(stream, context, CancellationToken.None);
        }

        protected override async Task SerializeToStreamAsync(
            Stream stream,
            TransportContext? context,
            CancellationToken cancellationToken)
        {
            await using var source = await inner.ReadAsStreamAsync(cancellationToken);
            var buffer = new byte[81920];
            long total = 0;

            while (true)
            {
                var read = await source.ReadAsync(buffer.AsMemory(), cancellationToken);
                if (read == 0)
                {
                    break;
                }

                total += read;
                if (total > maxBytes)
                {
                    throw new UpstreamResponseTooLargeException(maxBytes);
                }

                await stream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }
        }

        protected override bool TryComputeLength(out long length)
        {
            if (inner.Headers.ContentLength is { } contentLength && contentLength <= maxBytes)
            {
                length = contentLength;
                return true;
            }

            length = 0;
            return false;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
