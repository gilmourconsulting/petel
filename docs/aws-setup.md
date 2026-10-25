# Petel AWS Setup (test first)

> Region: `il-central-1`. Compute: one shared EC2 per environment (not Fargate). Israel-only inbound CIDRs. APIs bind to localhost; nginx is the public entry.

## Test (current)

| Piece | Name |
|---|---|
| VPC | `petel-test-vpc` `10.20.0.0/16` |
| EC2 | `petel-test-app` `t3.medium` Amazon Linux 2023 |
| RDS | `petel-test-pg` PostgreSQL 16 `db.t4g.micro` 32 GB, not public |
| Ingress | Israeli CIDRs on port 443 ([israeli-cidrs.txt](../infra/aws/israeli-cidrs.txt)). Port 80 is open so Let's Encrypt renewal can complete |
| Ops | SSM Session Manager (no SSH) |
| Secrets | SSM `/petel/test/*` |
| Deploy bucket | `s3://petel-aws-deploy-<account>/test/` |
| Budget | `petel-monthly-200` (confirm email) |

```powershell
.\infra\aws\Setup-Aws-Test-Infrastructure.ps1
.\infra\aws\Deploy-Aws-Test.ps1
aws ssm start-session --target <instance-id> --region il-central-1
```

Test `ASPNETCORE_ENVIRONMENT` is `Staging` (same as Azure test). Blazor `ApiSettings:BaseUrl` is `http://127.0.0.1:<api-port>/api`.

Copy JWT / AES / SMTP from Azure App Settings into `/etc/petel/*.env` (via SSM parameters) before login will fully work. Restore Azure test PostgreSQL with `pg_dump` / `pg_restore` onto `petel-test-pg`.

HTTPS for `ath-test.petel.site` terminates on nginx with a Let's Encrypt certificate at `/etc/letsencrypt/live/ath-test.petel.site/`. That hostname redirects to HTTPS. Namecheap host `ath-test` must point at the test instance:

```
ath-test  CNAME  ec2-51-16-213-231.il-central-1.compute.amazonaws.com
```

## Production (ATH)

Parallel ATH production stack. `ath.petel.site` is served from this stack. Assistants is not installed on this box.

| Piece | Name |
|---|---|
| VPC | `petel-prod-vpc` `10.30.0.0/16` |
| EC2 | `petel-prod-app` `t3.large` Amazon Linux 2023 |
| RDS | `petel-prod-pg` PostgreSQL 16 `db.t4g.large` 128 GB, private, 7-day backups |
| Ingress | Israeli CIDRs on ports 80 and 443 |
| Ops | SSM Session Manager (no SSH) |
| Secrets | SSM `/petel/prod/*` |
| Deploy bucket | `s3://petel-aws-deploy-<account>/prod/` |

```powershell
.\infra\aws\Setup-Aws-Prod-Infrastructure.ps1
.\infra\aws\Deploy-Aws-Prod.ps1
```

`ASPNETCORE_ENVIRONMENT` is `Production`. Blazor `ApiSettings:BaseUrl` is `http://127.0.0.1:5082/api`. Deploy copies JWT, encryption, email, and Gotenberg settings from Azure `petel-prod-api`. `HangfireConnection` is set empty so the Production placeholder does not start a Hangfire server on this copy.

HTTPS for `ath.petel.site` terminates on nginx with a Let's Encrypt certificate at `/etc/letsencrypt/live/ath.petel.site/`. Port 443 stays limited to Israeli CIDRs. Port 80 is open so certificate renewal can complete, and that hostname redirects to HTTPS. Namecheap Advanced DNS for `petel.site` must be an A record, not a CNAME to Azure:

```
ath  A  16.164.176.34
```

`meitar.petel.site` uses the same address. Port 80's default server stays ATH. nginx redirects that host to HTTPS and proxies it to Meitar on `127.0.0.1:5105`. The certificate is `/etc/letsencrypt/live/meitar.petel.site/`. Add this record if the name does not already point at that IP:

```
meitar  A  16.164.176.34
```
