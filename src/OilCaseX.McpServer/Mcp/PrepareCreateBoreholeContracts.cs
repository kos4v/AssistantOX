using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using OilCaseX.McpServer.Configuration;
using OilCaseX.McpServer.Mcp.Dtos;

namespace OilCaseX.McpServer.Mcp;

public sealed record ConfirmationRecord(
    string ConfirmationId,
    string OwnerKey,
    string ResourceScope,
    string ToolName,
    string CanonicalPayload,
    string PayloadHash,
    BoreholePurchasePreview Preview,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset ExpiresAtUtc);

public interface IConfirmationStore
{
    ConfirmationRecord Create(
        string ownerKey,
        string resourceScope,
        string toolName,
        string canonicalPayload,
        string payloadHash,
        BoreholePurchasePreview preview);

    bool TryGet(string confirmationId, out ConfirmationRecord? record);
}

public sealed class InMemoryConfirmationStore(IOptions<McpServerOptions> options) : IConfirmationStore
{
    private readonly ConcurrentDictionary<string, ConfirmationRecord> records = new();

    public ConfirmationRecord Create(
        string ownerKey,
        string resourceScope,
        string toolName,
        string canonicalPayload,
        string payloadHash,
        BoreholePurchasePreview preview)
    {
        var now = DateTimeOffset.UtcNow;
        var confirmation = new ConfirmationRecord(
            $"cnf_{Guid.NewGuid():N}",
            ownerKey,
            resourceScope,
            toolName,
            canonicalPayload,
            payloadHash,
            preview,
            now,
            now.AddSeconds(options.Value.ConfirmationTtlSeconds));

        records[confirmation.ConfirmationId] = confirmation;
        return confirmation;
    }

    public bool TryGet(string confirmationId, out ConfirmationRecord? record)
    {
        if (!records.TryGetValue(confirmationId, out record))
        {
            return false;
        }

        if (record.ExpiresAtUtc > DateTimeOffset.UtcNow)
        {
            return true;
        }

        records.TryRemove(confirmationId, out _);
        record = null;
        return false;
    }
}

/// <summary>
/// Carries only a non-reversible fingerprint of the delegated token into a confirmation.
/// The raw JWT is never stored, returned, or logged.
/// </summary>
public sealed class DelegatedRequestContext(IHttpContextAccessor httpContextAccessor)
{
    public string GetOwnerKey()
    {
        var authorization = httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
        var token = authorization?.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) is true
            ? authorization[7..].Trim()
            : string.Empty;

        if (token.Length == 0)
        {
            return "anonymous";
        }

        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return $"jwt:{Convert.ToHexString(digest).ToLowerInvariant()}";
    }
}

public static class BoreholePayloadCanonicalizer
{
    public const string ToolName = "prepare_create_borehole";

    public static string Canonicalize(PrepareCreateBoreholeRequest request)
        => $"{{\"orderId\":{request.OrderId},\"wellpadId\":{request.WellpadId}}}";

    public static string Hash(string canonicalPayload)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalPayload))).ToLowerInvariant();
}

public sealed record AuditEvent(
    string Name,
    string Outcome,
    string ToolName,
    string OwnerKey,
    string ResourceScope,
    string? PayloadHash,
    string? ConfirmationId,
    string? ErrorCode,
    DateTimeOffset OccurredAtUtc);

public interface IAuditSink
{
    void Record(AuditEvent auditEvent);
}

public sealed class LoggingAuditSink(ILogger<LoggingAuditSink> logger) : IAuditSink
{
    public void Record(AuditEvent auditEvent)
    {
        logger.LogInformation(
            "MCP audit {AuditName} outcome={Outcome} tool={ToolName} owner={OwnerKey} scope={ResourceScope} payloadHash={PayloadHash} confirmationId={ConfirmationId} errorCode={ErrorCode}",
            auditEvent.Name,
            auditEvent.Outcome,
            auditEvent.ToolName,
            auditEvent.OwnerKey,
            auditEvent.ResourceScope,
            auditEvent.PayloadHash,
            auditEvent.ConfirmationId,
            auditEvent.ErrorCode);
    }
}
