#!/bin/bash
set -euxo pipefail
dnf update -y
dnf install -y nginx tar gzip libicu

# Swap so four .NET processes fit on t3.medium
if [ ! -f /swapfile ]; then
  dd if=/dev/zero of=/swapfile bs=1M count=2048
  chmod 600 /swapfile
  mkswap /swapfile
  swapon /swapfile
  echo '/swapfile swap swap defaults 0 0' >> /etc/fstab
fi

# ASP.NET Core 9 runtime (Linux-x64 framework-dependent publish)
curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
bash /tmp/dotnet-install.sh --channel 9.0 --runtime aspnetcore --install-dir /usr/share/dotnet
ln -sfn /usr/share/dotnet/dotnet /usr/bin/dotnet

mkdir -p /opt/petel/ath-api /opt/petel/ath-blazor /opt/petel/assist-api /opt/petel/assist-blazor /etc/petel
chown -R ec2-user:ec2-user /opt/petel

cat >/usr/share/nginx/html/index.html <<'HTML'
<!doctype html><title>petel-test</title>
<p>petel-test host is up. Deploy apps with Deploy-Aws-Test.ps1.</p>
HTML

systemctl enable --now nginx
