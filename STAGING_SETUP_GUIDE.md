# Staging Setup Guide & Required Secrets

This document outlines the prerequisites and GitHub Secrets required to successfully run the Continuous Deployment (CD) pipeline to the staging environment.

## 1. Required GitHub Repository Secrets
Navigate to your GitHub repository on the web, go to **Settings > Secrets and variables > Actions**, and add the following repository secrets:

| Secret Name | Description |
| :--- | :--- |
| **`STAGING_HOST`** | The public IP address or FQDN of your staging server (e.g., `100.22.33.44` or `staging.yourdomain.com`). |
| **`STAGING_USER`** | The SSH username to connect to the staging server (e.g., `ubuntu` or `root`). |
| **`STAGING_SSH_KEY`** | The **Private SSH Key** (Ed25519 or RSA) used to authenticate with the staging host. The corresponding public key must be added to `~/.ssh/authorized_keys` on the server. |
| **`DB_PASSWORD`** | A strong, secure password that will be assigned to the staging PostgreSQL database. |
| **`CR_PAT`** | A GitHub Personal Access Token (classic or fine-grained) with **`read:packages`** permission. The staging server uses this token to authenticate and pull your private Docker image from `ghcr.io`. |

*(Note: The `GITHUB_TOKEN` used in the CI pipeline to push images is automatically generated and does not need to be manually added. However, you must ensure that your repository Action settings grant `packages: write` permissions, which the workflow is already configured to request.)*

## 2. Server Prerequisites
Before running the pipeline for the first time, ensure your staging server meets the following requirements:

1. **Docker Engine**: Installed and running.
2. **Docker Compose**: The Compose V2 plugin (`docker compose`) must be installed.
3. **Firewall Rules**:
   - Port **8080** must be open for incoming HTTP traffic (so the API and Health Check can be reached).
   - Port **443** must be open if you intend to configure SSL/TLS.
   - Port **22** must be open for the GitHub Actions runner to establish the SSH connection.

## 3. How the Pipeline Works
1. Upon a push to the `staging` or `main` branches (or via manual `workflow_dispatch`), the `.github/workflows/deploy-staging.yml` workflow triggers.
2. It builds the .NET 10 API into a multi-stage Docker image and tags it with `:staging` and `:${{ github.sha }}`.
3. The image is pushed securely to the GitHub Container Registry (`ghcr.io`).
4. Using SCP, `docker-compose.staging.yml` and a dynamically generated `.env` file (containing your `DB_PASSWORD`) are copied to `/opt/event-ticketing` on the staging server.
5. Using SSH, the staging server logs into `ghcr.io`, pulls the newly minted image, and runs `docker compose up -d --remove-orphans`. EF Core migrations execute automatically via `Program.cs`.
6. The workflow finishes by executing an automated `curl` health check against `http://<STAGING_HOST>:8080/health`. An HTTP `200 OK` confirms a successful deployment!
