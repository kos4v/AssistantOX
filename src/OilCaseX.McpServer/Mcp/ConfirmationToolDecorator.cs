using OilCaseX.McpServer.Mcp.Dtos;
using System.Diagnostics;
using OilCaseX.McpServer.ApiClient;
using OilCaseX.McpServer.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace OilCaseX.McpServer.Mcp;

/// <summary>Applies reusable confirmation storage, hashing and audit to a descriptor.</summary>
public sealed class ConfirmationToolDecorator(
    IConfirmationStore confirmationStore,
    DelegatedRequestContext requestContext,
    IAuditSink auditSink,
    IdempotencyKeyContext idempotencyKeyContext)
{
    public ToolResponse<object?> Prepare(ApiToolDescriptor descriptor, object? result, object?[] arguments)
    {
        var policy = descriptor.Confirmation!;
        var ownerKey = requestContext.GetOwnerKey();
        var resourceScope = policy.GetResourceScope(arguments);
        var canonicalPayload = BoreholePayloadCanonicalizer.Canonicalize(
            new PrepareCreateBoreholeRequest(
                ((ApiClient.Generated.PurchasedBoreholeCreateArgs)arguments[0]!).WellpadId,
                ((ApiClient.Generated.PurchasedBoreholeCreateArgs)arguments[0]!).OrderId));
        var payloadHash = BoreholePayloadCanonicalizer.Hash(canonicalPayload);
        var error = policy.GetValidationError(result);
        if (error is not null)
        {
            auditSink.Record(new AuditEvent("confirmation_prepare", "validation_failed", descriptor.ToolName, ownerKey, resourceScope, payloadHash, null, error.Code, DateTimeOffset.UtcNow));
            return ToolResponse<object?>.Failure(error);
        }

        var preview = policy.ProjectPreview(result);
        var confirmation = confirmationStore.Create(ownerKey, resourceScope, descriptor.ToolName, canonicalPayload, payloadHash, preview);
        auditSink.Record(new AuditEvent("confirmation_prepare", "prepared", descriptor.ToolName, ownerKey, resourceScope, payloadHash, confirmation.ConfirmationId, null, DateTimeOffset.UtcNow));
        return ToolResponse<object?>.Success(new PrepareCreateBoreholeResult(confirmation.ConfirmationId, confirmation.ExpiresAtUtc, payloadHash, preview));
    }

    public async Task<ToolResponse<T>> ExecuteAsync<T>(
        string confirmationId,
        string expectedToolName,
        Func<ConfirmationRecord, CancellationToken, Task> preflight,
        Func<ConfirmationRecord, string, CancellationToken, Task<T>> execute,
        CancellationToken cancellationToken)
    {
        using var activity = McpDiagnostics.ActivitySource.StartActivity(
            $"mcp.tool.{expectedToolName}",
            ActivityKind.Internal);
        activity?.SetTag("mcp.tool.name", expectedToolName);
        activity?.SetTag("mcp.confirmation.id", confirmationId);

        var ownerKey = requestContext.GetOwnerKey();
        if (!confirmationStore.TryGet(confirmationId, out var candidate) || candidate is null)
        {
            return Failure<T>(ownerKey, "confirmation_invalid", "Confirmation is missing or expired.", confirmationId);
        }

        if (!string.Equals(candidate.OwnerKey, ownerKey, StringComparison.Ordinal))
        {
            return Failure<T>(ownerKey, "forbidden", "Confirmation belongs to another caller.", confirmationId);
        }

        if (!string.Equals(candidate.ToolName, expectedToolName, StringComparison.Ordinal))
        {
            return Failure<T>(ownerKey, "confirmation_invalid", "Confirmation is not valid for this operation.", confirmationId);
        }

        try
        {
            await preflight(candidate, cancellationToken);
        }
        catch (Exception exception)
        {
            var error = exception is OilCaseXPreflightException preflightException
                ? new ToolError(preflightException.Code, preflightException.Message, false)
                : OilCaseXErrorMapper.Map(exception);
            auditSink.Record(new AuditEvent("confirmation_execute", "preflight_failed", expectedToolName, ownerKey, candidate.ResourceScope, candidate.PayloadHash, confirmationId, error.Code, DateTimeOffset.UtcNow));
            return ToolResponse<T>.Failure(error);
        }

        if (!confirmationStore.TryConsume(confirmationId, out var confirmation) || confirmation is null)
        {
            return Failure<T>(ownerKey, "confirmation_replayed", "Confirmation has already been consumed or expired.", confirmationId);
        }

        var idempotencyKey = CreateIdempotencyKey(ownerKey, confirmation.PayloadHash, expectedToolName);
        var previousKey = idempotencyKeyContext.CurrentKey;
        idempotencyKeyContext.CurrentKey = idempotencyKey;
        try
        {
            var result = await execute(confirmation, idempotencyKey, cancellationToken);
            auditSink.Record(new AuditEvent("confirmation_execute", "success", expectedToolName, ownerKey, confirmation.ResourceScope, confirmation.PayloadHash, confirmationId, null, DateTimeOffset.UtcNow));
            return ToolResponse<T>.Success(result);
        }
        catch (Exception exception)
        {
            var error = OilCaseXErrorMapper.Map(exception);
            auditSink.Record(new AuditEvent("confirmation_execute", "unknown", expectedToolName, ownerKey, confirmation.ResourceScope, confirmation.PayloadHash, confirmationId, error.Code, DateTimeOffset.UtcNow));
            return ToolResponse<T>.Failure(error with { Retryable = false });
        }
        finally
        {
            idempotencyKeyContext.CurrentKey = previousKey;
        }
    }

    private static string CreateIdempotencyKey(string ownerKey, string payloadHash, string toolName)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{toolName}|{ownerKey}|{payloadHash}"));
        return $"mcp_{Convert.ToHexString(bytes).ToLowerInvariant()}";
    }

    private ToolResponse<T> Failure<T>(string ownerKey, string code, string message, string confirmationId)
    {
        auditSink.Record(new AuditEvent("confirmation_execute", "rejected", "confirmation", ownerKey, "unknown", null, confirmationId, code, DateTimeOffset.UtcNow));
        return ToolResponse<T>.Failure(new ToolError(code, message, false));
    }
}
