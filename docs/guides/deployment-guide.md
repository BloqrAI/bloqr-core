# Deployment Guide

Comprehensive guide for deploying ad-blocking components in production environments.

## Overview

This guide covers deployment strategies for all components in the ad-blocking repository, including Docker containerization, CI/CD pipelines, environment configuration, and best practices for production deployments.

## Deployment Strategies

### 1. Docker Deployment

This repository's Docker artifacts support local development and testing only.
The development image is not a production runtime, and this repository does
not include `Dockerfile.production`, a cron image, or a deployable service.
Use the development container from the repository root:

```bash
docker compose up -d dev
docker compose exec dev bash
docker compose --profile test run --rm test
```

See the [Docker guide](/docs/docker-guide/) for toolchain details and compiler
commands. Swift is tested separately on macOS. This repository's service
deployment target is Cloudflare Workers in projects that contain deployable
services.

### 2. Kubernetes Deployment

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

The repository's
[`docker-image.yml`](../../.github/workflows/docker-image.yml) validates
Compose configuration and builds the development image when Docker artifacts
change. It does not publish or deploy a production image.

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
  image: rust:1.86
  script:
    - cargo test -p bloqr-compiler-core -p bloqr-compiler

build:development-image:
  stage: build
  image: docker:latest
  services:
    - docker:dind
  script:
    - docker build -f Dockerfile.warp -t bloqr-core-dev:$CI_COMMIT_SHA .
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
                            cargo test -p bloqr-compiler-core -p bloqr-compiler
                        '''
                    }
                }
            }
        }
        
        stage('Build development image') {
            steps {
                sh 'docker build -f Dockerfile.warp -t bloqr-core-dev:${BUILD_NUMBER} .'
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

#### Development Container Environment

```bash
# Optional .env file in the repository root
DEBUG=true
```

Start the development container with:

```bash
docker compose up -d dev
```

## Production Best Practices

### 1. Security

The development image has no service credentials. Avoid passing secrets to
the container; deployable services should use the secret-management mechanism
of their target platform.

### 2. Monitoring

The development container does not host a network service and therefore has
no Docker health check. Its validation is performed by the repository's
tests and Docker image workflow.

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
