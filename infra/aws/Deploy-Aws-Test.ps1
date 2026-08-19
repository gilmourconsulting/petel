# ============================================
# Deploy ATH + Assistants (test) to the AWS test EC2
# ============================================
# Requires Setup-Aws-Test-Infrastructure.ps1 first.
# Publishes linux-x64, uploads to S3, installs via SSM.
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
$InstanceId = aws ssm get-parameter --region $Region --name /petel/test/ec2/instance-id --query Parameter.Value --output text
$DbConn = aws ssm get-parameter --region $Region --name /petel/test/db/connection --with-decryption --query Parameter.Value --output text
$PublicIp = aws ssm get-parameter --region $Region --name /petel/test/ec2/public-ip --query Parameter.Value --output text

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

Write-Host "Deploy AWS test  |  $InstanceId  |  $PublicIp" -ForegroundColor Cyan

$stage = Join-Path $env:TEMP 'petel-aws-test-publish'
New-Item -ItemType Directory -Force -Path $stage | Out-Null

if (-not $SkipBuild) {
    Write-Step "dotnet publish (linux-x64)"
    if (-not $BlazorOnly) {
        Publish-App (Join-Path $Root 'PetelATH\PetelATH.Api\PetelATH.Api.csproj') (Join-Path $stage 'ath-api')
        Publish-App (Join-Path $Root 'PetelAssistants\PetelAssistants.Api\PetelAssistants.Api.csproj') (Join-Path $stage 'assist-api')
    }
    if (-not $ApiOnly) {
        Publish-App (Join-Path $Root 'PetelATH\PetelATH.BlazorServer\PetelATH.BlazorServer.csproj') (Join-Path $stage 'ath-blazor')
        Publish-App (Join-Path $Root 'PetelAssistants\PetelAssistants.BlazorServer\PetelAssistants.BlazorServer.csproj') (Join-Path $stage 'assist-blazor')
    }
}

Write-Step "Package + upload to s3://$Bucket/test/"
$tar = Join-Path $env:TEMP 'petel-test-apps.tgz'
if (Test-Path $tar) { Remove-Item $tar -Force }
Push-Location $stage
tar -czf $tar *
Pop-Location
aws s3 cp $tar "s3://$Bucket/test/apps.tgz" --region $Region | Out-Null
aws s3 cp (Join-Path $Here 'nginx-test.conf') "s3://$Bucket/test/nginx-test.conf" --region $Region | Out-Null
aws s3 cp (Join-Path $Here 'deploy-test.sh') "s3://$Bucket/test/deploy-test.sh" --region $Region | Out-Null
Get-ChildItem (Join-Path $Here 'systemd') -Filter '*.service' | ForEach-Object {
    aws s3 cp $_.FullName "s3://$Bucket/test/systemd/$($_.Name)" --region $Region | Out-Null
}

# Env files: APIs use localhost; copy Azure secrets if present in SSM, else placeholder
Write-Step "Write SSM app env (connection string + Azure secrets if az is logged in)"
$athSecrets = ""
$assistSecrets = ""
try {
    $jwt = az webapp config appsettings list --name petel-test-api --resource-group petel-test-rg --query "[?name=='Security__Jwt__SecretKey'].value | [0]" -o tsv 2>$null
    $enc = az webapp config appsettings list --name petel-test-api --resource-group petel-test-rg --query "[?name=='Security__DataEncryption__EncryptionKey'].value | [0]" -o tsv 2>$null
    $emailFrom = az webapp config appsettings list --name petel-test-api --resource-group petel-test-rg --query "[?name=='Email__FromAddress'].value | [0]" -o tsv 2>$null
    $emailUser = az webapp config appsettings list --name petel-test-api --resource-group petel-test-rg --query "[?name=='Email__Username'].value | [0]" -o tsv 2>$null
    $emailPass = az webapp config appsettings list --name petel-test-api --resource-group petel-test-rg --query "[?name=='Email__Password'].value | [0]" -o tsv 2>$null
    if ($jwt) {
        $athSecrets = @"

Security__Jwt__SecretKey=$jwt
Security__DataEncryption__EncryptionKey=$enc
Email__FromAddress=$emailFrom
Email__Username=$emailUser
Email__Password=$emailPass
"@
        Write-Host "  copied ATH secrets from Azure petel-test-api"
    }
    $ajwt = az webapp config appsettings list --name petel-assist-test-api --resource-group petel-assist-test-rg --query "[?name=='Security__Jwt__SecretKey'].value | [0]" -o tsv 2>$null
    $aenc = az webapp config appsettings list --name petel-assist-test-api --resource-group petel-assist-test-rg --query "[?name=='Security__DataEncryption__EncryptionKey'].value | [0]" -o tsv 2>$null
    if ($ajwt) {
        $assistSecrets = @"

Security__Jwt__SecretKey=$ajwt
Security__DataEncryption__EncryptionKey=$aenc
"@
        Write-Host "  copied Assistants secrets from Azure petel-assist-test-api"
    }
} catch {
    Write-Host "  Azure secret copy skipped (az not available or no access)"
}
$athApiEnv = @"
ConnectionStrings__DefaultConnection=$DbConn
ConnectionStrings__HangfireConnection=$DbConn
ASPNETCORE_ENVIRONMENT=Staging
ASPNETCORE_URLS=http://127.0.0.1:5082
$athSecrets
"@
$athBlazorEnv = @"
ApiSettings__BaseUrl=http://127.0.0.1:5082/api
ASPNETCORE_ENVIRONMENT=Staging
PORT=5293
"@
$assistApiEnv = @"
ConnectionStrings__DefaultConnection=$DbConn
ASPNETCORE_ENVIRONMENT=Staging
ASPNETCORE_URLS=http://127.0.0.1:5238
$assistSecrets
"@
$assistBlazorEnv = @"
ApiSettings__BaseUrl=http://127.0.0.1:5238/api
ASPNETCORE_ENVIRONMENT=Staging
PORT=5088
"@
aws ssm put-parameter --region $Region --name /petel/test/env/ath-api --type SecureString --value $athApiEnv --overwrite | Out-Null
aws ssm put-parameter --region $Region --name /petel/test/env/ath-blazor --type SecureString --value $athBlazorEnv --overwrite | Out-Null
aws ssm put-parameter --region $Region --name /petel/test/env/assist-api --type SecureString --value $assistApiEnv --overwrite | Out-Null
aws ssm put-parameter --region $Region --name /petel/test/env/assist-blazor --type SecureString --value $assistBlazorEnv --overwrite | Out-Null

Write-Step "SSM install on EC2"
$paramsPath = Join-Path $env:TEMP 'petel-ssm-deploy.json'
$paramsObj = @{
    commands = @(
        "aws s3 cp s3://$Bucket/test/deploy-test.sh /tmp/deploy-test.sh --region $Region",
        "bash /tmp/deploy-test.sh $Bucket"
    )
}
[System.IO.File]::WriteAllText($paramsPath, ($paramsObj | ConvertTo-Json -Compress))
$cmdId = aws ssm send-command `
    --region $Region `
    --instance-ids $InstanceId `
    --document-name AWS-RunShellScript `
    --comment 'petel-test deploy' `
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
Write-Host "ATH default:      http://$PublicIp/"
Write-Host "ATH DNS:          http://ath-test.petel.site/ (A record to $PublicIp)"
Write-Host "Assistants DNS:   http://assist-test.petel.site/"
Write-Host "APIs listen on localhost only."
