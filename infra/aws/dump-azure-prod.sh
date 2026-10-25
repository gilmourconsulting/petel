#!/bin/bash
set -euo pipefail
REGION=il-central-1
BUCKET="$1"

if command -v growpart >/dev/null; then
  growpart /dev/nvme0n1 1 || true
fi
if findmnt -n -o FSTYPE / | grep -qx xfs; then
  xfs_growfs / || true
else
  resize2fs /dev/nvme0n1p1 || true
fi
df -h /

aws ssm get-parameter --region "$REGION" --name /petel/prod/azure-dump-source --with-decryption --query Parameter.Value --output text > /tmp/azure-src.json
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
print("dumping", src["host"], src["database"], "user", src["user"], flush=True)
subprocess.check_call(
    ["pg_dump", "-Fc", "--no-owner", "--no-acl", "-f", "/var/tmp/petelappdb.dump"],
    env=env,
)
PY
ls -lh /var/tmp/petelappdb.dump
aws s3 cp /var/tmp/petelappdb.dump "s3://${BUCKET}/prod/petelappdb.dump" --region "$REGION"
rm -f /var/tmp/petelappdb.dump /tmp/azure-src.json
echo DUMP_OK
