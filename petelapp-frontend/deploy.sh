#!/bin/bash

ENVIRONMENT=$1

if [ -z "$ENVIRONMENT" ]; then
    echo "Usage: ./deploy.sh [development|staging|production]"
    exit 1
fi

echo "🚀 Deploying for environment: $ENVIRONMENT"

# Copy environment-specific config
if [ -f "public/env-config.$ENVIRONMENT.js" ]; then
    cp "public/env-config.$ENVIRONMENT.js" "public/env-config.js"
    echo "✅ Using env-config.$ENVIRONMENT.js"
else
    echo "❌ Environment config file not found: env-config.$ENVIRONMENT.js"
    exit 1
fi

# Continue with deployment...
echo "✅ Deployment configuration complete"