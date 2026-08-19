#!/bin/bash
set -euxo pipefail
REGION=il-central-1
BUCKET="$1"

aws s3 cp "s3://${BUCKET}/test/apps.tgz" /tmp/apps.tgz --region "$REGION"
mkdir -p /tmp/petel-apps
tar -xzf /tmp/apps.tgz -C /tmp/petel-apps
for app in ath-api ath-blazor assist-api assist-blazor; do
  if [ -d "/tmp/petel-apps/${app}" ]; then
    rm -rf "/opt/petel/${app}"
    mkdir -p "/opt/petel/${app}"
    cp -a "/tmp/petel-apps/${app}/." "/opt/petel/${app}/"
  fi
done
chown -R ec2-user:ec2-user /opt/petel

aws s3 cp "s3://${BUCKET}/test/nginx-test.conf" /etc/nginx/conf.d/petel.conf --region "$REGION"
rm -f /etc/nginx/conf.d/default.conf || true
mkdir -p /etc/systemd/system
aws s3 sync "s3://${BUCKET}/test/systemd" /etc/systemd/system --region "$REGION" --exclude '*' --include '*.service'

mkdir -p /etc/petel
aws ssm get-parameter --region "$REGION" --name /petel/test/env/ath-api --with-decryption --query Parameter.Value --output text > /etc/petel/ath-api.env
aws ssm get-parameter --region "$REGION" --name /petel/test/env/ath-blazor --with-decryption --query Parameter.Value --output text > /etc/petel/ath-blazor.env
aws ssm get-parameter --region "$REGION" --name /petel/test/env/assist-api --with-decryption --query Parameter.Value --output text > /etc/petel/assist-api.env
aws ssm get-parameter --region "$REGION" --name /petel/test/env/assist-blazor --with-decryption --query Parameter.Value --output text > /etc/petel/assist-blazor.env
chmod 600 /etc/petel/*.env
sed -i 's/\r$//' /etc/petel/*.env
chown root:ec2-user /etc/petel/*.env

systemctl daemon-reload
systemctl enable ath-api ath-blazor assist-api assist-blazor
systemctl restart ath-api ath-blazor assist-api assist-blazor nginx
nginx -t
systemctl is-active ath-api ath-blazor assist-api assist-blazor nginx
