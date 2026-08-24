param(
    [string]$ComputerName = "PMS-MB-StoreApp.PMS.LOCAL",
    [string]$SiteName = "AttendanceReporting",
    [string]$AppPoolName = "AttendanceReporting",
    [int]$Port = 5004,
    [string]$LocalPublishPath = "D:\Applications\AttendanceReporting\ServerPublish",
    [string]$RemoteAppPath = "C:\AttendanceReporting\App"
)

if (-not (Test-Path -LiteralPath $LocalPublishPath)) {
    throw "Local publish path not found: $LocalPublishPath. Run dotnet publish first."
}

$credential = Get-Credential -Message "Enter administrator credentials for $ComputerName"
$session = New-PSSession -ComputerName $ComputerName -Credential $credential

try {
    Invoke-Command -Session $session -ScriptBlock {
        param($Path)
        if (-not (Test-Path -LiteralPath $Path)) {
            New-Item -ItemType Directory -Path $Path -Force | Out-Null
        }

        $modulePaths = @(
            "$env:ProgramFiles\IIS\Asp.Net Core Module\V2\aspnetcorev2.dll",
            "${env:ProgramFiles(x86)}\IIS\Asp.Net Core Module\V2\aspnetcorev2.dll"
        )

        if (-not ($modulePaths | Where-Object { Test-Path -LiteralPath $_ })) {
            throw "ASP.NET Core IIS Hosting Bundle is not installed on this server. Install the .NET 10 Hosting Bundle first."
        }
    } -ArgumentList $RemoteAppPath

    $offline = Join-Path $RemoteAppPath "app_offline.htm"
    Invoke-Command -Session $session -ScriptBlock {
        param($OfflinePath)
        "Deployment in progress" | Set-Content -LiteralPath $OfflinePath -Encoding UTF8
    } -ArgumentList $offline

    Copy-Item -LiteralPath (Join-Path $LocalPublishPath "*") -Destination $RemoteAppPath -ToSession $session -Recurse -Force

    Invoke-Command -Session $session -ScriptBlock {
        param($SiteName, $AppPoolName, $Port, $RemoteAppPath, $OfflinePath)

        Import-Module WebAdministration

        if (-not (Test-Path "IIS:\AppPools\$AppPoolName")) {
            New-WebAppPool -Name $AppPoolName | Out-Null
        }

        Set-ItemProperty "IIS:\AppPools\$AppPoolName" -Name managedRuntimeVersion -Value ""
        Set-ItemProperty "IIS:\AppPools\$AppPoolName" -Name enable32BitAppOnWin64 -Value $false

        if (-not (Test-Path "IIS:\Sites\$SiteName")) {
            New-Website -Name $SiteName -Port $Port -PhysicalPath $RemoteAppPath -ApplicationPool $AppPoolName | Out-Null
        } else {
            Set-ItemProperty "IIS:\Sites\$SiteName" -Name physicalPath -Value $RemoteAppPath

            $site = Get-Website -Name $SiteName
            $hasPort = $site.Bindings.Collection | Where-Object { $_.protocol -eq "http" -and $_.bindingInformation -eq "*:$Port:" }
            if (-not $hasPort) {
                New-WebBinding -Name $SiteName -Protocol http -Port $Port -IPAddress "*" | Out-Null
            }
        }

        icacls $RemoteAppPath /grant "IIS AppPool\$AppPoolName:(OI)(CI)RX" /T | Out-Null

        if (Test-Path -LiteralPath $OfflinePath) {
            Remove-Item -LiteralPath $OfflinePath -Force
        }

        Start-WebAppPool -Name $AppPoolName -ErrorAction SilentlyContinue
        Start-Website -Name $SiteName -ErrorAction SilentlyContinue
    } -ArgumentList $SiteName, $AppPoolName, $Port, $RemoteAppPath, $offline
}
finally {
    if ($session) {
        Remove-PSSession $session
    }
}

"Deployment completed: http://10.51.0.100:$Port/"
