using OilCaseX.McpServer.ApiClient.Generated;
using OilCaseX.McpServer.Mcp.Dtos;

namespace OilCaseX.McpServer.Mcp.Projection;

public static class OilCaseXDtoProjector
{
    public static Func<object?, object?> CreateDefaultProjector(Type returnType)
    {
        var resultType = returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>)
            ? returnType.GetGenericArguments()[0]
            : returnType;

        if (resultType == typeof(WellpadResult2))
        {
            return ProjectWellpad;
        }

        if (resultType == typeof(BoreholeResult2))
        {
            return ProjectBorehole;
        }

        if (resultType.IsGenericType
            && resultType.GetGenericTypeDefinition() == typeof(ICollection<>)
            && resultType.GetGenericArguments()[0] == typeof(WellpadResult2))
        {
            return ProjectWellpads;
        }

        return value => value!;
    }

    public static object ProjectWellpads(object? value) => ((IEnumerable<WellpadResult2>?)value ?? [])
        .Select(ProjectWellpad).ToArray();

    public static object ProjectWellpad(object? value)
    {
        var result = (WellpadResult2)value!;
        var position = result.Position ?? new PositionOnMapDTO();
        return new WellpadSummary(result.Id, result.OOAName.ToString(), result.Status.ToString(),
            result.TomorrowStatus.ToString(), new MapPosition(position.X, position.Y, position.Z),
            result.SizeX, result.SizeY, result.WellpadSize,
            (result.PurchasedBoreholeIds ?? Array.Empty<int>()).ToArray(), result.IsConstructionPlanCompleted);
    }

    public static object ProjectBorehole(object? value)
    {
        var result = (BoreholeResult2)value!;
        var head = result.Head ?? new TrajectoryPointDTO();
        return new BoreholeSummary(result.BoreholeId, result.Nickname, result.BoreholeStatus.ToString(),
            result.ChokeBeanDiameter.ToString(), new BoreholeHead(head.Id, head.SequenceNumber, head.X, head.Y, head.Z),
            result.Trajectory?.Count ?? 0, result.History?.Count ?? 0, result.WellpadId);
    }
}
