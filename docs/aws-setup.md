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

## Prod shadow (ATH)

The `petel-prod-*` VPC, EC2, and RDS already exist and also host the Meitar shadow. ATH is added beside Meitar. It is not the client-facing site (`ath.petel.site` stays on Azure).

| Piece | Name |
|---|---|
| VPC | `petel-prod-vpc` `10.30.0.0/16` |
| EC2 | `petel-prod-app` `t3.medium` (Meitar on port 80, ATH on port 8080) |
| RDS | `petel-prod-pg` PostgreSQL 16, database `petelappdb` |
| ATH ingress | `petel-prod-sg-ath` — Israeli CIDRs on port 8080 |
| Secrets | SSM `/petel/prod/env/ath-api` and `/petel/prod/env/ath-blazor` |
| URL | `http://<prod-eip>:8080/` from an Israeli IP |

`ASPNETCORE_ENVIRONMENT` is `Production`. Blazor calls the API on `http://127.0.0.1:5082/api`. JWT and the data-encryption key are copied from Azure Key Vault `petel-kv-prod-6581` so restored rows decrypt. Hangfire tables are created empty on boot so Azure background jobs are not replayed.

Azure flexible-server backups are not files. The last automatic full backup of `petel-prod-db-4407` is the recovery base; the shadow database is a `pg_dump` / `pg_restore` of `petel_schema` from that server onto `petel-prod-pg`. Meitar schemas on the same RDS instance are left in place.

```powershell
.\infra\aws\Deploy-Aws-Prod-Ath.ps1
```
