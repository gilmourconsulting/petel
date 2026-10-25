# ============================================
# Deploy ATH (production) to the AWS prod EC2
# ============================================
# Requires Setup-Aws-Prod-Infrastructure.ps1 first.
# Publishes linux-x64 from the current checkout, uploads to S3, installs via SSM.
# HangfireConnection is intentionally omitted so this copy does not run prod jobs.
# ============================================

param(
    [switch]$ApiOnly,
    [switch]$BlazorOnly,
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
$Sha = (git -C $Root rev-parse HEAD).Trim()

function Write-Step([string]$Message) {
    Write-Host ""
    Write-Host $Message -ForegroundColor Yellow
}

function Get-AwsFileUri([string]$Path) {
    return 'file://' + ($Path -replace '\\', '/')
}

function Get-AzSetting([string]$Name) {
    $value = az webapp config appsettings list --name petel-prod-api --resource-group petel-prod-rg --query "[?name=='$Name'].value | [0]" -o tsv 2>$null
    if ($LASTEXITCODE -ne 0 -or -not $value -or $value -eq 'None') { return $null }
    if ($value -like '@Microsoft.KeyVault*' -and $value -match 'https://[^)]+') {
        $uri = $Matches[0].TrimEnd('/')
        $value = az keyvault secret show --id $uri --query value -o tsv
        if ($LASTEXITCODE -ne 0 -or -not $value) { throw "Key Vault resolve failed for $Name" }
    }
    return $value
}

function Publish-App([string]$ProjectPath, [string]$OutDir) {
    Write-Host "  publish $ProjectPath"
    if (Test-Path $OutDir) { Remove-Item $OutDir -Recurse -Force }
    dotnet publish $ProjectPath -c Release -r linux-x64 --self-contained false -o $OutDir --nologo -v q
    if ($LASTEXITCODE -ne 0) { throw "publish failed: $ProjectPath" }
}

Write-Host "Deploy AWS prod  |  $Sha  |  $InstanceId  |  $PublicIp" -ForegroundColor Cyan

$stage = Join-Path $env:TEMP 'petel-aws-prod-publish'
New-Item -ItemType Directory -Force -Path $stage | Out-Null

if (-not $SkipBuild) {
    Write-Step "dotnet publish (linux-x64)"
    if (-not $BlazorOnly) {
        Publish-App (Join-Path $Root 'PetelATH\PetelATH.Api\PetelATH.Api.csproj') (Join-Path $stage 'ath-api')
    }
    if (-not $ApiOnly) {
        Publish-App (Join-Path $Root 'PetelATH\PetelATH.BlazorServer\PetelATH.BlazorServer.csproj') (Join-Path $stage 'ath-blazor')
    }
}

Write-Step "Package + upload to s3://$Bucket/prod/"
$tar = Join-Path $env:TEMP 'petel-prod-apps.tgz'
if (Test-Path $tar) { Remove-Item $tar -Force }
Push-Location $stage
tar -czf $tar *
Pop-Location
aws s3 cp $tar "s3://$Bucket/prod/apps.tgz" --region $Region | Out-Null
aws s3 cp (Join-Path $Here 'nginx-prod.conf') "s3://$Bucket/prod/nginx-prod.conf" --region $Region | Out-Null
aws s3 cp (Join-Path $Here 'deploy-prod.sh') "s3://$Bucket/prod/deploy-prod.sh" --region $Region | Out-Null
Get-ChildItem (Join-Path $Here 'systemd-prod') -Filter '*.service' | ForEach-Object {
    aws s3 cp $_.FullName "s3://$Bucket/prod/systemd/$($_.Name)" --region $Region | Out-Null
}

Write-Step "Write SSM app env (connection string + Azure prod secrets)"
$jwt = Get-AzSetting 'Security__Jwt__SecretKey'
$enc = Get-AzSetting 'Security__DataEncryption__EncryptionKey'
$emailFrom = Get-AzSetting 'Email__FromAddress'
$emailUser = Get-AzSetting 'Email__Username'
$emailPass = Get-AzSetting 'Email__Password'
$gotenbergUrl = Get-AzSetting 'Gotenberg__BaseUrl'
$gotenbergUser = Get-AzSetting 'Gotenberg__Username'
$gotenbergPass = Get-AzSetting 'Gotenberg__Password'
if (-not $jwt -or -not $enc) { throw 'Azure petel-prod-api is missing JWT or encryption key settings' }
Write-Host "  copied ATH secrets from Azure petel-prod-api (Hangfire left unset)"

$athApiEnv = @"
ConnectionStrings__DefaultConnection=$DbConn
ConnectionStrings__HangfireConnection=
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://127.0.0.1:5082
Security__Jwt__SecretKey=$jwt
Security__DataEncryption__EncryptionKey=$enc
Email__FromAddress=$emailFrom
Email__Username=$emailUser
Email__Password=$emailPass
"@
if ($gotenbergUrl) {
    $athApiEnv += @"

Gotenberg__BaseUrl=$gotenbergUrl
Gotenberg__Username=$gotenbergUser
Gotenberg__Password=$gotenbergPass
"@
}
$athBlazorEnv = @"
ApiSettings__BaseUrl=http://127.0.0.1:5082/api
ASPNETCORE_ENVIRONMENT=Production
PORT=5293
"@
aws ssm put-parameter --region $Region --name /petel/prod/env/ath-api --type SecureString --value $athApiEnv --overwrite | Out-Null
aws ssm put-parameter --region $Region --name /petel/prod/env/ath-blazor --type SecureString --value $athBlazorEnv --overwrite | Out-Null

Write-Step "SSM install on EC2"
$paramsPath = Join-Path $env:TEMP 'petel-ssm-deploy-prod.json'
$paramsObj = @{
    commands = @(
        "aws s3 cp s3://$Bucket/prod/deploy-prod.sh /tmp/deploy-prod.sh --region $Region",
        "sed -i 's/\r$//' /tmp/deploy-prod.sh",
        "bash /tmp/deploy-prod.sh $Bucket"
    )
}
$utf8 = New-Object System.Text.UTF8Encoding $false
[System.IO.File]::WriteAllText($paramsPath, ($paramsObj | ConvertTo-Json -Compress), $utf8)
$cmdId = aws ssm send-command `
    --region $Region `
    --instance-ids $InstanceId `
    --document-name AWS-RunShellScript `
    --comment "petel-prod deploy $Sha" `
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
Write-Host "Deployed $Sha" -ForegroundColor Green
Write-Host "ATH:  http://$PublicIp/"
Write-Host "API listens on localhost only. Hangfire is not configured."
