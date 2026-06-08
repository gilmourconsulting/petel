param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('test', 'production')]
    [string]$Environment,
    [switch]$Update
)

$ErrorActionPreference = "Stop"
$ProgressPreference    = "SilentlyContinue"

$envConfig = @{
    'test'       = @{
        ResourceGroup  = 'petel-test-rg'
        Location       = 'israelcentral'
        AcaEnvName     = 'petel-test-gotenberg-env'
        AppName        = 'petel-test-gotenberg'
        ApiAppName     = 'petel-test-api'
        MaxReplicas    = 1
    }
    'production' = @{
        ResourceGroup  = 'petel-prod-rg'
        Location       = 'israelcentral'
        AcaEnvName     = 'petel-prod-gotenberg-env'
        AppName        = 'petel-prod-gotenberg'
        ApiAppName     = 'petel-prod-api'
        MaxReplicas    = 2
    }
}

$GotenbergImage = 'gotenberg/gotenberg:8'
$cfg         = $envConfig[$Environment]
$rg          = $cfg.ResourceGroup
$location    = $cfg.Location
$acaEnvName  = $cfg.AcaEnvName
$appName     = $cfg.AppName
$apiAppName  = $cfg.ApiAppName
$maxReplicas = $cfg.MaxReplicas

function Get-SecretValue {
    param([string]$Prompt)
    Write-Host ""
    Write-Host $Prompt -ForegroundColor Cyan
    $secureValue = Read-Host -AsSecureString "Value"
    $bstr = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureValue)
    try { return [System.Runtime.InteropServices.Marshal]::PtrToStringAuto($bstr) }
    finally { [System.Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr) }
}

Write-Host ""
Write-Host "======================================================" -ForegroundColor Cyan
Write-Host "  Gotenberg Setup  |  Environment: $Environment" -ForegroundColor Cyan
Write-Host "======================================================" -ForegroundColor Cyan
Write-Host ""

if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    Write-Error "Azure CLI (az) is not installed or not in PATH."
}

$accountInfo = az account show --output json 2>$null | ConvertFrom-Json
if (-not $accountInfo) { Write-Error "Not logged in to Azure. Run 'az login' first." }
Write-Host "Logged in as: $($accountInfo.user.name)" -ForegroundColor Green
Write-Host ""
Write-Host "Gotenberg basic auth credentials" -ForegroundColor Yellow
Write-Host "(These will be stored in the ACA container env and in the API App Service settings)"
$gotenbergUser = Read-Host "Username (e.g. gotenberguser)"
$gotenbergPass = Get-SecretValue "Password for Gotenberg basic auth"
Write-Host ""

Write-Host "Checking ACA Environment '$acaEnvName'..." -ForegroundColor Cyan
$ErrorActionPreference = "Continue"
$acaEnvCheck = az containerapp env show --name $acaEnvName --resource-group $rg --output json 2>&1
$ErrorActionPreference = "Stop"
$acaEnvExists = if ($LASTEXITCODE -eq 0) { $acaEnvCheck | ConvertFrom-Json } else { $null }
if (-not $acaEnvExists) {
    Write-Host "   Creating ACA Environment (this may take ~2 min)..."
    az containerapp env create --name $acaEnvName --resource-group $rg --location $location --output none
    Write-Host "   ACA Environment created." -ForegroundColor Green
} else {
    Write-Host "   ACA Environment already exists." -ForegroundColor Green
}

$ErrorActionPreference = "Continue"
$appCheck = az containerapp show --name $appName --resource-group $rg --output json 2>&1
$ErrorActionPreference = "Stop"
$appExists = if ($LASTEXITCODE -eq 0) { $appCheck | ConvertFrom-Json } else { $null }
if ($appExists -and -not $Update) {
    Write-Host ""
    Write-Host "Container App '$appName' already exists. Use -Update to redeploy." -ForegroundColor Green
} else {
    if ($appExists) {
        Write-Host "Updating Container App '$appName'..." -ForegroundColor Cyan
        az containerapp update `
            --name $appName --resource-group $rg --image $GotenbergImage `
            --set-env-vars "GOTENBERG_API_BASICAUTH_USERNAME=$gotenbergUser" "GOTENBERG_API_BASICAUTH_PASSWORD=$gotenbergPass" `
            --output none
    } else {
        Write-Host "Creating Container App '$appName'..." -ForegroundColor Cyan
        az containerapp create `
            --name $appName --resource-group $rg --environment $acaEnvName `
            --image $GotenbergImage --target-port 3000 --ingress external `
            --min-replicas 0 --max-replicas $maxReplicas --cpu 0.5 --memory 1.0Gi `
            --env-vars "GOTENBERG_API_BASICAUTH_USERNAME=$gotenbergUser" "GOTENBERG_API_BASICAUTH_PASSWORD=$gotenbergPass" `
            --output none
    }
    Write-Host "   Container App deployed." -ForegroundColor Green
}

$fqdn = az containerapp show --name $appName --resource-group $rg --query "properties.configuration.ingress.fqdn" --output tsv
$gotenbergUrl = "https://$fqdn"
Write-Host ""
Write-Host "Gotenberg URL: $gotenbergUrl" -ForegroundColor Green
Write-Host ""
Write-Host "Updating API App Service '$apiAppName' with Gotenberg settings..." -ForegroundColor Cyan
az webapp config appsettings set `
    --name $apiAppName --resource-group $rg `
    --settings "Gotenberg__BaseUrl=$gotenbergUrl" "Gotenberg__Username=$gotenbergUser" "Gotenberg__Password=$gotenbergPass" `
    --output none
Write-Host "   App Service settings updated." -ForegroundColor Green
Write-Host ""
Write-Host "======================================================" -ForegroundColor Cyan
Write-Host "  Setup complete!" -ForegroundColor Cyan
Write-Host "  Environment : $Environment"
Write-Host "  Container   : $appName"
Write-Host "  URL         : $gotenbergUrl"
Write-Host ""
Write-Host "  Restart the API to apply new settings:"
Write-Host "  az webapp restart --name $apiAppName --resource-group $rg"
Write-Host "======================================================" -ForegroundColor Cyan