using Microsoft.Extensions.Options;
using OilCaseX.McpServer.Configuration;

namespace OilCaseX.McpServer.Middleware;

public sealed class ResponseSizeLimitMiddleware(
    RequestDelegate next,
    IOptions<McpServerOptions> options)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var originalBody = context.Response.Body;
        await using var limitedBody = new SizeLimitingStream(
            originalBody,
            options.Value.MaxResponseBodyBytes);
        context.Response.Body = limitedBody;

        try
        {
            await next(context);
        }
        catch (ResponseSizeLimitExceededException) when (!context.Response.HasStarted)
        {
            context.Response.Body = originalBody;
            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "response_too_large",
                message = "The MCP response exceeds the configured size limit."
            });
        }
        finally
        {
            context.Response.Body = originalBody;
        }
    }

    private sealed class SizeLimitingStream(Stream inner, long maxBytes) : Stream
    {
        private long bytesWritten;

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => inner.CanWrite;
        public override long Length => inner.Length;
        public override long Position { get => inner.Position; set => throw new NotSupportedException(); }

        public override void Flush() => inner.Flush();
        public override Task FlushAsync(CancellationToken cancellationToken) => inner.FlushAsync(cancellationToken);
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
        {
            EnsureWithinLimit(count);
            inner.Write(buffer, offset, count);
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            EnsureWithinLimit(buffer.Length);
            inner.Write(buffer);
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            EnsureWithinLimit(count);
            return inner.WriteAsync(buffer, offset, count, cancellationToken);
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            EnsureWithinLimit(buffer.Length);
            return inner.WriteAsync(buffer, cancellationToken);
        }

        private void EnsureWithinLimit(int count)
        {
            if (count > maxBytes - bytesWritten)
            {
                throw new ResponseSizeLimitExceededException();
            }

            bytesWritten += count;
        }
    }

    private sealed class ResponseSizeLimitExceededException : Exception;
}
