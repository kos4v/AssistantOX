$ErrorActionPreference = 'Stop'

$distro = 'Ubuntu-24.04'
$listenPort = 8000
$wslAddress = $null

for ($attempt = 1; $attempt -le 12; $attempt++) {
    $addresses = wsl.exe -d $distro -u root -- hostname -I 2>$null
    if ($LASTEXITCODE -eq 0 -and $addresses) {
        $candidate = ($addresses.Trim() -split '\s+')[0]
        if ($candidate -match '^\d{1,3}(\.\d{1,3}){3}$') {
            $wslAddress = $candidate
            break
        }
    }

    Start-Sleep -Seconds 5
}

if (-not $wslAddress) {
    throw "Could not determine the IP address for $distro"
}

netsh interface portproxy delete v4tov4 listenaddress=0.0.0.0 listenport=$listenPort 2>$null
netsh interface portproxy add v4tov4 listenaddress=0.0.0.0 listenport=$listenPort connectaddress=$wslAddress connectport=$listenPort

$existingRule = Get-NetFirewallRule -DisplayName 'vLLM OpenAI API' -ErrorAction SilentlyContinue
if (-not $existingRule) {
    $firewallRule = @{
        DisplayName = 'vLLM OpenAI API'
        Direction   = 'Inbound'
        Action      = 'Allow'
        Protocol    = 'TCP'
        LocalPort   = $listenPort
        Profile     = 'Any'
    }
    New-NetFirewallRule @firewallRule | Out-Null
} else {
    $existingRule | Set-NetFirewallRule -Enabled True -Profile Any | Out-Null
}

Write-Output "Forwarding 0.0.0.0:$listenPort to $wslAddress`:$listenPort"
