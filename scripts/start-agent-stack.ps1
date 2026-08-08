param(
    [switch]$NoBuild
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$projects = @()

function Start-Project {
    param(
        [string]$Name,
        [string]$Project,
        [string]$Urls,
        [hashtable]$Environment
    )

    $oldUrls = $env:ASPNETCORE_URLS
    $oldEnvironment = $env:ASPNETCORE_ENVIRONMENT
    $env:ASPNETCORE_URLS = $Urls
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    foreach ($key in $Environment.Keys) { Set-Item "Env:$key" $Environment[$key] }

    $arguments = @('run', '--project', $Project, '--no-launch-profile')
    if ($NoBuild) { $arguments += '--no-build' }
    Write-Host "Starting $Name ($Urls)"
    $process = Start-Process dotnet -ArgumentList $arguments -WorkingDirectory $repoRoot -PassThru
    $script:projects += $process

    $env:ASPNETCORE_URLS = $oldUrls
    $env:ASPNETCORE_ENVIRONMENT = $oldEnvironment
    return $process
}

try {
    $mcp = Start-Project 'OilCaseX.McpServer' 'src/OilCaseX.McpServer/OilCaseX.McpServer.csproj' 'http://localhost:5089' @{}
    Start-Sleep -Seconds 2

    $agent = Start-Project 'OilCaseX.Agent.Api' 'src/OilCaseX.Agent.Api/OilCaseX.Agent.Api.csproj' 'http://localhost:52225' @{
        'OILCASE_MCP_URL' = 'http://localhost:5089/mcp'
    }
    Start-Sleep -Seconds 2

    $ui = Start-Project 'OilCaseX.Agent.Ui' 'src/OilCaseX.Agent.Ui/OilCaseX.Agent.Ui.csproj' 'http://localhost:52227' @{
        'AgentUi__HubUrl' = 'http://localhost:52225/hubs/agent'
    }

    Write-Host ''
    Write-Host 'OilCaseX local stack is running:'
    Write-Host '  UI:   http://localhost:52227'
    Write-Host '  API:  http://localhost:52225'
    Write-Host '  MCP:  http://localhost:5089/mcp'
    Write-Host 'Press Ctrl+C to stop all projects.'

    while ($true) {
        $running = $projects | Where-Object { -not $_.HasExited }
        if ($running.Count -eq 0) { break }
        Start-Sleep -Seconds 1
    }
}
finally {
    foreach ($process in $projects) {
        if ($process -and -not $process.HasExited) {
            Stop-Process -Id $process.Id -Force
        }
    }
}
