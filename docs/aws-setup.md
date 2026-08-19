# Petel AWS Setup (test first)

> Region: `il-central-1`. Compute: one shared EC2 per environment (not Fargate). Israel-only inbound CIDRs. APIs bind to localhost; nginx is the public entry.

## Test (current)

| Piece | Name |
|---|---|
| VPC | `petel-test-vpc` `10.20.0.0/16` |
| EC2 | `petel-test-app` `t3.medium` Amazon Linux 2023 |
| RDS | `petel-test-pg` PostgreSQL 16 `db.t4g.micro` 32 GB, not public |
| Ingress | Israeli CIDRs on ports 80 and 443 ([israeli-cidrs.txt](../infra/aws/israeli-cidrs.txt)) |
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
