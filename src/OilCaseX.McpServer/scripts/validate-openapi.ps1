[CmdletBinding()]
param(
    [string]$SnapshotPath = "..\contracts\openapi\oilcasex.v1.mcp.json"
)

$ErrorActionPreference = "Stop"
$resolvedPath = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot $SnapshotPath))
if (-not (Test-Path -LiteralPath $resolvedPath)) {
    throw "OpenAPI snapshot does not exist: $resolvedPath"
}

$document = Get-Content -Raw -LiteralPath $resolvedPath | ConvertFrom-Json
$errors = [System.Collections.Generic.List[string]]::new()

if ($document.openapi -ne "3.0.4") {
    $errors.Add("Expected OpenAPI 3.0.4, got '$($document.openapi)'.")
}
if ($null -eq $document.servers -or @($document.servers).Count -eq 0) {
    $errors.Add("The curated snapshot must define at least one server.")
}
if ($null -eq $document.components.securitySchemes.Bearer) {
    $errors.Add("Bearer security scheme is missing.")
}
if ($null -eq $document.security.Bearer -and $null -eq $document.security[0].Bearer) {
    $errors.Add("Global Bearer security requirement is missing.")
}

$expected = [ordered]@{
    "/Api/V1/Purchased/Wellpad|get" = "listWellpads"
    "/Api/V1/Purchased/Wellpad/{wellpadId}|get" = "getWellpad"
    "/Api/V1/Purchased/Borehole/All|get" = "listBoreholes"
    "/Api/V1/Purchased/Borehole/BoreholeInfo/{boreholeId}|get" = "getBorehole"
    "/Api/V1/Production/Info/Borehole/{boreholeId}|get" = "getBoreholeProduction"
    "/Api/V1/Purchased/Borehole|post" = "createPurchasedBorehole"
}

$operationIds = [System.Collections.Generic.HashSet[string]]::new()
foreach ($pathProperty in $document.paths.psobject.Properties) {
    foreach ($methodProperty in $pathProperty.Value.psobject.Properties) {
        $key = "$($pathProperty.Name)|$($methodProperty.Name.ToLowerInvariant())"
        if (-not $expected.Contains($key)) {
            $errors.Add("Unexpected curated operation: $key")
            continue
        }

        $operation = $methodProperty.Value
        if ([string]::IsNullOrWhiteSpace($operation.operationId)) {
            $errors.Add("Missing operationId: $key")
        }
        elseif (-not $operationIds.Add($operation.operationId)) {
            $errors.Add("Duplicate operationId: $($operation.operationId)")
        }
        elseif ($operation.operationId -ne $expected[$key]) {
            $errors.Add("Unexpected operationId for ${key}: '$($operation.operationId)'")
        }
        if ([string]::IsNullOrWhiteSpace($operation.summary)) {
            $errors.Add("Missing summary: $key")
        }
        if ([string]::IsNullOrWhiteSpace($operation.description)) {
            $errors.Add("Missing description: $key")
        }
        if ($null -eq $operation.responses -or @($operation.responses.psobject.Properties).Count -eq 0) {
            $errors.Add("Missing responses: $key")
        }
        if ($null -eq $operation.security[0].Bearer) {
            $errors.Add("Missing operation-level Bearer security: $key")
        }
    }
}

foreach ($key in $expected.Keys) {
    $parts = $key.Split('|', 2)
    $pathProperty = $document.paths.psobject.Properties[$parts[0]]
    if ($null -eq $pathProperty -or $null -eq $pathProperty.Value.psobject.Properties[$parts[1]]) {
        $errors.Add("Expected MVP operation is missing: $key")
    }
}

if ($errors.Count -gt 0) {
    $errors | ForEach-Object { Write-Error $_ }
    exit 1
}

Write-Host "OpenAPI validation passed: $resolvedPath"
Write-Host "Validated operations: $($expected.Count)"
