# ============================================
# SSM port-forward tunnel to private Petel RDS
# ============================================
# Opens localhost:<LocalPort> -> RDS:5432 via the env EC2 box.
# Keep-alive TCP probes prevent Session Manager idle timeout.
#
# Examples:
#   .\infra\aws\Open-Aws-Rds-Tunnel.ps1
#   .\infra\aws\Open-Aws-Rds-Tunnel.ps1 -Environment prod
#   .\infra\aws\Open-Aws-Rds-Tunnel.ps1 -LocalPort 15432 -KeepAliveSeconds 300
# ============================================

param(
    [ValidateSet('test', 'prod')]
    [string]$Environment = 'test',

    [int]$LocalPort = 15432,

    [ValidateRange(30, 3600)]
    [int]$KeepAliveSeconds = 300,

    [string]$Region = 'il-central-1'
)

$ErrorActionPreference = 'Stop'

$names = @{
    test = @{ Ec2 = 'petel-test-app'; Db = 'petel-test-pg'; PasswordParam = '/petel/test/db/password' }
    prod = @{ Ec2 = 'petel-prod-app'; Db = 'petel-prod-pg'; PasswordParam = '/petel/prod/db/password' }
}[$Environment]

function Write-Step([string]$Message) {
    Write-Host ""
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Get-RunningInstanceId([string]$NameTag) {
    $id = aws ec2 describe-instances `
        --region $Region `
        --filters "Name=tag:Name,Values=$NameTag" "Name=instance-state-name,Values=running" `
        --query 'Reservations[0].Instances[0].InstanceId' `
        --output text
    if (-not $id -or $id -eq 'None') {
        throw "No running EC2 instance tagged Name=$NameTag in $Region"
    }
    return $id
}

function Get-RdsHostname([string]$DbId) {
    $hostName = aws rds describe-db-instances `
        --region $Region `
        --db-instance-identifier $DbId `
        --query 'DBInstances[0].Endpoint.Address' `
        --output text
    if (-not $hostName -or $hostName -eq 'None') {
        throw "RDS instance $DbId not found in $Region"
    }
    return $hostName
}

function Test-LocalPortFree([int]$Port) {
    $listener = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue
    if ($listener) {
        throw "Local port $Port is already in use. Stop the other process or pass -LocalPort."
    }
}

function Send-KeepAlive([int]$Port) {
    $client = [System.Net.Sockets.TcpClient]::new()
    try {
        $async = $client.BeginConnect('127.0.0.1', $Port, $null, $null)
        $ok = $async.AsyncWaitHandle.WaitOne(3000)
        if (-not $ok) {
            throw "connect timed out"
        }
        $client.EndConnect($async)
        return $true
    }
    catch {
        Write-Warning "Keep-alive failed: $($_.Exception.Message)"
        return $false
    }
    finally {
        $client.Close()
    }
}

Write-Step "Resolve $Environment targets"
$instanceId = Get-RunningInstanceId $names.Ec2
$rdsHost = Get-RdsHostname $names.Db
Test-LocalPortFree $LocalPort

Write-Host "EC2:  $instanceId ($($names.Ec2))"
Write-Host "RDS:  $rdsHost"
Write-Host "Local: 127.0.0.1:$LocalPort -> $rdsHost`:5432"
Write-Host "Password SSM: $($names.PasswordParam)"
Write-Host ""
Write-Host "pgAdmin: Host=127.0.0.1  Port=$LocalPort  DB=petelappdb  User=peteladmin" -ForegroundColor Green
Write-Host "Ctrl+C stops the tunnel." -ForegroundColor Yellow

$awsArgs = @(
    'ssm', 'start-session',
    '--target', $instanceId,
    '--region', $Region,
    '--document-name', 'AWS-StartPortForwardingSessionToRemoteHost',
    '--parameters', "host=$rdsHost,portNumber=5432,localPortNumber=$LocalPort"
)

Write-Step "Start SSM port-forward"
$tunnel = Start-Process -FilePath 'aws' -ArgumentList $awsArgs -PassThru -NoNewWindow

$ready = $false
for ($i = 0; $i -lt 30; $i++) {
    Start-Sleep -Seconds 1
    if ($tunnel.HasExited) {
        throw "SSM session exited early (code $($tunnel.ExitCode)). Is Session Manager Plugin installed?"
    }
    try {
        $probe = [System.Net.Sockets.TcpClient]::new()
        $probe.Connect('127.0.0.1', $LocalPort)
        $probe.Close()
        $ready = $true
        break
    }
    catch {
        # still opening
    }
}

if (-not $ready) {
    if (-not $tunnel.HasExited) { Stop-Process -Id $tunnel.Id -Force -ErrorAction SilentlyContinue }
    throw "Port $LocalPort did not open within 30s"
}

Write-Host "Tunnel ready. Keep-alive every $KeepAliveSeconds seconds." -ForegroundColor Green

try {
    while (-not $tunnel.HasExited) {
        Start-Sleep -Seconds $KeepAliveSeconds
        if ($tunnel.HasExited) { break }
        $at = Get-Date -Format 'HH:mm:ss'
        if (Send-KeepAlive $LocalPort) {
            Write-Host "[$at] keep-alive ok" -ForegroundColor DarkGray
        }
    }
    Write-Warning "SSM session ended (exit $($tunnel.ExitCode))"
}
finally {
    if ($tunnel -and -not $tunnel.HasExited) {
        Write-Step "Stopping tunnel"
        Stop-Process -Id $tunnel.Id -Force -ErrorAction SilentlyContinue
    }
}
