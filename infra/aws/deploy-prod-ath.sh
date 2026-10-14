#!/bin/bash
set -euxo pipefail
REGION=il-central-1
BUCKET="$1"

aws s3 cp "s3://${BUCKET}/prod/ath/apps.tgz" /tmp/ath-apps.tgz --region "$REGION"
mkdir -p /tmp/petel-ath-apps
rm -rf /tmp/petel-ath-apps/*
tar -xzf /tmp/ath-apps.tgz -C /tmp/petel-ath-apps
for app in ath-api ath-blazor; do
  if [ -d "/tmp/petel-ath-apps/${app}" ]; then
    rm -rf "/opt/petel/${app}"
    mkdir -p "/opt/petel/${app}"
    cp -a "/tmp/petel-ath-apps/${app}/." "/opt/petel/${app}/"
  fi
done
chown -R ec2-user:ec2-user /opt/petel

aws s3 cp "s3://${BUCKET}/prod/ath/nginx-prod.conf" /etc/nginx/conf.d/petel.conf --region "$REGION"
sed -i 's/\r$//' /etc/nginx/conf.d/petel.conf
mkdir -p /etc/systemd/system
aws s3 cp "s3://${BUCKET}/prod/ath/systemd/ath-api-prod.service" /etc/systemd/system/ath-api.service --region "$REGION"
aws s3 cp "s3://${BUCKET}/prod/ath/systemd/ath-blazor-prod.service" /etc/systemd/system/ath-blazor.service --region "$REGION"
sed -i 's/\r$//' /etc/systemd/system/ath-api.service /etc/systemd/system/ath-blazor.service

mkdir -p /etc/petel
aws ssm get-parameter --region "$REGION" --name /petel/prod/env/ath-api --with-decryption --query Parameter.Value --output text > /etc/petel/ath-api.env
aws ssm get-parameter --region "$REGION" --name /petel/prod/env/ath-blazor --with-decryption --query Parameter.Value --output text > /etc/petel/ath-blazor.env
chmod 600 /etc/petel/ath-api.env /etc/petel/ath-blazor.env
sed -i 's/\r$//' /etc/petel/ath-api.env /etc/petel/ath-blazor.env
chown root:ec2-user /etc/petel/ath-api.env /etc/petel/ath-blazor.env

systemctl daemon-reload
systemctl enable ath-api ath-blazor
systemctl restart ath-api ath-blazor
nginx -t
systemctl reload nginx
systemctl is-active ath-api ath-blazor nginx meitar-web
