using System.Reflection;
using System.Text.Json;
using System.Linq.Expressions;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using OilCaseX.McpServer.ApiClient;
using OilCaseX.McpServer.ApiClient.Generated;
using OilCaseX.McpServer.Mcp.Dtos;
using OilCaseX.McpServer.Mcp.Projection;
using BoreholePurchasePreview = OilCaseX.McpServer.Mcp.Dtos.BoreholePurchasePreview;

namespace OilCaseX.McpServer.Mcp;

/// <summary>
/// Build-time curated descriptors are exposed as MCP tools through one generic executor.
/// The generated API client remains the only component that performs upstream calls.
/// </summary>
public sealed class OilCaseXGenericTools : IEnumerable<McpServerTool>
{
    private static readonly JsonSerializerOptions StrictJsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow
    };

    private static readonly MethodInfo InvokeMethod =
        typeof(GenericApiToolTarget).GetMethod(nameof(GenericApiToolTarget.InvokeAsync))!;

    private readonly IReadOnlyList<McpServerTool> tools;

    public OilCaseXGenericTools()
    {
        var descriptors = OilCaseXApiToolCatalog.Descriptors
            .Where(ApiToolFilters.IsAllowed)
            .ToArray();
        tools = descriptors.Select(CreateTool).ToArray();
    }

    public IEnumerator<McpServerTool> GetEnumerator() => tools.GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    private static McpServerTool CreateTool(ApiToolDescriptor descriptor)
    {
        var tool = McpServerTool.Create(
            InvokeMethod,
            _ => new GenericApiToolTarget(descriptor),
            new McpServerToolCreateOptions
            {
                Name = descriptor.ToolName,
                Title = descriptor.Title,
                Description = descriptor.Description,
                ReadOnly = descriptor.ReadOnly,
                Destructive = descriptor.Destructive,
                Idempotent = descriptor.Idempotent,
                OpenWorld = false,
                UseStructuredContent = false
            });

        tool.ProtocolTool.InputSchema = JsonSerializer.Deserialize<JsonElement>(descriptor.InputSchema);
        return tool;
    }
}

public sealed record ApiToolDescriptor(
    Type ClientType,
    string OperationId,
    string ToolName,
    string Title,
    string Description,
    bool ReadOnly,
    bool Destructive,
    bool Idempotent,
    string InputSchema,
    Func<object?, object?> Project,
    MethodInfo Method,
    ConfirmationPreparation? Confirmation = null);

public sealed record ConfirmationPreparation(
    Func<object?[], string> GetResourceScope,
    Func<object?, ToolError?> GetValidationError,
    Func<object?, BoreholePurchasePreview> ProjectPreview);

public static class OilCaseXApiToolCatalog
{
    public static IReadOnlyList<ApiToolDescriptor> Descriptors { get; } = CreateDescriptors();

    private static IReadOnlyList<ApiToolDescriptor> CreateDescriptors()
        =>
        [
            CreateDescriptor((OilCaseXApiClientGenerated client) => client.ListWellpadsAsync(CancellationToken.None)),
            CreateDescriptor((OilCaseXApiClientGenerated client) => client.GetWellpadAsync(0, CancellationToken.None)),
            CreateDescriptor((OilCaseXApiClientGenerated client) => client.GetBoreholeAsync(0, CancellationToken.None)),
            CreatePrepareBoreholeDescriptor()
        ];

    private static ApiToolDescriptor CreatePrepareBoreholeDescriptor()
    {
        return CreateDescriptor(
            (OilCaseXApiClientGenerated client) => client.ValidatePurchasedBoreholeAsync(new PurchasedBoreholeCreateArgs(), CancellationToken.None),
            operationId: "prepareCreateBorehole",
            title: "Prepare an OilCaseX borehole creation",
            description: "Validates an OilCaseX borehole creation and creates a confirmation.",
            readOnly: false,
            idempotent: false,
            confirmation: new ConfirmationPreparation(
                values => $"wellpad:{((PurchasedBoreholeCreateArgs)values[0]!).WellpadId}",
                result =>
                {
                    var validation = (BoreholePurchaseValidationResult)result!;
                    if (validation.IsValid && validation.Preview is not null) return null;
                    var issue = validation.Issues?.FirstOrDefault();
                    return new ToolError(issue?.Code ?? "validation_failed", issue?.Message ?? "OilCaseX rejected the borehole creation request.", false);
                },
                result =>
                {
                    var preview = ((BoreholePurchaseValidationResult)result!).Preview!;
                    return new BoreholePurchasePreview(preview.WellpadId, preview.OrderId, preview.WellpadSize, preview.HeadX, preview.HeadY, (decimal)preview.HeadZ, preview.ResourceVersion ?? string.Empty);
                }));
    }

    private static ApiToolDescriptor CreateDescriptor<TClient, TResult>(
        Expression<Func<TClient, Task<TResult>>> methodExpression,
        string? operationId = null,
        string? toolName = null,
        string? title = null,
        string? description = null,
        bool readOnly = true,
        bool destructive = false,
        bool idempotent = true,
        string? inputSchema = null,
        Func<object?, object?>? project = null,
        ConfirmationPreparation? confirmation = null)
        where TClient : class
    {
        if (methodExpression.Body is not MethodCallExpression methodCall)
        {
            throw new ArgumentException("The descriptor expression must call an API client method.", nameof(methodExpression));
        }

        var method = methodCall.Method;
        var defaultOperationId = ToOperationId(method.Name);
        operationId ??= defaultOperationId;

        return new ApiToolDescriptor(
            typeof(TClient),
            operationId,
            toolName ?? JsonNamingPolicy.SnakeCaseLower.ConvertName(operationId),
            title ?? ToTitle(operationId),
            description ?? $"Generated OilCaseX API operation '{operationId}'.",
            readOnly,
            destructive,
            idempotent,
            inputSchema ?? CreateInputSchema(method),
            project ?? OilCaseXDtoProjector.CreateDefaultProjector(method.ReturnType),
            method,
            confirmation);
    }

    private static string CreateInputSchema(MethodInfo method)
    {
        var properties = new Dictionary<string, object?>(StringComparer.Ordinal);
        var required = new List<string>();

        foreach (var parameter in method.GetParameters())
        {
            if (parameter.ParameterType == typeof(CancellationToken))
            {
                continue;
            }

            var name = parameter.Name
                ?? throw new InvalidOperationException($"API method '{method.Name}' has an unnamed parameter.");
            var property = CreateParameterSchema(parameter.ParameterType);
            properties[name] = property;
            if (!parameter.IsOptional)
            {
                required.Add(name);
            }
        }

        var schema = new Dictionary<string, object?>
        {
            ["type"] = "object",
            ["additionalProperties"] = false,
            ["properties"] = properties
        };
        if (required.Count > 0)
        {
            schema["required"] = required;
        }

        return JsonSerializer.Serialize(schema);
    }

    private static object CreateParameterSchema(Type parameterType)
    {
        var nullableType = Nullable.GetUnderlyingType(parameterType);
        var type = nullableType ?? parameterType;
        if (type == typeof(int) || type == typeof(long) || type == typeof(short))
        {
            return new Dictionary<string, object?>
            {
                ["type"] = "integer",
                ["minimum"] = 1
            };
        }

        if (type == typeof(bool))
        {
            return new Dictionary<string, object?> { ["type"] = "boolean" };
        }

        if (type == typeof(string))
        {
            return new Dictionary<string, object?> { ["type"] = "string" };
        }

        return new Dictionary<string, object?> { ["type"] = "object" };
    }

    private static string ToOperationId(string methodName)
    {
        var name = methodName.EndsWith("Async", StringComparison.Ordinal)
            ? methodName[..^5]
            : methodName;
        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    private static string ToTitle(string operationId)
        => string.Join(' ', operationId.Split('_', StringSplitOptions.RemoveEmptyEntries)
            .Select(word => char.ToUpperInvariant(word[0]) + word[1..]));

}

public static class ApiToolFilters
{
    private static readonly IApiToolFilter[] Filters =
    [
        new OperationAllowListFilter(["listWellpads", "getWellpad", "getBorehole", "prepareCreateBorehole"]),
        new NonDestructiveOperationFilter()
    ];

    public static bool IsAllowed(ApiToolDescriptor descriptor)
        => Filters.All(filter => filter.Include(descriptor));
}

public interface IApiToolFilter
{
    bool Include(ApiToolDescriptor descriptor);
}

public sealed class OperationAllowListFilter(IEnumerable<string> operationIds) : IApiToolFilter
{
    private readonly HashSet<string> operationIds = operationIds.ToHashSet(StringComparer.Ordinal);

    public bool Include(ApiToolDescriptor descriptor) => operationIds.Contains(descriptor.OperationId);
}

public sealed class NonDestructiveOperationFilter : IApiToolFilter
{
    public bool Include(ApiToolDescriptor descriptor) => !descriptor.Destructive;
}

internal sealed class GenericApiToolTarget(ApiToolDescriptor descriptor)
{
    public async Task<object?> InvokeAsync(
        RequestContext<CallToolRequestParams> context,
        CancellationToken cancellationToken)
    {
        var arguments = context.Params.Arguments ?? new Dictionary<string, JsonElement>();
        var parameterNames = descriptor.Method.GetParameters()
            .Where(parameter => parameter.ParameterType != typeof(CancellationToken) && parameter.Name is not null)
            .Select(parameter => parameter.Name!)
            .ToHashSet(StringComparer.Ordinal);
        var unknownArgument = arguments.Keys.FirstOrDefault(name => !parameterNames.Contains(name));
        if (unknownArgument is not null)
        {
            return ToolResponse<object?>.Failure(
                new ToolError("invalid_input", $"Unknown argument '{unknownArgument}'.", false));
        }

        var values = new List<object?>();
        foreach (var parameter in descriptor.Method.GetParameters())
        {
            if (parameter.ParameterType == typeof(CancellationToken))
            {
                values.Add(cancellationToken);
                continue;
            }

            if (parameter.Name is null
                || !arguments.TryGetValue(parameter.Name, out var value))
            {
                return ToolResponse<object?>.Failure(
                    new ToolError("invalid_input", $"Missing required argument '{parameter.Name}'.", false));
            }

            object? converted;
            try
            {
                converted = value.Deserialize(parameter.ParameterType, StrictJsonOptions);
            }
            catch (JsonException)
            {
                return ToolResponse<object?>.Failure(
                    new ToolError("invalid_input", $"Argument '{parameter.Name}' contains an unknown or invalid JSON property.", false));
            }
            if (converted is null)
            {
                return ToolResponse<object?>.Failure(
                    new ToolError("invalid_input", $"Argument '{parameter.Name}' cannot be null.", false));
            }

            values.Add(converted);
        }

        try
        {
            using var scope = context.Server.Services!.CreateScope();
            var client = scope.ServiceProvider.GetRequiredService(descriptor.ClientType);
            var invocation = descriptor.Method.Invoke(client, values.ToArray());
            var result = await UnwrapTaskAsync(invocation);
            if (descriptor.Confirmation is not null)
            {
                var decorator = new ConfirmationToolDecorator(
                    scope.ServiceProvider.GetRequiredService<IConfirmationStore>(),
                    scope.ServiceProvider.GetRequiredService<DelegatedRequestContext>(),
                    scope.ServiceProvider.GetRequiredService<IAuditSink>(),
                    scope.ServiceProvider.GetRequiredService<ApiClient.IdempotencyKeyContext>());
                return decorator.Prepare(descriptor, result, values.ToArray());
            }
            return ToolResponse<object?>.Success(descriptor.Project(result));
        }
        catch (Exception exception)
        {
            var effectiveException = exception is TargetInvocationException { InnerException: not null } target
                ? target.InnerException!
                : exception;
            var error = OilCaseXErrorMapper.Map(effectiveException);
            return ToolResponse<object?>.Failure(error);
        }
    }

    private static async Task<object?> UnwrapTaskAsync(object? invocation)
    {
        if (invocation is not Task task)
        {
            return invocation;
        }

        await task.ConfigureAwait(false);
        return task.GetType().GetProperty("Result")?.GetValue(task);
    }
}
