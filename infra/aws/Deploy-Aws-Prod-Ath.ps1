# ============================================
# Deploy PetelATH onto the existing AWS prod EC2 (shadow)
# ============================================
# Meitar stays on port 80. ATH is served on port 8080.
# Requires the petel-prod VPC, EC2, and RDS already created.
# ============================================

param(
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$Region = 'il-central-1'
$Here = Split-Path -Parent $MyInvocation.MyCommand.Path
$Root = Split-Path -Parent (Split-Path -Parent $Here)
$AccountId = (aws sts get-caller-identity --query Account --output text)
$Bucket = "petel-aws-deploy-$AccountId"
$InstanceId = aws ssm get-parameter --region $Region --name /petel/prod/ec2/instance-id --query Parameter.Value --output text
$DbConn = aws ssm get-parameter --region $Region --name /petel/prod/db/connection --with-decryption --query Parameter.Value --output text
$PublicIp = aws ssm get-parameter --region $Region --name /petel/prod/ec2/public-ip --query Parameter.Value --output text

function Write-Step([string]$Message) {
    Write-Host ""
    Write-Host $Message -ForegroundColor Yellow
}

function Get-AwsFileUri([string]$Path) {
    return 'file://' + ($Path -replace '\\', '/')
}

function Publish-App([string]$ProjectPath, [string]$OutDir) {
    Write-Host "  publish $ProjectPath"
    if (Test-Path $OutDir) { Remove-Item $OutDir -Recurse -Force }
    dotnet publish $ProjectPath -c Release -r linux-x64 --self-contained false -o $OutDir --nologo -v q
    if ($LASTEXITCODE -ne 0) { throw "publish failed: $ProjectPath" }
}

function Read-AzTsv([scriptblock]$Command) {
    $value = & $Command
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace("$value")) {
        throw "Azure read failed"
    }
    return ("$value").Trim()
}

Write-Host "Deploy AWS prod ATH shadow  |  $InstanceId  |  $PublicIp" -ForegroundColor Cyan

$stage = Join-Path $env:TEMP 'petel-aws-prod-ath-publish'
New-Item -ItemType Directory -Force -Path $stage | Out-Null

if (-not $SkipBuild) {
    Write-Step "dotnet publish (linux-x64)"
    Publish-App (Join-Path $Root 'PetelATH\PetelATH.Api\PetelATH.Api.csproj') (Join-Path $stage 'ath-api')
    Publish-App (Join-Path $Root 'PetelATH\PetelATH.BlazorServer\PetelATH.BlazorServer.csproj') (Join-Path $stage 'ath-blazor')
}

Write-Step "Package + upload to s3://$Bucket/prod/ath/"
$tar = Join-Path $env:TEMP 'petel-prod-ath-apps.tgz'
if (Test-Path $tar) { Remove-Item $tar -Force }
Push-Location $stage
tar -czf $tar *
Pop-Location
aws s3 cp $tar "s3://$Bucket/prod/ath/apps.tgz" --region $Region | Out-Null
aws s3 cp (Join-Path $Here 'nginx-prod.conf') "s3://$Bucket/prod/ath/nginx-prod.conf" --region $Region | Out-Null
aws s3 cp (Join-Path $Here 'deploy-prod-ath.sh') "s3://$Bucket/prod/ath/deploy-prod-ath.sh" --region $Region | Out-Null
aws s3 cp (Join-Path $Here 'systemd\ath-api-prod.service') "s3://$Bucket/prod/ath/systemd/ath-api-prod.service" --region $Region | Out-Null
aws s3 cp (Join-Path $Here 'systemd\ath-blazor-prod.service') "s3://$Bucket/prod/ath/systemd/ath-blazor-prod.service" --region $Region | Out-Null

Write-Step "Write SSM app env from Azure prod secrets"
$jwt = Read-AzTsv { az keyvault secret show --vault-name petel-kv-prod-6581 --name Security--Jwt--SecretKey --query value -o tsv }
$encKey = Read-AzTsv { az keyvault secret show --vault-name petel-kv-prod-6581 --name Security--DataEncryption--EncryptionKey --query value -o tsv }
$emailFrom = Read-AzTsv { az webapp config appsettings list --name petel-prod-api --resource-group petel-prod-rg --query "[?name=='Email__FromAddress'].value | [0]" -o tsv }
$emailUser = Read-AzTsv { az webapp config appsettings list --name petel-prod-api --resource-group petel-prod-rg --query "[?name=='Email__Username'].value | [0]" -o tsv }
$emailPass = Read-AzTsv { az webapp config appsettings list --name petel-prod-api --resource-group petel-prod-rg --query "[?name=='Email__Password'].value | [0]" -o tsv }
$emailHost = Read-AzTsv { az webapp config appsettings list --name petel-prod-api --resource-group petel-prod-rg --query "[?name=='Email__SmtpHost'].value | [0]" -o tsv }
$emailPort = Read-AzTsv { az webapp config appsettings list --name petel-prod-api --resource-group petel-prod-rg --query "[?name=='Email__SmtpPort'].value | [0]" -o tsv }

$athApiEnv = @"
ConnectionStrings__DefaultConnection=$DbConn
ConnectionStrings__HangfireConnection=$DbConn
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://127.0.0.1:5082
Security__Jwt__SecretKey=$jwt
Security__DataEncryption__EncryptionKey=$encKey
Email__SmtpHost=$emailHost
Email__SmtpPort=$emailPort
Email__FromAddress=$emailFrom
Email__Username=$emailUser
Email__Password=$emailPass
"@
$athBlazorEnv = @"
ApiSettings__BaseUrl=http://127.0.0.1:5082/api
ASPNETCORE_ENVIRONMENT=Production
PORT=5293
"@

$apiEnvPath = Join-Path $env:TEMP 'petel-ath-api.env'
$blazorEnvPath = Join-Path $env:TEMP 'petel-ath-blazor.env'
$utf8 = New-Object System.Text.UTF8Encoding $false
[System.IO.File]::WriteAllText($apiEnvPath, $athApiEnv.Trim() + "`n", $utf8)
[System.IO.File]::WriteAllText($blazorEnvPath, $athBlazorEnv.Trim() + "`n", $utf8)
aws ssm put-parameter --region $Region --name /petel/prod/env/ath-api --type SecureString --value (Get-AwsFileUri $apiEnvPath) --overwrite | Out-Null
aws ssm put-parameter --region $Region --name /petel/prod/env/ath-blazor --type SecureString --value (Get-AwsFileUri $blazorEnvPath) --overwrite | Out-Null
Remove-Item $apiEnvPath, $blazorEnvPath -Force
Write-Host "  stored /petel/prod/env/ath-api and ath-blazor"

Write-Step "SSM install on EC2"
$paramsPath = Join-Path $env:TEMP 'petel-ssm-prod-ath.json'
$paramsObj = @{
    commands = @(
        "aws s3 cp s3://$Bucket/prod/ath/deploy-prod-ath.sh /tmp/deploy-prod-ath.sh --region $Region",
        "sed -i 's/\r$//' /tmp/deploy-prod-ath.sh",
        "bash /tmp/deploy-prod-ath.sh $Bucket"
    )
}
[System.IO.File]::WriteAllText($paramsPath, ($paramsObj | ConvertTo-Json -Compress))
$cmdId = aws ssm send-command `
    --region $Region `
    --instance-ids $InstanceId `
    --document-name AWS-RunShellScript `
    --timeout-seconds 1800 `
    --comment 'petel-prod ath shadow deploy' `
    --parameters (Get-AwsFileUri $paramsPath) `
    --query Command.CommandId --output text

Write-Host "SSM command $cmdId - waiting..."
aws ssm wait command-executed --region $Region --command-id $cmdId --instance-id $InstanceId
$status = aws ssm get-command-invocation --region $Region --command-id $cmdId --instance-id $InstanceId --query Status --output text
if ($status -ne 'Success') {
    aws ssm get-command-invocation --region $Region --command-id $cmdId --instance-id $InstanceId --query StandardErrorContent --output text
    throw "SSM deploy failed: $status"
}

Write-Host ""
Write-Host "Deployed." -ForegroundColor Green
Write-Host "ATH shadow:  http://${PublicIp}:8080/   (Israeli IPs only)"
Write-Host "Meitar stays on port 80."
Write-Host "API listens on localhost only."
