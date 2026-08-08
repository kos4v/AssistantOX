namespace OilCaseX.McpServer.Mcp.Dtos;

public sealed record PrepareCreateBoreholeRequest(int WellpadId, int OrderId);

public sealed record BoreholePurchasePreview(int WellpadId, int OrderId, int WellpadSize,
    int HeadX, int HeadY, decimal HeadZ, string ResourceVersion);

public sealed record PrepareCreateBoreholeResult(string ConfirmationId, DateTimeOffset ExpiresAtUtc,
    string PayloadHash, BoreholePurchasePreview Preview);
