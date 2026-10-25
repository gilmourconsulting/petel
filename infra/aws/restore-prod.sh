#!/bin/bash
set -euo pipefail
REGION=il-central-1
BUCKET="$1"
RDS_HOST="$2"

systemctl stop ath-api ath-blazor || true

aws s3 cp "s3://${BUCKET}/prod/petelappdb.dump" /var/tmp/petelappdb.dump --region "$REGION"
chmod 600 /var/tmp/petelappdb.dump

export PGHOST="$RDS_HOST"
export PGPORT=5432
export PGUSER=peteladmin
export PGSSLMODE=require
export PGPASSWORD
PGPASSWORD="$(aws ssm get-parameter --region "$REGION" --name /petel/prod/db/password --with-decryption --query Parameter.Value --output text)"

# Source counts if the dump credential is still present
if aws ssm get-parameter --region "$REGION" --name /petel/prod/azure-dump-source --with-decryption --query Parameter.Value --output text > /tmp/azure-src.json 2>/dev/null; then
  chmod 600 /tmp/azure-src.json
  python3 - <<'PY'
import json, os, subprocess
src = json.load(open("/tmp/azure-src.json"))
env = os.environ.copy()
env["PGHOST"] = src["host"]
env["PGPORT"] = str(src.get("port") or "5432")
env["PGDATABASE"] = src["database"]
env["PGUSER"] = src["user"]
env["PGPASSWORD"] = src["password"]
env["PGSSLMODE"] = "require"
sql = "SELECT n.nspname, count(*) FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE c.relkind='r' AND n.nspname IN ('petel_schema','meitar_raw','meitar_data') GROUP BY 1 ORDER BY 1;"
print("AZURE_COUNTS")
subprocess.check_call(["psql", "-At", "-c", sql], env=env)
PY
  rm -f /tmp/azure-src.json
fi

psql -d postgres -v ON_ERROR_STOP=1 -c "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = 'petelappdb' AND pid <> pg_backend_pid();"
psql -d postgres -v ON_ERROR_STOP=1 -c "DROP DATABASE IF EXISTS petelappdb;"
psql -d postgres -v ON_ERROR_STOP=1 -c "CREATE DATABASE petelappdb;"

set +e
pg_restore -d petelappdb --no-owner --no-acl /var/tmp/petelappdb.dump > /tmp/pg_restore.err 2>&1
restore_status=$?
set -e
echo "RESTORE_EXIT=$restore_status"
grep -E 'error:' /tmp/pg_restore.err | sed 's/.*error: //' | sort | uniq -c | sort -nr | head -30 || true

echo "RDS_SCHEMAS"
psql -d petelappdb -At -c "SELECT nspname FROM pg_namespace WHERE nspname NOT LIKE 'pg_%' AND nspname <> 'information_schema' ORDER BY 1;"
echo "RDS_COUNTS"
psql -d petelappdb -At -c "SELECT n.nspname, count(*) FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE c.relkind='r' AND n.nspname IN ('petel_schema','meitar_raw','meitar_data') GROUP BY 1 ORDER BY 1;"

rm -f /var/tmp/petelappdb.dump
echo RESTORE_DONE
