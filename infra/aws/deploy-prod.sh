#!/bin/bash
set -euxo pipefail
REGION=il-central-1
BUCKET="$1"

aws s3 cp "s3://${BUCKET}/prod/apps.tgz" /tmp/apps.tgz --region "$REGION"
mkdir -p /tmp/petel-apps
tar -xzf /tmp/apps.tgz -C /tmp/petel-apps
for app in ath-api ath-blazor; do
  if [ -d "/tmp/petel-apps/${app}" ]; then
    rm -rf "/opt/petel/${app}"
    mkdir -p "/opt/petel/${app}"
    cp -a "/tmp/petel-apps/${app}/." "/opt/petel/${app}/"
  fi
done
chown -R ec2-user:ec2-user /opt/petel

aws s3 cp "s3://${BUCKET}/prod/nginx-prod.conf" /etc/nginx/conf.d/petel.conf --region "$REGION"
rm -f /etc/nginx/conf.d/default.conf || true
python3 - <<'PY'
from pathlib import Path
p = Path("/etc/nginx/nginx.conf")
text = p.read_text()
start = text.find("    server {\n        listen       80;")
end = text.find("# Settings for a TLS enabled server.")
if start != -1 and end != -1 and start < end:
    p.write_text(text[:start] + text[end:])
PY
mkdir -p /etc/systemd/system
aws s3 sync "s3://${BUCKET}/prod/systemd" /etc/systemd/system --region "$REGION" --exclude '*' --include '*.service'

mkdir -p /etc/petel
aws ssm get-parameter --region "$REGION" --name /petel/prod/env/ath-api --with-decryption --query Parameter.Value --output text > /etc/petel/ath-api.env
aws ssm get-parameter --region "$REGION" --name /petel/prod/env/ath-blazor --with-decryption --query Parameter.Value --output text > /etc/petel/ath-blazor.env
chmod 600 /etc/petel/*.env
sed -i 's/\r$//' /etc/petel/*.env
chown root:ec2-user /etc/petel/*.env

systemctl daemon-reload
systemctl enable ath-api ath-blazor
systemctl restart ath-api ath-blazor nginx
nginx -t
systemctl is-active ath-api ath-blazor nginx
