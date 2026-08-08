[CmdletBinding()]
param(
    [string]$Uri = "https://x.stg.oilcase.ru/swagger/v1/swagger.json",
    [string]$OutputRoot = "..\contracts\openapi"
)

$ErrorActionPreference = "Stop"
$utf8NoBom = [System.Text.UTF8Encoding]::new($false)
$null = Add-Type -AssemblyName System.Net.Http
$resolvedOutputRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot $OutputRoot))
New-Item -ItemType Directory -Force -Path $resolvedOutputRoot | Out-Null

function Get-Sha256([byte[]]$Bytes) {
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        return ([BitConverter]::ToString($sha.ComputeHash($Bytes))).Replace("-", "").ToLowerInvariant()
    }
    finally {
        $sha.Dispose()
    }
}

function Copy-JsonObject($Value) {
    return ($Value | ConvertTo-Json -Depth 100 -Compress | ConvertFrom-Json)
}

function Remove-UnsupportedMachineLimits($Node) {
    if ($null -eq $Node) {
        return
    }

    if ($Node -is [System.Array]) {
        foreach ($item in $Node) {
            Remove-UnsupportedMachineLimits $item
        }
        return
    }

    $properties = @($Node.psobject.Properties)
    foreach ($property in $properties) {
        if ($property.Name -in @("maximum", "minimum") -and $property.Value -is [ValueType]) {
            $numericValue = [double]$property.Value
            # NSwag/NJsonSchema reads numeric bounds as Decimal. Values equal to
            # Double.MaxValue are machine-generated and add no useful domain rule.
            if ([Math]::Abs($numericValue) -gt 79228162514264337593543950335.0) {
                $Node.psobject.Properties.Remove($property.Name)
                continue
            }
        }
        Remove-UnsupportedMachineLimits $property.Value
    }
}

$httpClient = [System.Net.Http.HttpClient]::new()
$httpClient.Timeout = [TimeSpan]::FromSeconds(30)
try {
    $rawBytes = $httpClient.GetByteArrayAsync($Uri).GetAwaiter().GetResult()
}
finally {
    $httpClient.Dispose()
}
$rawJson = [System.Text.Encoding]::UTF8.GetString($rawBytes)
$source = $rawJson | ConvertFrom-Json
$capturedAt = [DateTimeOffset]::UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
$sourceHash = Get-Sha256 $rawBytes

$rawPath = Join-Path $resolvedOutputRoot "oilcasex.v1.raw.json"
[System.IO.File]::WriteAllText($rawPath, $rawJson, $utf8NoBom)

$mappings = @(
    [ordered]@{
        ToolName = "list_wellpads"
        Method = "get"
        Path = "/Api/V1/Purchased/Wellpad"
        OperationId = "listWellpads"
        Summary = "List purchased wellpads"
        Description = "Returns wellpads visible to the delegated user and team."
        Status = "enabled-read"
    },
    [ordered]@{
        ToolName = "get_wellpad"
        Method = "get"
        Path = "/Api/V1/Purchased/Wellpad/{wellpadId}"
        OperationId = "getWellpad"
        Summary = "Get a purchased wellpad"
        Description = "Returns one wellpad after OilCaseX authorization and team-scope checks."
        Status = "enabled-read"
    },
    [ordered]@{
        ToolName = "list_boreholes"
        Method = "get"
        Path = "/Api/V1/Purchased/Borehole/All"
        OperationId = "listBoreholes"
        Summary = "List purchased boreholes"
        Description = "Returns boreholes visible to the delegated user and team."
        Status = "enabled-read"
    },
    [ordered]@{
        ToolName = "get_borehole"
        Method = "get"
        Path = "/Api/V1/Purchased/Borehole/BoreholeInfo/{boreholeId}"
        OperationId = "getBorehole"
        Summary = "Get borehole information"
        Description = "Returns normalized information for one authorized borehole."
        Status = "enabled-read"
    },
    [ordered]@{
        ToolName = "get_borehole_production"
        Method = "get"
        Path = "/Api/V1/Production/Info/Borehole/{boreholeId}"
        OperationId = "getBoreholeProduction"
        Summary = "Get borehole production information"
        Description = "Returns production information for one authorized borehole."
        Status = "enabled-read"
    },
    [ordered]@{
        ToolName = "create_purchased_borehole"
        Method = "post"
        Path = "/Api/V1/Purchased/Borehole"
        OperationId = "createPurchasedBorehole"
        Summary = "Create a purchased borehole"
        Description = "Creates a borehole after API preflight, confirmation and idempotency checks."
        Status = "blocked-until-preflight"
    },
    [ordered]@{
        ToolName = "validate_borehole_purchase"
        Method = "post"
        Path = "/Api/V1/Purchased/Borehole/Validate"
        OperationId = "validatePurchasedBorehole"
        Summary = "Validate and preview a purchased borehole"
        Description = "Runs the OilCaseX domain preflight without persisting a borehole."
        Status = "enabled-read"
    }
)

$curatedPaths = [ordered]@{}
$mappingManifest = [System.Collections.Generic.List[object]]::new()

foreach ($mapping in $mappings) {
    $pathProperty = $source.paths.psobject.Properties[$mapping.Path]
    if ($null -eq $pathProperty) {
        throw "Required path is missing from source OpenAPI: $($mapping.Path)"
    }

    $methodProperty = $pathProperty.Value.psobject.Properties[$mapping.Method]
    if ($null -eq $methodProperty) {
        throw "Required method is missing from source OpenAPI: $($mapping.Method.ToUpper()) $($mapping.Path)"
    }

    $operation = Copy-JsonObject $methodProperty.Value
    $operation | Add-Member -NotePropertyName "operationId" -NotePropertyValue $mapping.OperationId -Force
    $operation | Add-Member -NotePropertyName "summary" -NotePropertyValue $mapping.Summary -Force
    $operation | Add-Member -NotePropertyName "description" -NotePropertyValue $mapping.Description -Force
    $operation | Add-Member -NotePropertyName "security" -NotePropertyValue @([pscustomobject]@{ Bearer = @() }) -Force
    $operation | Add-Member -NotePropertyName "x-mcp-tool-name" -NotePropertyValue $mapping.ToolName -Force
    $operation | Add-Member -NotePropertyName "x-mcp-status" -NotePropertyValue $mapping.Status -Force

    $responses = $operation.responses
    $standardResponses = [ordered]@{
        "401" = "Authentication is missing or invalid."
        "403" = "The delegated user is not allowed to access this resource."
        "429" = "The upstream API rate limit was exceeded."
        "502" = "The upstream OilCaseX API is unavailable."
    }
    foreach ($status in $standardResponses.Keys) {
        if ($null -eq $responses.psobject.Properties[$status]) {
            $responses | Add-Member -NotePropertyName $status -NotePropertyValue ([pscustomobject]@{
                description = $standardResponses[$status]
            })
        }
    }

    if ($null -eq $curatedPaths[$mapping.Path]) {
        $curatedPaths[$mapping.Path] = [ordered]@{}
    }
    $curatedPaths[$mapping.Path][$mapping.Method] = $operation

    $mappingManifest.Add([pscustomobject]@{
        toolName = $mapping.ToolName
        status = $mapping.Status
        method = $mapping.Method.ToUpper()
        path = $mapping.Path
        operationId = $mapping.OperationId
    })
}

$components = Copy-JsonObject $source.components
$schemaNormalization = "Removed machine-generated numeric bounds outside Decimal range for NSwag compatibility."
Remove-UnsupportedMachineLimits $components
$components | Add-Member -NotePropertyName "securitySchemes" -NotePropertyValue ([pscustomobject]@{
    Bearer = [pscustomobject]@{
        type = "http"
        scheme = "bearer"
        bearerFormat = "JWT"
        description = "Delegated OilCaseX user access token."
    }
}) -Force

$curated = [ordered]@{
    openapi = [string]$source.openapi
    info = [pscustomobject]@{
        title = "OilCaseX MCP API contract"
        version = [string]$source.info.version
        description = "Curated REST contract consumed by OilCaseX.McpServer."
    }
    servers = @([pscustomobject]@{
        url = "https://x.stg.oilcase.ru"
        description = "Staging OilCaseX REST API; production URL is deployment configuration."
    })
    security = @([pscustomobject]@{ Bearer = @() })
    paths = [pscustomobject]$curatedPaths
    components = $components
    "x-oilcasex-source" = [pscustomobject]@{
        url = $Uri
        sha256 = $sourceHash
        sourcePathCount = @($source.paths.psobject.Properties).Count
        sourceSchemaCount = @($source.components.schemas.psobject.Properties).Count
        schemaNormalization = $schemaNormalization
    }
}

$curatedJson = $curated | ConvertTo-Json -Depth 100
$curatedPath = Join-Path $resolvedOutputRoot "oilcasex.v1.mcp.json"
[System.IO.File]::WriteAllText($curatedPath, $curatedJson, $utf8NoBom)
$curatedHash = Get-Sha256 ([System.IO.File]::ReadAllBytes($curatedPath))

$manifest = [ordered]@{
    source = [pscustomobject]@{
        url = $Uri
        capturedAt = $capturedAt
        sha256 = $sourceHash
        rawSnapshot = "oilcasex.v1.raw.json"
    }
    curated = [pscustomobject]@{
        snapshot = "oilcasex.v1.mcp.json"
        sha256 = $curatedHash
        operationCount = $mappingManifest.Count
    }
    mappings = @($mappingManifest)
}
$manifestPath = Join-Path $resolvedOutputRoot "oilcasex.v1.mcp.manifest.json"
[System.IO.File]::WriteAllText(
    $manifestPath,
    ($manifest | ConvertTo-Json -Depth 100),
    $utf8NoBom)

Write-Host "Raw snapshot: $rawPath"
Write-Host "Raw SHA-256: $sourceHash"
Write-Host "Curated snapshot: $curatedPath"
Write-Host "Curated SHA-256: $curatedHash"
Write-Host "MVP mappings: $($mappingManifest.Count)"
