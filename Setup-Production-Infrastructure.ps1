# ============================================
# Petel Application - Production Infrastructure Setup
# ============================================
# This script creates all Azure resources needed for production
# Run this ONCE before deploying the application
# ============================================

param(
    [switch]$SkipDatabase,
    [switch]$SkipKeyVault,
    [switch]$SkipFrontDoor,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

# Production Configuration
$config = @{
    ResourceGroup        = 'petel-prod-rg'
    Location            = 'israelcentral'
    AppServicePlan      = 'petel-prod-plan'
    BlazorAppName       = 'petel-prod-blazor'
    ApiAppName          = 'petel-prod-api'
    KeyVaultName        = "petel-kv-prod-$(Get-Random -Minimum 1000 -Maximum 9999)"
    DbServerName        = "petel-prod-db-$(Get-Random -Minimum 1000 -Maximum 9999)"
    DbName              = 'petelappdb'
    DbAdminUser         = 'peteldbadmin'
    FrontDoorName       = 'petel-prod-frontdoor'
    WafPolicyName       = 'petelWafProd'
    PlanSku             = 'P1V3'  # Production tier
    DbSku               = 'Standard_D2ds_v4'  # 2 vCores, 8GB RAM
    DbStorageSize       = 128  # GB
    Tags                = @{
        Environment = 'Production'
        Application = 'PetelEMS'
        ManagedBy   = 'Infrastructure'
        CostCenter  = 'Education'
    }
}

Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "Petel Production Infrastructure Setup" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

if ($DryRun) {
    Write-Host "DRY RUN MODE - No resources will be created" -ForegroundColor Yellow
    Write-Host ""
}

# Display planned resources
Write-Host "Planned Resources:" -ForegroundColor Cyan
Write-Host "  Resource Group:      $($config.ResourceGroup)" -ForegroundColor White
Write-Host "  Location:            $($config.Location)" -ForegroundColor White
Write-Host "  App Service Plan:    $($config.AppServicePlan) ($($config.PlanSku))" -ForegroundColor White
Write-Host "  Blazor App:          $($config.BlazorAppName).azurewebsites.net" -ForegroundColor White
Write-Host "  API App:             $($config.ApiAppName).azurewebsites.net" -ForegroundColor White
if (-not $SkipKeyVault) {
    Write-Host "  Key Vault:           $($config.KeyVaultName).vault.azure.net" -ForegroundColor White
}
if (-not $SkipDatabase) {
    Write-Host "  PostgreSQL Server:   $($config.DbServerName).postgres.database.azure.com" -ForegroundColor White
    Write-Host "  Database:            $($config.DbName)" -ForegroundColor White
}
if (-not $SkipFrontDoor) {
    Write-Host "  Front Door:          $($config.FrontDoorName).azurefd.net" -ForegroundColor White
}
Write-Host ""

if ($DryRun) {
    Write-Host "Dry run complete. Run without -DryRun to create resources." -ForegroundColor Green
    exit 0
}

# Confirm before proceeding
$confirm = Read-Host "Create these resources? (yes/no)"
if ($confirm -ne 'yes') {
    Write-Host "Cancelled by user" -ForegroundColor Yellow
    exit 0
}

Write-Host ""
Write-Host "Starting infrastructure creation..." -ForegroundColor Green
Write-Host ""

# Helper Functions
function Write-Step {
    param([string]$Message)
    Write-Host ""
    Write-Host $Message -ForegroundColor Yellow
    Write-Host ("=" * $Message.Length) -ForegroundColor Yellow
}

function Write-Success {
    param([string]$Message)
    Write-Host "SUCCESS: $Message" -ForegroundColor Green
}

function Write-ErrorMsg {
    param([string]$Message)
    Write-Host "ERROR: $Message" -ForegroundColor Red
}

# Test Azure CLI
try {
    az account show | Out-Null
    Write-Success "Azure CLI authenticated"
}
catch {
    Write-ErrorMsg "Azure CLI not authenticated. Run: az login"
    exit 1
}

# Step 1: Create Resource Group
Write-Step "Step 1: Creating Resource Group"

$rgExists = az group exists --name $config.ResourceGroup
if ($rgExists -eq 'true') {
    Write-Host "Resource group already exists" -ForegroundColor Yellow
} else {
    $tagString = ($config.Tags.GetEnumerator() | ForEach-Object { "$($_.Key)=$($_.Value)" }) -join ' '
    az group create `
        --name $config.ResourceGroup `
        --location $config.Location `
        --tags $tagString | Out-Null
    Write-Success "Resource group created: $($config.ResourceGroup)"
}

# Step 2: Create App Service Plan
Write-Step "Step 2: Creating App Service Plan"

$planExists = az appservice plan show `
    --name $config.AppServicePlan `
    --resource-group $config.ResourceGroup `
    --query "id" -o tsv 2>$null

if ($planExists) {
    Write-Host "App Service Plan already exists" -ForegroundColor Yellow
} else {
    az appservice plan create `
        --name $config.AppServicePlan `
        --resource-group $config.ResourceGroup `
        --location $config.Location `
        --sku $config.PlanSku `
        --is-linux `
        --tags $($config.Tags.GetEnumerator() | ForEach-Object { "$($_.Key)=$($_.Value)" }) | Out-Null
    Write-Success "App Service Plan created: $($config.AppServicePlan)"
}

# Step 3: Create API App Service
Write-Step "Step 3: Creating API App Service"

$apiExists = az webapp show `
    --name $config.ApiAppName `
    --resource-group $config.ResourceGroup `
    --query "id" -o tsv 2>$null

if ($apiExists) {
    Write-Host "API App Service already exists" -ForegroundColor Yellow
} else {
    az webapp create `
        --name $config.ApiAppName `
        --resource-group $config.ResourceGroup `
        --plan $config.AppServicePlan `
        --runtime "DOTNETCORE:9.0" `
        --tags $($config.Tags.GetEnumerator() | ForEach-Object { "$($_.Key)=$($_.Value)" }) | Out-Null
    
    # Configure API App Service
    az webapp config set `
        --name $config.ApiAppName `
        --resource-group $config.ResourceGroup `
        --always-on true `
        --http20-enabled true `
        --min-tls-version 1.2 | Out-Null
    
    # Set environment variables
    az webapp config appsettings set `
        --name $config.ApiAppName `
        --resource-group $config.ResourceGroup `
        --settings `
            ASPNETCORE_ENVIRONMENT="Production" `
            WEBSITE_RUN_FROM_PACKAGE="1" | Out-Null
    
    Write-Success "API App Service created: $($config.ApiAppName).azurewebsites.net"
}

# Step 4: Create Blazor App Service
Write-Step "Step 4: Creating Blazor App Service"

$blazorExists = az webapp show `
    --name $config.BlazorAppName `
    --resource-group $config.ResourceGroup `
    --query "id" -o tsv 2>$null

if ($blazorExists) {
    Write-Host "Blazor App Service already exists" -ForegroundColor Yellow
} else {
    az webapp create `
        --name $config.BlazorAppName `
        --resource-group $config.ResourceGroup `
        --plan $config.AppServicePlan `
        --runtime "DOTNETCORE:8.0" `
        --tags $($config.Tags.GetEnumerator() | ForEach-Object { "$($_.Key)=$($_.Value)" }) | Out-Null
    
    # Configure Blazor App Service
    az webapp config set `
        --name $config.BlazorAppName `
        --resource-group $config.ResourceGroup `
        --always-on true `
        --http20-enabled true `
        --min-tls-version 1.2 `
        --websockets-enabled true | Out-Null
    
    # Set environment variables
    az webapp config appsettings set `
        --name $config.BlazorAppName `
        --resource-group $config.ResourceGroup `
        --settings `
            ASPNETCORE_ENVIRONMENT="Production" `
            WEBSITE_RUN_FROM_PACKAGE="1" | Out-Null
    
    Write-Success "Blazor App Service created: $($config.BlazorAppName).azurewebsites.net"
}

# Step 5: Create PostgreSQL Database (if not skipped)
if (-not $SkipDatabase) {
    Write-Step "Step 5: Creating PostgreSQL Database"
    
    $dbExists = az postgres flexible-server show `
        --name $config.DbServerName `
        --resource-group $config.ResourceGroup `
        --query "id" -o tsv 2>$null
    
    if ($dbExists) {
        Write-Host "PostgreSQL server already exists" -ForegroundColor Yellow
    } else {
        # Generate strong admin password
        $dbPassword = -join ((48..57) + (65..90) + (97..122) | Get-Random -Count 24 | ForEach-Object {[char]$_})
        $dbPassword += "!@#"  # Ensure special characters
        
        Write-Host "Creating PostgreSQL Flexible Server (this may take 5-10 minutes)..." -ForegroundColor Gray
        
        az postgres flexible-server create `
            --name $config.DbServerName `
            --resource-group $config.ResourceGroup `
            --location $config.Location `
            --admin-user $config.DbAdminUser `
            --admin-password $dbPassword `
            --sku-name $config.DbSku `
            --storage-size $config.DbStorageSize `
            --tier GeneralPurpose `
            --version 14 `
            --high-availability Disabled `
            --public-access All `
            --tags $($config.Tags.GetEnumerator() | ForEach-Object { "$($_.Key)=$($_.Value)" }) | Out-Null
        
        # Create database
        az postgres flexible-server db create `
            --resource-group $config.ResourceGroup `
            --server-name $config.DbServerName `
            --database-name $config.DbName | Out-Null
        
        Write-Success "PostgreSQL server created: $($config.DbServerName).postgres.database.azure.com"
        Write-Host ""
        Write-Host "CRITICAL: Save these database credentials securely!" -ForegroundColor Red
        Write-Host "Server:   $($config.DbServerName).postgres.database.azure.com" -ForegroundColor Yellow
        Write-Host "Database: $($config.DbName)" -ForegroundColor Yellow
        Write-Host "Username: $($config.DbAdminUser)" -ForegroundColor Yellow
        Write-Host "Password: $dbPassword" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "Connection String:" -ForegroundColor Yellow
        $connString = "Host=$($config.DbServerName).postgres.database.azure.com;Database=$($config.DbName);Username=$($config.DbAdminUser);Password=$dbPassword;SslMode=Require"
        Write-Host $connString -ForegroundColor Cyan
        Write-Host ""
        
        # Save to file for reference
        $credsFile = "production-db-credentials-$(Get-Date -Format 'yyyyMMdd-HHmmss').txt"
        @"
Production Database Credentials
Created: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')
===========================================

Server:   $($config.DbServerName).postgres.database.azure.com
Database: $($config.DbName)
Username: $($config.DbAdminUser)
Password: $dbPassword

Connection String:
$connString

IMPORTANT: Store these credentials in Azure Key Vault immediately!
Then delete this file securely.
"@ | Out-File -FilePath $credsFile -Encoding UTF8
        
        Write-Host "Credentials saved to: $credsFile" -ForegroundColor Yellow
        Write-Host "Add to Key Vault then DELETE THIS FILE!" -ForegroundColor Red
    }
} else {
    Write-Host "Skipping database creation (use existing or configure manually)" -ForegroundColor Yellow
}

# Step 6: Create Key Vault (if not skipped)
if (-not $SkipKeyVault) {
    Write-Step "Step 6: Creating Key Vault"
    
    $kvExists = az keyvault show `
        --name $config.KeyVaultName `
        --resource-group $config.ResourceGroup `
        --query "id" -o tsv 2>$null
    
    if ($kvExists) {
        Write-Host "Key Vault already exists" -ForegroundColor Yellow
    } else {
        az keyvault create `
            --name $config.KeyVaultName `
            --resource-group $config.ResourceGroup `
            --location $config.Location `
            --sku standard `
            --enable-rbac-authorization false `
            --enabled-for-deployment true `
            --enabled-for-template-deployment true `
            --tags $($config.Tags.GetEnumerator() | ForEach-Object { "$($_.Key)=$($_.Value)" }) | Out-Null
        
        Write-Success "Key Vault created: $($config.KeyVaultName).vault.azure.net"
        
        # Grant access to App Services
        Write-Host "Granting Key Vault access to API App Service..." -ForegroundColor Gray
        az webapp identity assign `
            --name $config.ApiAppName `
            --resource-group $config.ResourceGroup | Out-Null
        
        $apiPrincipalId = az webapp identity show `
            --name $config.ApiAppName `
            --resource-group $config.ResourceGroup `
            --query "principalId" -o tsv
        
        az keyvault set-policy `
            --name $config.KeyVaultName `
            --object-id $apiPrincipalId `
            --secret-permissions get list | Out-Null
        
        Write-Host "Granting Key Vault access to Blazor App Service..." -ForegroundColor Gray
        az webapp identity assign `
            --name $config.BlazorAppName `
            --resource-group $config.ResourceGroup | Out-Null
        
        $blazorPrincipalId = az webapp identity show `
            --name $config.BlazorAppName `
            --resource-group $config.ResourceGroup `
            --query "principalId" -o tsv
        
        az keyvault set-policy `
            --name $config.KeyVaultName `
            --object-id $blazorPrincipalId `
            --secret-permissions get list | Out-Null
        
        Write-Success "Key Vault access configured for both App Services"
    }
} else {
    Write-Host "Skipping Key Vault creation (use existing or configure manually)" -ForegroundColor Yellow
}

# Step 7: Configure CORS for API
Write-Step "Step 7: Configuring CORS"

az webapp cors add `
    --name $config.ApiAppName `
    --resource-group $config.ResourceGroup `
    --allowed-origins `
        "https://$($config.BlazorAppName).azurewebsites.net" `
        "https://$($config.FrontDoorName).azurefd.net" | Out-Null

Write-Success "CORS configured for API"

# Summary
Write-Host ""
Write-Host "============================================" -ForegroundColor Green
Write-Host "Infrastructure Setup Complete!" -ForegroundColor Green
Write-Host "============================================" -ForegroundColor Green
Write-Host ""

Write-Host "Created Resources:" -ForegroundColor Cyan
Write-Host "  Resource Group:      $($config.ResourceGroup)" -ForegroundColor White
Write-Host "  App Service Plan:    $($config.AppServicePlan)" -ForegroundColor White
Write-Host "  API App:             https://$($config.ApiAppName).azurewebsites.net" -ForegroundColor White
Write-Host "  Blazor App:          https://$($config.BlazorAppName).azurewebsites.net" -ForegroundColor White

if (-not $SkipKeyVault) {
    Write-Host "  Key Vault:           https://$($config.KeyVaultName).vault.azure.net" -ForegroundColor White
}

if (-not $SkipDatabase) {
    Write-Host "  PostgreSQL Server:   $($config.DbServerName).postgres.database.azure.com" -ForegroundColor White
    Write-Host "  Database:            $($config.DbName)" -ForegroundColor White
}

Write-Host ""
Write-Host "Next Steps:" -ForegroundColor Yellow
Write-Host "  1. Add database credentials to Key Vault (if created)" -ForegroundColor White
Write-Host "  2. Generate and store JWT secret key in Key Vault" -ForegroundColor White
Write-Host "  3. Generate and store encryption key in Key Vault" -ForegroundColor White
Write-Host "  4. Update API App Service connection strings to use Key Vault references" -ForegroundColor White
Write-Host "  5. Run Deploy-ToAzure.ps1 -Environment production to deploy application" -ForegroundColor White
Write-Host "  6. Configure Front Door with WAF (run Setup-Production-FrontDoor.ps1)" -ForegroundColor White
Write-Host "  7. Configure IP restrictions for Israeli access only" -ForegroundColor White
Write-Host "  8. Set up monitoring and alerts" -ForegroundColor White
Write-Host ""
Write-Host "Documentation:" -ForegroundColor Yellow
Write-Host "  - See PRODUCTION_DEPLOYMENT_GUIDE.md for detailed steps" -ForegroundColor White
Write-Host "  - See PRODUCTION_CHECKLIST.md for validation" -ForegroundColor White
Write-Host ""
