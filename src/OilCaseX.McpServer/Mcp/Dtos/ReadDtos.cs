namespace OilCaseX.McpServer.Mcp.Dtos;

public sealed record MapPosition(int X, int Y, double Z);

public sealed record WellpadSummary(int Id, string ObjectName, string Status, string TomorrowStatus,
    MapPosition Position, int SizeX, int SizeY, int WellpadSize, IReadOnlyList<int> BoreholeIds,
    bool ConstructionPlanCompleted);

public sealed record BoreholeHead(int Id, int SequenceNumber, int X, int Y, double Z);

public sealed record BoreholeSummary(int BoreholeId, string? Nickname, string Status,
    string ChokeBeanDiameter, BoreholeHead Head, int TrajectoryPointCount, int HistoryRecordCount,
    int WellpadId);
