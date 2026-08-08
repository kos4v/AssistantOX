using OilCaseX.McpServer.Mcp.Dtos;

namespace OilCaseX.McpServer.Mcp;

/// <summary>Applies reusable confirmation storage, hashing and audit to a descriptor.</summary>
public sealed class ConfirmationToolDecorator(
    IConfirmationStore confirmationStore,
    DelegatedRequestContext requestContext,
    IAuditSink auditSink)
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
}
