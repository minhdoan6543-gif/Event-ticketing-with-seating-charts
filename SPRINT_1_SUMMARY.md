# Sprint 1 Completion Summary

I have successfully implemented the remaining requirements for SCRUM-72, 73, and 74. 

Here are the details of the updates made:

### 1. API Documentation UI (SCRUM-72)
- Added the **`Scalar.AspNetCore`** package to the `EventTicketing.Api` project.
- Configured `Program.cs` to expose the Scalar UI (by default at `/scalar/v1`) and the OpenAPI schema when the application is running in either `Development` or `Staging` environments:
  ```csharp
  if (app.Environment.IsDevelopment() || app.Environment.IsStaging())
  {
      app.MapOpenApi();
      app.MapScalarApiReference(options => 
      {
          options.EndpointPathPrefix = "/scalar/v1";
      });
  }
  ```

### 2. CI Pipeline with Code Coverage (SCRUM-73)
- Updated `.github/workflows/ci.yml` to add code coverage collection to the `dotnet test` command via the `--collect:"XPlat Code Coverage"` flag. 
- The pipeline now explicitly restores dependencies, performs formatting verification (`dotnet format --verify-no-changes`), builds the codebase, and outputs the necessary coverage reports. I also added a placeholder step to upload those reports to Codecov if needed.

### 3. Automated Staging Deployment (SCRUM-74)
- Created a dedicated CD workflow file at `.github/workflows/deploy-staging.yml`.
- This new workflow is triggered on pushes to the `staging` and `main` branches.
- **Workflow behavior**:
  - Authenticates securely to the GitHub Container Registry (`ghcr.io`).
  - Builds the Docker image based on the multi-stage `Dockerfile` and pushes it with a `:staging` tag.
  - SSHs into the Staging server using GitHub Actions secrets (`STAGING_HOST`, `STAGING_USER`, `STAGING_SSH_KEY`).
  - Pulls the new image and spins up the stack using `docker-compose`.
- **Staging configuration**:
  - Created `EventTicketing.Api/appsettings.Staging.json` to outline the configuration layout for the environment.
  - Complemented it by creating `docker-compose.staging.yml`. This setup inherently applies the necessary configuration overrides for .NET natively via Environment Variables (like `ConnectionStrings__DefaultConnection` and `ASPNETCORE_ENVIRONMENT=Staging`), cleanly isolating the PostgreSQL/Redis deployments for the Staging machine.
