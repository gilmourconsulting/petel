# ============================================
# Petel AWS test infrastructure (il-central-1)
# ============================================
# Shared EC2 + RDS PostgreSQL. Blazor/nginx public on Israeli CIDRs only.
# APIs bind to localhost. Ops via SSM (no SSH).
# Run once (idempotent). Then: .\Deploy-Aws-Test.ps1
# ============================================

param(
    [switch]$SkipBudget,
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'
$Region = 'il-central-1'
$AccountId = (aws sts get-caller-identity --query Account --output text)
$Here = Split-Path -Parent $MyInvocation.MyCommand.Path
$CidrFile = Join-Path $Here 'israeli-cidrs.txt'
$UserDataFile = Join-Path $Here 'user-data-test.sh'

$Name = @{
    Vpc           = 'petel-test-vpc'
    Igw           = 'petel-test-igw'
    SubnetA       = 'petel-test-subnet-a'
    SubnetB       = 'petel-test-subnet-b'
    Rt            = 'petel-test-rt'
    SgHttp        = 'petel-test-sg-http'
    SgHttps       = 'petel-test-sg-https'
    SgRds         = 'petel-test-sg-rds'
    Role          = 'petel-test-ec2-role'
    InstanceProf  = 'petel-test-ec2-profile'
    DbSubnet      = 'petel-test-db-subnets'
    Db            = 'petel-test-pg'
    Ec2           = 'petel-test-app'
    Eip           = 'petel-test-eip'
    Bucket        = "petel-aws-deploy-$AccountId"
}

function Write-Step([string]$Message) {
    Write-Host ""
    Write-Host $Message -ForegroundColor Yellow
}

function Get-TaggedId {
    param([string]$Service, [string]$Name)
    switch ($Service) {
        'vpc' {
            aws ec2 describe-vpcs --region $Region --filters "Name=tag:Name,Values=$Name" --query 'Vpcs[0].VpcId' --output text
        }
        'igw' {
            aws ec2 describe-internet-gateways --region $Region --filters "Name=tag:Name,Values=$Name" --query 'InternetGateways[0].InternetGatewayId' --output text
        }
        'subnet' {
            aws ec2 describe-subnets --region $Region --filters "Name=tag:Name,Values=$Name" --query 'Subnets[0].SubnetId' --output text
        }
        'rt' {
            aws ec2 describe-route-tables --region $Region --filters "Name=tag:Name,Values=$Name" --query 'RouteTables[0].RouteTableId' --output text
        }
        'sg' {
            aws ec2 describe-security-groups --region $Region --filters "Name=group-name,Values=$Name" --query 'SecurityGroups[0].GroupId' --output text
        }
        'instance' {
            aws ec2 describe-instances --region $Region --filters "Name=tag:Name,Values=$Name" "Name=instance-state-name,Values=pending,running,stopping,stopped" --query 'Reservations[0].Instances[0].InstanceId' --output text
        }
        'eip' {
            aws ec2 describe-addresses --region $Region --filters "Name=tag:Name,Values=$Name" --query 'Addresses[0].AllocationId' --output text
        }
        default { 'None' }
    }
}

function Test-AwsId([string]$Value) {
    return ($Value -and $Value -ne 'None' -and $Value -ne 'null')
}

function Set-NameTag([string]$ResourceId, [string]$NameValue) {
    aws ec2 create-tags --region $Region --resources $ResourceId --tags "Key=Name,Value=$NameValue" "Key=Environment,Value=test" "Key=Application,Value=Petel" | Out-Null
}

function Write-JsonNoBom([string]$Path, $Object) {
    $enc = New-Object System.Text.UTF8Encoding $false
    [System.IO.File]::WriteAllText($Path, ($Object | ConvertTo-Json -Depth 8), $enc)
}

function Get-AwsFileUri([string]$Path) {
    return 'file://' + ($Path -replace '\\', '/')
}

Write-Host "Petel AWS test infra  |  account $AccountId  |  $Region" -ForegroundColor Cyan
if ($DryRun) { Write-Host "DRY RUN" -ForegroundColor Yellow; exit 0 }

$cidrs = Get-Content $CidrFile | Where-Object { $_.Trim() -and -not $_.StartsWith('#') }
if ($cidrs.Count -lt 20) { throw "Israeli CIDR list looks too short: $($cidrs.Count)" }

# --- Spend guardrail (Budgets is us-east-1) ---
if (-not $SkipBudget) {
    Write-Step "Budget alarm \$200 / month"
    $existing = aws budgets describe-budgets --account-id $AccountId --region us-east-1 --query "Budgets[?BudgetName=='petel-monthly-200'].BudgetName" --output text 2>$null
    if (-not $existing) {
        $budgetPath = Join-Path $env:TEMP 'petel-budget.json'
        $notifyPath = Join-Path $env:TEMP 'petel-budget-notify.json'
        @{
            BudgetName  = 'petel-monthly-200'
            BudgetLimit = @{ Amount = '200'; Unit = 'USD' }
            TimeUnit    = 'MONTHLY'
            BudgetType  = 'COST'
        } | ConvertTo-Json -Compress | Set-Content -Encoding ascii $budgetPath
        ,@(
            @{
                Notification = @{
                    NotificationType      = 'ACTUAL'
                    ComparisonOperator    = 'GREATER_THAN'
                    Threshold             = 80
                    ThresholdType         = 'PERCENTAGE'
                    NotificationState     = 'ALARM'
                }
                Subscribers  = @(
                    @{ SubscriptionType = 'EMAIL'; Address = 'asher@petel.site' }
                )
            }
        ) | ConvertTo-Json -Depth 6 | Set-Content -Encoding ascii $notifyPath
        aws budgets create-budget --account-id $AccountId --region us-east-1 --budget (Get-AwsFileUri $budgetPath) --notifications-with-subscribers (Get-AwsFileUri $notifyPath) | Out-Null
        Write-Host "Confirm the SNS/budget email sent to asher@petel.site" -ForegroundColor Yellow
    } else {
        Write-Host "Budget already exists"
    }
}

# --- VPC ---
Write-Step "VPC"
$vpcId = Get-TaggedId vpc $Name.Vpc
if (-not (Test-AwsId $vpcId)) {
    $vpcId = aws ec2 create-vpc --region $Region --cidr-block 10.20.0.0/16 --query Vpc.VpcId --output text
    aws ec2 modify-vpc-attribute --region $Region --vpc-id $vpcId --enable-dns-hostnames Value=true
    aws ec2 modify-vpc-attribute --region $Region --vpc-id $vpcId --enable-dns-support Value=true
    Set-NameTag $vpcId $Name.Vpc
}
Write-Host "VPC $vpcId"

Write-Step "Internet gateway"
$igwId = Get-TaggedId igw $Name.Igw
if (-not (Test-AwsId $igwId)) {
    $igwId = aws ec2 create-internet-gateway --region $Region --query InternetGateway.InternetGatewayId --output text
    Set-NameTag $igwId $Name.Igw
    aws ec2 attach-internet-gateway --region $Region --internet-gateway-id $igwId --vpc-id $vpcId
}
Write-Host "IGW $igwId"

Write-Step "Subnets (RDS needs two AZs)"
$subnetA = Get-TaggedId subnet $Name.SubnetA
if (-not (Test-AwsId $subnetA)) {
    $subnetA = aws ec2 create-subnet --region $Region --vpc-id $vpcId --cidr-block 10.20.1.0/24 --availability-zone il-central-1a --query Subnet.SubnetId --output text
    aws ec2 modify-subnet-attribute --region $Region --subnet-id $subnetA --map-public-ip-on-launch
    Set-NameTag $subnetA $Name.SubnetA
}
$subnetB = Get-TaggedId subnet $Name.SubnetB
if (-not (Test-AwsId $subnetB)) {
    $subnetB = aws ec2 create-subnet --region $Region --vpc-id $vpcId --cidr-block 10.20.2.0/24 --availability-zone il-central-1b --query Subnet.SubnetId --output text
    aws ec2 modify-subnet-attribute --region $Region --subnet-id $subnetB --map-public-ip-on-launch
    Set-NameTag $subnetB $Name.SubnetB
}
Write-Host "Subnets $subnetA $subnetB"

Write-Step "Route table"
$rtId = Get-TaggedId rt $Name.Rt
if (-not (Test-AwsId $rtId)) {
    $rtId = aws ec2 create-route-table --region $Region --vpc-id $vpcId --query RouteTable.RouteTableId --output text
    Set-NameTag $rtId $Name.Rt
    aws ec2 create-route --region $Region --route-table-id $rtId --destination-cidr-block 0.0.0.0/0 --gateway-id $igwId | Out-Null
    aws ec2 associate-route-table --region $Region --route-table-id $rtId --subnet-id $subnetA | Out-Null
    aws ec2 associate-route-table --region $Region --route-table-id $rtId --subnet-id $subnetB | Out-Null
}
Write-Host "RT $rtId"

function New-CidrSg {
    param([string]$SgName, [int]$Port, [string]$Description)
    $sgId = Get-TaggedId sg $SgName
    if (-not (Test-AwsId $sgId)) {
        $sgId = aws ec2 create-security-group --region $Region --group-name $SgName --description $Description --vpc-id $vpcId --query GroupId --output text
        Set-NameTag $sgId $SgName
    }
    $ruleCount = [int](aws ec2 describe-security-groups --region $Region --group-ids $sgId --query 'length(SecurityGroups[0].IpPermissions[].IpRanges[])' --output text)
    if ($ruleCount -lt 10) {
        $ErrorActionPreference = 'Continue'
        foreach ($c in $cidrs) {
            aws ec2 authorize-security-group-ingress --region $Region --group-id $sgId --protocol tcp --port $Port --cidr $c.Trim() 2>$null | Out-Null
        }
        $ErrorActionPreference = 'Stop'
    }
    return $sgId
}

Write-Step "Security groups (Israeli CIDRs on 80/443; RDS from EC2 only)"
$sgHttp = New-CidrSg -SgName $Name.SgHttp -Port 80 -Description 'Petel test HTTP from Israel'
$sgHttps = New-CidrSg -SgName $Name.SgHttps -Port 443 -Description 'Petel test HTTPS from Israel'
$sgRds = Get-TaggedId sg $Name.SgRds
if (-not (Test-AwsId $sgRds)) {
    $sgRds = aws ec2 create-security-group --region $Region --group-name $Name.SgRds --description 'Petel test RDS from EC2' --vpc-id $vpcId --query GroupId --output text
    Set-NameTag $sgRds $Name.SgRds
}

Write-Step "IAM instance role (SSM + S3 deploy + SSM params)"
$roleExists = $null
try { $roleExists = aws iam get-role --role-name $Name.Role --query Role.RoleName --output text 2>$null } catch { $roleExists = $null }
if (-not $roleExists) {
    $trust = @{
        Version   = '2012-10-17'
        Statement = @(
            @{
                Effect    = 'Allow'
                Principal = @{ Service = 'ec2.amazonaws.com' }
                Action    = 'sts:AssumeRole'
            }
        )
    } | ConvertTo-Json -Depth 6
    $trustPath = Join-Path $env:TEMP 'petel-ec2-trust.json'
    $trust | Set-Content -Encoding ascii $trustPath
    aws iam create-role --role-name $Name.Role --assume-role-policy-document (Get-AwsFileUri $trustPath) | Out-Null
    aws iam attach-role-policy --role-name $Name.Role --policy-arn arn:aws:iam::aws:policy/AmazonSSMManagedInstanceCore
    $inline = @{
        Version   = '2012-10-17'
        Statement = @(
            @{
                Effect   = 'Allow'
                Action   = @('ssm:GetParameter', 'ssm:GetParameters', 'ssm:GetParametersByPath')
                Resource = "arn:aws:ssm:${Region}:${AccountId}:parameter/petel/test/*"
            }
            @{
                Effect   = 'Allow'
                Action   = @('s3:GetObject', 's3:ListBucket')
                Resource = @(
                    "arn:aws:s3:::$( $Name.Bucket )"
                    "arn:aws:s3:::$( $Name.Bucket )/*"
                )
            }
        )
    } | ConvertTo-Json -Depth 6
    $inlinePath = Join-Path $env:TEMP 'petel-ec2-inline.json'
    $inline | Set-Content -Encoding ascii $inlinePath
    aws iam put-role-policy --role-name $Name.Role --policy-name petel-test-ec2 --policy-document (Get-AwsFileUri $inlinePath)
}
$profExists = $null
try { $profExists = aws iam get-instance-profile --instance-profile-name $Name.InstanceProf --query InstanceProfile.InstanceProfileName --output text 2>$null } catch { $profExists = $null }
if (-not $profExists) {
    aws iam create-instance-profile --instance-profile-name $Name.InstanceProf | Out-Null
    aws iam add-role-to-instance-profile --instance-profile-name $Name.InstanceProf --role-name $Name.Role
    Start-Sleep -Seconds 8
}

Write-Step "S3 deploy bucket"
$ErrorActionPreference = 'Continue'
aws s3api head-bucket --bucket $Name.Bucket --region $Region 2>$null | Out-Null
$bucketMissing = ($LASTEXITCODE -ne 0)
$ErrorActionPreference = 'Stop'
if ($bucketMissing) {
    aws s3api create-bucket --bucket $Name.Bucket --region $Region --create-bucket-configuration LocationConstraint=$Region | Out-Null
    aws s3api put-public-access-block --bucket $Name.Bucket --public-access-block-configuration BlockPublicAcls=true,IgnorePublicAcls=true,BlockPublicPolicy=true,RestrictPublicBuckets=true
}

Write-Step "RDS subnet group + PostgreSQL 16 (db.t4g.micro, 32 GB)"
$subnetGroup = $null
try { $subnetGroup = aws rds describe-db-subnet-groups --region $Region --db-subnet-group-name $Name.DbSubnet --query DBSubnetGroups[0].DBSubnetGroupName --output text 2>$null } catch { $subnetGroup = $null }
if (-not $subnetGroup -or $subnetGroup -eq 'None') {
    aws rds create-db-subnet-group --region $Region --db-subnet-group-name $Name.DbSubnet --db-subnet-group-description 'Petel test' --subnet-ids $subnetA $subnetB | Out-Null
}

$dbStatus = $null
try { $dbStatus = aws rds describe-db-instances --region $Region --db-instance-identifier $Name.Db --query DBInstances[0].DBInstanceStatus --output text 2>$null } catch { $dbStatus = $null }
if (-not $dbStatus -or $dbStatus -eq 'None') {
    $dbPass = -join ((48..57 + 65..90 + 97..122) | Get-Random -Count 24 | ForEach-Object { [char]$_ })
    aws ssm put-parameter --region $Region --name /petel/test/db/password --type SecureString --value $dbPass --overwrite | Out-Null
    aws rds create-db-instance `
        --region $Region `
        --db-instance-identifier $Name.Db `
        --db-instance-class db.t4g.micro `
        --engine postgres `
        --engine-version 16.14 `
        --master-username peteladmin `
        --master-user-password $dbPass `
        --allocated-storage 32 `
        --storage-type gp3 `
        --db-name petelappdb `
        --vpc-security-group-ids $sgRds `
        --db-subnet-group-name $Name.DbSubnet `
        --no-publicly-accessible `
        --backup-retention-period 3 `
        --no-multi-az `
        --storage-encrypted `
        --tags Key=Name,Value=$($Name.Db) Key=Environment,Value=test Key=Application,Value=Petel | Out-Null
    Write-Host "RDS creating (5-10 min)..."
} else {
    Write-Host "RDS already $dbStatus"
    $dbPass = aws ssm get-parameter --region $Region --name /petel/test/db/password --with-decryption --query Parameter.Value --output text
}
if (-not $dbPass) {
    $dbPass = aws ssm get-parameter --region $Region --name /petel/test/db/password --with-decryption --query Parameter.Value --output text
}

Write-Step "Wait for RDS available"
aws rds wait db-instance-available --region $Region --db-instance-identifier $Name.Db
$dbHost = aws rds describe-db-instances --region $Region --db-instance-identifier $Name.Db --query 'DBInstances[0].Endpoint.Address' --output text
Write-Host "RDS $dbHost"

# RDS SG: allow 5432 from both web SGs (EC2 has both attached)
$rdsIngress = aws ec2 describe-security-groups --region $Region --group-ids $sgRds --query 'SecurityGroups[0].IpPermissions' --output json
if ($rdsIngress -eq '[]') {
    aws ec2 authorize-security-group-ingress --region $Region --group-id $sgRds --protocol tcp --port 5432 --source-group $sgHttp | Out-Null
    aws ec2 authorize-security-group-ingress --region $Region --group-id $sgRds --protocol tcp --port 5432 --source-group $sgHttps | Out-Null
}

$conn = "Host=$dbHost;Database=petelappdb;Username=peteladmin;Password=$dbPass"
aws ssm put-parameter --region $Region --name /petel/test/db/connection --type SecureString --value $conn --overwrite | Out-Null

Write-Step "EC2 t3.medium"
$instanceId = Get-TaggedId instance $Name.Ec2
if (-not (Test-AwsId $instanceId)) {
    $ami = aws ssm get-parameter --region $Region --name /aws/service/ami-amazon-linux-latest/al2023-ami-kernel-6.1-x86_64 --query Parameter.Value --output text
    $ebsPath = Join-Path $env:TEMP 'petel-ec2-ebs.json'
    $ebsJson = '[{"DeviceName":"/dev/xvda","Ebs":{"VolumeSize":30,"VolumeType":"gp3","DeleteOnTermination":true}}]'
    $enc = New-Object System.Text.UTF8Encoding $false
    [System.IO.File]::WriteAllText($ebsPath, $ebsJson, $enc)
    $instanceId = aws ec2 run-instances `
        --region $Region `
        --image-id $ami `
        --instance-type t3.medium `
        --subnet-id $subnetA `
        --security-group-ids $sgHttp $sgHttps `
        --iam-instance-profile "Name=$($Name.InstanceProf)" `
        --user-data (Get-AwsFileUri $UserDataFile) `
        --block-device-mappings (Get-AwsFileUri $ebsPath) `
        --tag-specifications "ResourceType=instance,Tags=[{Key=Name,Value=$($Name.Ec2)},{Key=Environment,Value=test},{Key=Application,Value=Petel}]" `
        --query 'Instances[0].InstanceId' --output text
    if (-not (Test-AwsId $instanceId)) { throw 'EC2 launch failed' }
}
Write-Host "EC2 $instanceId"
aws ec2 wait instance-running --region $Region --instance-ids $instanceId

Write-Step "Elastic IP"
$allocId = Get-TaggedId eip $Name.Eip
if (-not (Test-AwsId $allocId)) {
    $allocId = aws ec2 allocate-address --region $Region --domain vpc --query AllocationId --output text
    Set-NameTag $allocId $Name.Eip
}
$assoc = aws ec2 describe-addresses --region $Region --allocation-ids $allocId --query 'Addresses[0].InstanceId' --output text
if ($assoc -ne $instanceId) {
    aws ec2 associate-address --region $Region --instance-id $instanceId --allocation-id $allocId | Out-Null
}
$eip = aws ec2 describe-addresses --region $Region --allocation-ids $allocId --query 'Addresses[0].PublicIp' --output text
aws ssm put-parameter --region $Region --name /petel/test/ec2/public-ip --type String --value $eip --overwrite | Out-Null
aws ssm put-parameter --region $Region --name /petel/test/ec2/instance-id --type String --value $instanceId --overwrite | Out-Null

Write-Host ""
Write-Host "Test infrastructure ready" -ForegroundColor Green
Write-Host "  EC2:  $instanceId"
Write-Host "  EIP:  $eip   (HTTP from Israeli IPs only)"
Write-Host "  RDS:  $dbHost"
Write-Host "  SSM:  /petel/test/db/connection"
Write-Host ""
Write-Host "Next: .\infra\aws\Deploy-Aws-Test.ps1"
Write-Host "SSM shell: aws ssm start-session --target $instanceId --region $Region"
