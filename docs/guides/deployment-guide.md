# Deployment Guide

Comprehensive guide for deploying ad-blocking components in production environments.

## Overview

This guide covers deployment strategies for all components in the ad-blocking repository, including Docker containerization, CI/CD pipelines, environment configuration, and best practices for production deployments.

## Deployment Strategies

### 1. Docker Deployment

#### Pre-built Development Image

Use the pre-configured Docker image with all dependencies:

```bash
# Pull the image
docker pull jaysonknight/warp-env:ad-blocking

# Run container
docker run -it -v $(pwd):/workspace jaysonknight/warp-env:ad-blocking
```

#### Custom Production Image

Build a minimal production image:

```dockerfile
# Dockerfile.production
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build

# Copy and build only what you need
WORKDIR /app
COPY src/ ./src/
# Mount or copy your compiler config at runtime (e.g. from a checkout of
# BloqrAI/bloqr-blocklists) — it's no longer bundled in this repo's src/ tree.

# Build specific component
RUN cd src/compilers/dotnet && dotnet publish -c Release -o /app/out

FROM mcr.microsoft.com/dotnet/runtime:10.0-alpine
WORKDIR /app
COPY --from=build /app/out .

ENTRYPOINT ["dotnet", "Bloqr.Compiler.Dotnet.Console.dll"]
```

Build and run:

```bash
docker build -f Dockerfile.production -t ad-blocking-compiler:latest .
docker run -v $(pwd)/rules:/app/rules ad-blocking-compiler:latest
```

#### Multi-stage Builds for Each Component

**TypeScript/Deno:**
```dockerfile
FROM denoland/deno:2.0.0

WORKDIR /app
COPY src/compilers/typescript/ .

RUN deno cache src/mod.ts

CMD ["deno", "task", "compile"]
```

**.NET:**
```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/compilers/dotnet/ .
RUN dotnet publish -c Release -o /app

FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "Bloqr.Compiler.Dotnet.Console.dll"]
```

**Python:**
```dockerfile
FROM python:3.12-slim

WORKDIR /app
COPY src/compilers/python/ .

RUN pip install --no-cache-dir -e .

CMD ["bloqr-compiler"]
```

**Rust:**
```dockerfile
FROM rust:1.70 AS builder
WORKDIR /app
COPY src/compilers/rust/ .
RUN cargo build --release

FROM debian:bookworm-slim
COPY --from=builder /app/target/release/bloqr-compiler /usr/local/bin/
CMD ["bloqr-compiler"]
```

### 2. Docker Compose Deployment

Create a `docker-compose.yml` for orchestration:

```yaml
version: '3.8'

services:
  # Rules compilation service
  rules-compiler:
    build:
      context: .
      dockerfile: Dockerfile.production
    volumes:
      - ./rules:/app/rules
      - ./Config:/app/Config
    environment:
      - CONFIG_PATH=/app/Config/compiler-config.json
    restart: unless-stopped

  # For a deployable AdGuard DNS API service, see the API client Dockerfiles
  # in BloqrAI/bloqr-apiclients — they're no longer part of this repo.

  # Scheduled compilation
  compiler-cron:
    build:
      context: .
      dockerfile: Dockerfile.cron
    volumes:
      - ./rules:/app/rules
      - ./Config:/app/Config
    environment:
      - CRON_SCHEDULE=0 */6 * * *  # Every 6 hours
    restart: unless-stopped
```

Run with:

```bash
docker-compose up -d
docker-compose logs -f
```

### 3. Kubernetes Deployment

#### ConfigMap for Configuration

```yaml
# configmap.yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: compiler-config
data:
  compiler-config.json: |
    {
      "name": "Production Filter List",
      "version": "1.0.0",
      "sources": [
        {
          "name": "EasyList",
          "source": "https://easylist.to/easylist/easylist.txt",
          "type": "adblock"
        }
      ],
      "transformations": [
        "Validate",
        "Deduplicate",
        "RemoveEmptyLines",
        "InsertFinalNewLine"
      ]
    }
```

#### Secret for API Keys

```yaml
# secret.yaml
apiVersion: v1
kind: Secret
metadata:
  name: adguard-api-secret
type: Opaque
stringData:
  api-key: your-api-key-here
```

#### Deployment

```yaml
# deployment.yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: rules-compiler
spec:
  replicas: 1
  selector:
    matchLabels:
      app: rules-compiler
  template:
    metadata:
      labels:
        app: rules-compiler
    spec:
      containers:
      - name: compiler
        image: ad-blocking-compiler:latest
        env:
        - name: CONFIG_PATH
          value: /config/compiler-config.json
        - name: ADGUARD_AdGuard__ApiKey
          valueFrom:
            secretKeyRef:
              name: adguard-api-secret
              key: api-key
        volumeMounts:
        - name: config
          mountPath: /config
        - name: rules
          mountPath: /app/rules
      volumes:
      - name: config
        configMap:
          name: compiler-config
      - name: rules
        persistentVolumeClaim:
          claimName: rules-pvc
```

#### CronJob for Scheduled Compilation

```yaml
# cronjob.yaml
apiVersion: batch/v1
kind: CronJob
metadata:
  name: rules-compiler-cron
spec:
  schedule: "0 */6 * * *"  # Every 6 hours
  jobTemplate:
    spec:
      template:
        spec:
          containers:
          - name: compiler
            image: ad-blocking-compiler:latest
            env:
            - name: CONFIG_PATH
              value: /config/compiler-config.json
            volumeMounts:
            - name: config
              mountPath: /config
            - name: rules
              mountPath: /app/rules
          restartPolicy: OnFailure
          volumes:
          - name: config
            configMap:
              name: compiler-config
          - name: rules
            persistentVolumeClaim:
              claimName: rules-pvc
```

Apply:

```bash
kubectl apply -f configmap.yaml
kubectl apply -f secret.yaml
kubectl apply -f deployment.yaml
kubectl apply -f cronjob.yaml
```

## CI/CD Integration

### GitHub Actions

#### Complete Workflow

```yaml
# .github/workflows/deploy.yml
name: Build and Deploy

on:
  push:
    branches: [ main ]
    tags: [ 'v*' ]
  pull_request:
    branches: [ main ]

env:
  REGISTRY: ghcr.io
  IMAGE_NAME: ${{ github.repository }}

jobs:
  test:
    runs-on: ubuntu-latest
    strategy:
      matrix:
        component: [typescript, dotnet, python, rust]
    steps:
      - uses: actions/checkout@v4
      
      - name: Test TypeScript
        if: matrix.component == 'typescript'
        run: |
          cd src/compilers/typescript
          deno cache src/mod.ts
          deno task test
      
      - name: Test .NET
        if: matrix.component == 'dotnet'
        run: |
          cd src/compilers/dotnet
          dotnet test CompilerDotnet.slnx
      
      - name: Test Python
        if: matrix.component == 'python'
        run: |
          cd src/compilers/python
          pip install -e ".[dev]"
          pytest
      
      - name: Test Rust
        if: matrix.component == 'rust'
        run: |
          cd src/compilers/rust
          cargo test

  build:
    needs: test
    runs-on: ubuntu-latest
    permissions:
      contents: read
      packages: write
    steps:
      - uses: actions/checkout@v4
      
      - name: Log in to Container Registry
        uses: docker/login-action@v3
        with:
          registry: ${{ env.REGISTRY }}
          username: ${{ github.actor }}
          password: ${{ secrets.GITHUB_TOKEN }}
      
      - name: Extract metadata
        id: meta
        uses: docker/metadata-action@v5
        with:
          images: ${{ env.REGISTRY }}/${{ env.IMAGE_NAME }}
      
      - name: Build and push Docker image
        uses: docker/build-push-action@v5
        with:
          context: .
          file: Dockerfile.production
          push: ${{ github.event_name != 'pull_request' }}
          tags: ${{ steps.meta.outputs.tags }}
          labels: ${{ steps.meta.outputs.labels }}

  deploy:
    needs: build
    runs-on: ubuntu-latest
    if: github.ref == 'refs/heads/main'
    steps:
      - uses: actions/checkout@v4
      
      - name: Deploy to production
        run: |
          # Add your deployment commands here
          echo "Deploying to production..."
```

#### Compile and Release Workflow

```yaml
# .github/workflows/compile-rules.yml
name: Compile and Release Rules

on:
  schedule:
    - cron: '0 */6 * * *'  # Every 6 hours
  workflow_dispatch:

jobs:
  compile:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      
      - name: Setup Deno
        uses: denoland/setup-deno@v1
        with:
          deno-version: v2.x
      
      - name: Compile rules
        run: |
          cd src/compilers/typescript
          deno task compile -- -c ../../Config/compiler-config.json -r
      
      - name: Upload artifact
        uses: actions/upload-artifact@v3
        with:
          name: compiled-rules
          path: ../bloqr-blocklists/output/adguard_user_filter.txt
      
      - name: Create Release
        if: github.event_name == 'schedule'
        uses: softprops/action-gh-release@v1
        with:
          tag_name: rules-${{ github.run_number }}
          files: ../bloqr-blocklists/output/adguard_user_filter.txt
```

### GitLab CI/CD

```yaml
# .gitlab-ci.yml
stages:
  - test
  - build
  - deploy

variables:
  DOCKER_DRIVER: overlay2

test:typescript:
  stage: test
  image: denoland/deno:2.0.0
  script:
    - cd src/compilers/typescript
    - deno cache src/mod.ts
    - deno task test

test:dotnet:
  stage: test
  image: mcr.microsoft.com/dotnet/sdk:10.0
  script:
    - cd src/compilers/dotnet
    - dotnet test CompilerDotnet.slnx

test:python:
  stage: test
  image: python:3.12
  script:
    - cd src/compilers/python
    - pip install -e ".[dev]"
    - pytest

test:rust:
  stage: test
  image: rust:1.70
  script:
    - cd src/compilers/rust
    - cargo test

build:
  stage: build
  image: docker:latest
  services:
    - docker:dind
  script:
    - docker build -f Dockerfile.production -t $CI_REGISTRY_IMAGE:$CI_COMMIT_SHA .
    - docker push $CI_REGISTRY_IMAGE:$CI_COMMIT_SHA

deploy:production:
  stage: deploy
  image: alpine:latest
  only:
    - main
  script:
    - apk add --no-cache curl
    - curl -X POST $ADGUARD_WEBHOOK_URL
```

### Jenkins Pipeline

```groovy
// Jenkinsfile
pipeline {
    agent any
    
    stages {
        stage('Test') {
            parallel {
                stage('TypeScript') {
                    steps {
                        sh '''
                            cd src/compilers/typescript
                            deno cache src/mod.ts
                            deno task test
                        '''
                    }
                }
                stage('.NET') {
                    steps {
                        sh '''
                            cd src/compilers/dotnet
                            dotnet test CompilerDotnet.slnx
                        '''
                    }
                }
                stage('Python') {
                    steps {
                        sh '''
                            cd src/compilers/python
                            pip install -e ".[dev]"
                            pytest
                        '''
                    }
                }
                stage('Rust') {
                    steps {
                        sh '''
                            cd src/compilers/rust
                            cargo test
                        '''
                    }
                }
            }
        }
        
        stage('Build') {
            steps {
                sh 'docker build -f Dockerfile.production -t ad-blocking:${BUILD_NUMBER} .'
            }
        }
        
        stage('Deploy') {
            when {
                branch 'main'
            }
            steps {
                sh '''
                    docker tag ad-blocking:${BUILD_NUMBER} ad-blocking:latest
                    docker push ad-blocking:latest
                '''
            }
        }
    }
}
```

## Environment Configuration

### Environment Variables

#### Rules Compilers

| Variable | Description | Default | Required |
|----------|-------------|---------|----------|
| `CONFIG_PATH` | Path to configuration file | `compiler-config.json` | No |
| `DEBUG` | Enable debug logging | `false` | No |
| `RULES_DIR` | Output directory for rules | `./rules` | No |
| `BLOQR_COMPILER_Logging__LogLevel__Default` | .NET log level | `Information` | No |

#### API Clients

The AdGuard DNS API clients (and their environment variables) moved to [`BloqrAI/bloqr-apiclients`](https://github.com/BloqrAI/bloqr-apiclients) and are no longer part of this repo.

### Configuration Files

#### Production Configuration Example

```yaml
# config/production.yaml
name: Production Filter List
description: Production ad-blocking filter
version: "2.0.0"
homepage: https://github.com/yourusername/ad-blocking
license: GPL-3.0

sources:
  - name: EasyList
    source: https://easylist.to/easylist/easylist.txt
    type: adblock
    transformations:
      - Validate
      - RemoveModifiers

  - name: EasyPrivacy
    source: https://easylist.to/easylist/easyprivacy.txt
    type: adblock
    transformations:
      - Validate
      - RemoveModifiers

  - name: AdGuard Base
    source: https://raw.githubusercontent.com/AdguardTeam/FiltersRegistry/master/filters/filter_2_Base/filter.txt
    type: adblock

transformations:
  - Deduplicate
  - RemoveEmptyLines
  - TrimLines
  - InsertFinalNewLine
  - ConvertToAscii

exclusions_sources:
  - config/whitelist.txt
```

#### Environment-Specific Configurations

```bash
# config/.env.production
ADGUARD_AdGuard__ApiKey=prod_api_key_here
CONFIG_PATH=/app/config/production.yaml
RULES_DIR=/app/rules
DEBUG=false

# config/.env.staging
ADGUARD_AdGuard__ApiKey=staging_api_key_here
CONFIG_PATH=/app/config/staging.yaml
RULES_DIR=/app/rules
DEBUG=true
```

Load with:

```bash
docker run --env-file config/.env.production ad-blocking:latest
```

## Production Best Practices

### 1. Security

**Secrets Management:**

```bash
# Use Docker secrets
echo "your-api-key" | docker secret create adguard_api_key -

# Reference in docker-compose.yml
services:
  api-service:
    secrets:
      - adguard_api_key
    environment:
      - ADGUARD_AdGuard__ApiKey_FILE=/run/secrets/adguard_api_key

secrets:
  adguard_api_key:
    external: true
```

**Use HashiCorp Vault:**

```bash
# Store secret
vault kv put secret/adguard api_key=your-api-key

# Retrieve in application
export ADGUARD_AdGuard__ApiKey=$(vault kv get -field=api_key secret/adguard)
```

### 2. Monitoring

**Health Checks:**

```dockerfile
# Add health check to Dockerfile
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD curl -f http://localhost:5000/health || exit 1
```

**Logging:**

```yaml
# docker-compose.yml
services:
  rules-compiler:
    logging:
      driver: "json-file"
      options:
        max-size: "10m"
        max-file: "3"
```

**Prometheus Metrics:**

```csharp
// Add to .NET application
services.AddHealthChecks();
app.MapHealthChecks("/health");
app.MapMetrics();  // Prometheus metrics endpoint
```

### 3. Performance

**Resource Limits:**

```yaml
# docker-compose.yml
services:
  rules-compiler:
    deploy:
      resources:
        limits:
          cpus: '2'
          memory: 2G
        reservations:
          cpus: '1'
          memory: 1G
```

**Caching:**

```dockerfile
# Cache dependencies in layers
FROM denoland/deno:2.0.0
WORKDIR /app

# Copy dependency files first
COPY deno.json deno.lock ./
RUN deno cache --lock=deno.lock

# Then copy source
COPY . .
```

### 4. Backup and Recovery

**Volume Backups:**

```bash
# Backup rules volume
docker run --rm \
  -v ad-blocking_rules:/data \
  -v $(pwd)/backups:/backup \
  alpine tar czf /backup/rules-backup-$(date +%Y%m%d).tar.gz -C /data .

# Restore
docker run --rm \
  -v ad-blocking_rules:/data \
  -v $(pwd)/backups:/backup \
  alpine tar xzf /backup/rules-backup-20250101.tar.gz -C /data
```

**Automated Backups:**

```yaml
# backup-cronjob.yaml
apiVersion: batch/v1
kind: CronJob
metadata:
  name: rules-backup
spec:
  schedule: "0 2 * * *"  # Daily at 2 AM
  jobTemplate:
    spec:
      template:
        spec:
          containers:
          - name: backup
            image: alpine:latest
            command:
            - /bin/sh
            - -c
            - tar czf /backup/rules-$(date +%Y%m%d).tar.gz -C /data .
            volumeMounts:
            - name: rules
              mountPath: /data
            - name: backup
              mountPath: /backup
          restartPolicy: OnFailure
          volumes:
          - name: rules
            persistentVolumeClaim:
              claimName: rules-pvc
          - name: backup
            persistentVolumeClaim:
              claimName: backup-pvc
```

## Scaling Strategies

### Horizontal Scaling

```yaml
# kubernetes-hpa.yaml
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: rules-compiler-hpa
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: rules-compiler
  minReplicas: 2
  maxReplicas: 10
  metrics:
  - type: Resource
    resource:
      name: cpu
      target:
        type: Utilization
        averageUtilization: 70
```

### Load Balancing

```yaml
# nginx-lb.conf
upstream compilers {
    least_conn;
    server compiler1:5000;
    server compiler2:5000;
    server compiler3:5000;
}

server {
    listen 80;
    location / {
        proxy_pass http://compilers;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
    }
}
```

## Troubleshooting Deployment Issues

### Container Startup Failures

```bash
# Check logs
docker logs <container_id>

# Inspect container
docker inspect <container_id>

# Check resource constraints
docker stats
```

### Network Issues

```bash
# Test connectivity
docker exec <container_id> ping api.adguard-dns.io

# Check DNS resolution
docker exec <container_id> nslookup api.adguard-dns.io

# Inspect network
docker network inspect <network_name>
```

### Permission Issues

```bash
# Run with specific user
docker run --user $(id -u):$(id -g) ad-blocking:latest

# Fix volume permissions
docker run --rm -v ad-blocking_rules:/data alpine chown -R 1000:1000 /data
```

## Related Documentation

- [Docker Guide](../docker-guide.md)
- [Testing Guide](./testing-guide.md)
- [Troubleshooting Guide](./troubleshooting-guide.md)
- [Configuration Reference](../configuration-reference.md)

## License

GPLv3 - See [LICENSE](../../LICENSE) for details.
