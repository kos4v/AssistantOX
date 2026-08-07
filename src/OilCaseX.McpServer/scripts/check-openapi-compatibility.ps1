[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PreviousPath,
    [Parameter(Mandatory = $true)]
    [string]$CurrentPath
)

$ErrorActionPreference = "Stop"

function Read-Document([string]$Path) {
    $resolved = [System.IO.Path]::GetFullPath($Path)
    if (-not (Test-Path -LiteralPath $resolved)) {
        throw "OpenAPI document does not exist: $resolved"
    }
    return Get-Content -Raw -LiteralPath $resolved | ConvertFrom-Json
}

$previous = Read-Document $PreviousPath
$current = Read-Document $CurrentPath
$breaking = [System.Collections.Generic.List[string]]::new()

foreach ($pathProperty in $previous.paths.psobject.Properties) {
    $currentPathProperty = $current.paths.psobject.Properties[$pathProperty.Name]
    if ($null -eq $currentPathProperty) {
        $breaking.Add("Removed path: $($pathProperty.Name)")
        continue
    }

    foreach ($methodProperty in $pathProperty.Value.psobject.Properties) {
        if ($null -eq $currentPathProperty.Value.psobject.Properties[$methodProperty.Name]) {
            $breaking.Add("Removed operation: $($methodProperty.Name.ToUpper()) $($pathProperty.Name)")
            continue
        }

        $oldOperation = $methodProperty.Value
        $newOperation = $currentPathProperty.Value.psobject.Properties[$methodProperty.Name].Value
        if ($oldOperation.operationId -ne $newOperation.operationId) {
            $breaking.Add("Changed operationId: $($pathProperty.Name) $($methodProperty.Name): '$($oldOperation.operationId)' → '$($newOperation.operationId)'")
        }

        foreach ($parameter in @($oldOperation.parameters)) {
            if ($parameter.required -eq $true) {
                $newParameter = @($newOperation.parameters) | Where-Object {
                    $_.name -eq $parameter.name -and $_.in -eq $parameter.in
                } | Select-Object -First 1
                if ($null -eq $newParameter) {
                    $breaking.Add("Removed required parameter: $($methodProperty.Name.ToUpper()) $($pathProperty.Name) $($parameter.in)/$($parameter.name)")
                }
            }
        }
    }
}

if ($breaking.Count -gt 0) {
    $breaking | ForEach-Object { Write-Error $_ }
    exit 1
}

Write-Host "OpenAPI compatibility check passed."
