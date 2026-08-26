# GitHub Deployment Manager

<p align="center">
  <strong>A Windows desktop tool for discovering GitHub releases, validating release packages, and deploying them to local folders or network shares.</strong>
</p>

<p align="center">
  <img src="https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white" alt=".NET 10">
  <img src="https://img.shields.io/badge/Windows-10%2B-0078D6?logo=windows&logoColor=white" alt="Windows 10+">
  <img src="https://img.shields.io/badge/UI-WinForms-0078D7" alt="WinForms">
  <img src="https://img.shields.io/badge/GitHub-REST%20API-181717?logo=github&logoColor=white" alt="GitHub REST API">
</p>

<p align="center">
  Simple, repeatable GitHub release deployments with package validation, PAT authentication, network path support, progress feedback, and application logging.
</p>

## Overview

**GitHub Deployment Manager** is a Windows desktop application built with .NET 10 and WinForms.

It provides a guided three step workflow:

1. Select a GitHub owner and load repositories.
2. Select a published release and validate its deployable package.
3. Confirm a local or network deployment destination and deploy the selected release.

The application is intended for small and medium sized internal deployment workflows where a full CI/CD platform is unnecessary or where a lightweight operator driven deployment utility is preferred.

## Features

* **Three step deployment workflow**
  * Select GitHub source
  * Choose release package
  * Confirm deployment destination
* **GitHub release discovery**
  * Loads repositories for the selected owner
  * Lists published releases
  * Lets the user select the release to deploy
* **Release package validation**
  * Prefers a ZIP release asset containing an executable
  * Supports a single EXE release asset
  * Supports GitHub source ZIP packages as a fallback
  * Checks release and asset accessibility before deployment
* **Private repository support**
  * Personal Access Token (PAT) authentication
  * Authenticated GitHub API requests
  * Suitable for private organisational repositories when the token has the required permissions
* **Flexible deployment targets**
  * Local Windows folders
  * UNC network shares
  * Example: `\\server\share\deploy`
* **Deployment feedback**
  * Progress reporting
  * Status messages
  * Detailed operation logs
* **Configuration through `App.config`**
  * Default GitHub owner
  * Default deployment path
  * GitHub API base URL
  * Request timeout
  * Logging location
  * Cache duration
  * Repository result limit

## Screenshots

### Step 1: Select the GitHub source

Enter the GitHub owner or organisation, then fetch the repositories available to the authenticated account.

<p align="center">
  <img src="docs/screenshot-github-source.png" alt="GitHub Deployment Manager showing the GitHub source and repository selection screen" width="100%">
</p>

### Step 2: Choose and validate a release package

Select a published release. The application validates that the release is accessible and identifies the package that can be deployed.

<p align="center">
  <img src="docs/screenshot-release-and-deploy.png" alt="GitHub Deployment Manager showing release validation and deployment destination" width="100%">
</p>

### Step 3: Confirm the deployment destination

The deployment destination can be a local folder or a network share. Use **Test** before deployment to verify that the current Windows account can access the destination.

## Typical Workflow

```text
GitHub Owner / Organisation
          |
          v
      Fetch Repos
          |
          v
    Select Repository
          |
          v
     Select Release
          |
          v
  Validate Release Asset
          |
          v
   Test Deployment Path
          |
          v
      Deploy Release
```

A typical deployment target can be:

```text
C:\Applications\MyApp
```

or:

```text
\\SERVER01\Applications\MyApp
```

## Requirements

### Runtime

* Windows 10 or later
* .NET 10 runtime

### Development

* Visual Studio 2026 with the .NET desktop development workload, or
* .NET 10 SDK
* Network access to GitHub
* Access to the destination folder or network share using the Windows account running the application

## Build and Run

### Visual Studio

1. Open `GitHubDeploymentManager.slnx`.
2. Restore NuGet packages.
3. Build the solution.
4. Run the WinForms project with **F5**.

### .NET CLI

From the repository root:

```bash
dotnet restore
dotnet build
dotnet run --project src/GitHubDeploymentManager
```

Adjust the project path if the repository structure differs.

## Configuration

The application reads its default settings from `App.config` using `appSettings`.

Example:

```xml
<configuration>
  <appSettings>
    <add key="GitHubOwner" value="your-default-owner" />
    <add key="DefaultDeploymentPath" value="\\server\share\deploy" />
    <add key="GitHubApiBaseUrl" value="https://api.github.com/" />
    <add key="GitHubTimeoutSeconds" value="60" />
    <add key="GitHubToken" value="" />
    <add key="LogFolderPath" value="Logs" />
    <add key="GitHubCacheMinutes" value="5" />
    <add key="MaxRepositoryResults" value="50" />
  </appSettings>
</configuration>
```

### Configuration keys

| Key | Description | Example |
|---|---|---|
| `GitHubOwner` | Default GitHub user or organisation shown when the app starts or is reset | `my-company` |
| `DefaultDeploymentPath` | Default local or UNC deployment destination | `\\server\share\deploy` |
| `GitHubApiBaseUrl` | GitHub REST API base URL | `https://api.github.com/` |
| `GitHubTimeoutSeconds` | HTTP request timeout | `60` |
| `GitHubToken` | Optional PAT stored in configuration | Empty by default |
| `LogFolderPath` | Folder used for application logs | `Logs` |
| `GitHubCacheMinutes` | Repository cache duration | `5` |
| `MaxRepositoryResults` | Maximum repository results to load | `50` |

### Configuration notes

* `GitHubToken` should normally be left empty. The application can accept a PAT through the UI.
* If a PAT is stored in `App.config`, treat the file as sensitive and do not commit secrets to source control.
* `DefaultDeploymentPath` is validated when the application starts and when the configuration is reset.
* `GitHubOwner` is restored when **Clear/Reset** is used, but repositories are not automatically fetched.

## Personal Access Token (PAT)

A PAT can be supplied from the **PAT** control in the application.

A token is useful when:

* The repository is private.
* Release assets are private.
* The application needs authenticated GitHub API requests.
* You want higher API limits than unauthenticated requests.

For fine grained PATs, grant only the repository permissions required by the application. For classic PATs, use the minimum appropriate scope for the repositories being accessed.

> **Security:** Never commit a real PAT to `App.config`, source control, screenshots, issue reports, or logs.

### GitHub Enterprise Server

The API base URL is configurable, so the application can be used with GitHub Enterprise Server deployments.

For example:

```xml
<add key="GitHubApiBaseUrl" value="https://github.example.com/api/v3/" />
```

The GitHub owner can be an organisation:

```text
MyOrganisation
```

and the application can then access private repositories that the supplied PAT is authorised to read.

## Release Package Validation

Before deployment, the application checks whether the selected release is usable.

The preferred package types are:

### ZIP release asset

Example:

```text
MyApplication.zip
```

The application validates the asset and inspects its contents.

### EXE release asset

Example:

```text
MyApplication.exe
```

A single executable can be deployed directly to the selected destination.

### GitHub source ZIP

If a dedicated release asset is not available, the application can fall back to the GitHub source ZIP, depending on the configured behaviour.

For production deployments, a dedicated release asset is recommended because it is normally a built and tested deployment artifact rather than the repository source tree.

## Deployment

After validation, the user can select a destination and test access before starting the deployment.

Supported target types include:

```text
C:\Applications\MyApplication
D:\Deployments\MyApplication
\\SERVER01\Applications\MyApplication
\\FILESERVER\Releases\MyApplication
```

The application uses the Windows identity under which it is running. For network paths, that account must have appropriate permissions on the share and underlying NTFS folder.

## Logging

Logs are written to the configured `LogFolderPath`.

The default location is:

```text
Logs
```

A typical log file is:

```text
deployment-YYYY-MM-DD.log
```

Logs are intended to provide:

* Operation traces
* Release and asset information
* Deployment progress
* Authentication and HTTP failures
* File system errors
* Network path errors
* Exception details useful for troubleshooting

Secrets such as PAT values should never be written to logs.

## Troubleshooting

### Release is unavailable or validation returns HTTP 404

For a private repository or private release asset:

1. Confirm that a valid PAT is supplied.
2. Confirm that the PAT has access to the repository.
3. Confirm that the release or asset is accessible to that account.
4. Review the application log for the failed request.

Some release assets require the GitHub release asset API endpoint with an authenticated request and an `Accept: application/octet-stream` header. The application uses that approach for release asset retrieval.

### HTTP 403 or rate limit errors

A `403` may indicate an API rate limit or insufficient permissions.

Try:

* Supplying a PAT.
* Confirming the PAT permissions.
* Waiting for the rate limit window to reset.

The logs include useful GitHub response details, including the rate limit reset information when GitHub provides it.

### Deployment path is inaccessible

For a local folder:

* Confirm that the folder exists.
* Confirm that the Windows account has read and write permissions.

For a UNC path:

* Confirm that the share is reachable.
* Confirm share permissions.
* Confirm NTFS permissions.
* Confirm that the application is running under the expected Windows account.

Use the **Test** button before deployment to identify access problems early.

### UI scrolling or focus changes unexpectedly

The application preserves scroll and focus state for transient UI updates. If unexpected scrolling still occurs, capture the action sequence that reproduces it and review the application log.

## Security

* Treat PATs as credentials.
* Do not commit PATs to Git.
* Avoid storing PATs in plain text unless there is a deliberate operational reason.
* Run the application under a Windows account with only the permissions required for the target deployment paths.
* For network deployments, restrict write access to the required deployment and backup locations.
* Do not include secrets in support logs or screenshots.

For a larger production environment, a future enhancement could integrate Windows Credential Manager or another secure secret store rather than relying on plain text configuration.

## Project Structure


The exact project structure may vary as the solution evolves.

## Tests

Unit tests are available for the core GitHub and deployment services.

Run the complete test suite with:

```bash
dotnet test
```

You can also run individual tests from Visual Studio's Test Explorer.

Recommended test areas include:

* GitHub API authentication
* Repository discovery
* Release discovery
* Release asset validation
* Download handling
* ZIP extraction
* Local deployment
* UNC deployment
* Error handling
* Configuration loading

## Extending the Project

Potential future improvements include:

* Secure PAT storage
* `.tar.gz` package support
* Deployment backups and rollback
* Windows Service lifecycle management
* Scheduled Task lifecycle management
* Health checks after deployment
* Post-deployment commands
* Multiple deployment targets
* Version retention
* Package checksums and signature verification
* SFTP or other deployment strategies
* Automated release monitoring
* Notifications through Teams or email

Contributions and focused improvements are welcome.

Before submitting a pull request:

1. Keep changes scoped to the problem being solved.
2. Update tests for behaviour changes.
3. Update documentation when configuration or user-facing behaviour changes.
4. Do not include credentials, tokens, or machine specific paths in commits.

## Changelog

### v1.0

* GitHub repository discovery
* Published release discovery
* Release package validation
* ZIP, EXE, and source ZIP handling
* Local folder deployment
* UNC network deployment
* PAT authentication
* Configurable GitHub API endpoint
* Deployment progress and logging

For detailed release history, see [`CHANGELOG.md`](CHANGELOG.md) when available.

## License

This project is currently intended to be licensed under the MIT License.

Add the repository license text to:

```text
LICENSE
```

and update this section when the final license is selected.

## Issues and Support

Please report bugs and feature requests through the repository's **Issues** section.

When reporting a deployment problem, include:

* Application version
* GitHub Enterprise or GitHub.com usage
* Repository visibility (public or private)
* Deployment target type (local or UNC)
* Relevant log lines
* Steps to reproduce

Do not include PATs, passwords, connection strings, or other secrets in an issue.

## Keywords

`github` `github-api` `github-releases` `deployment` `deployment-tool` `winforms` `dotnet` `dotnet10` `windows` `network-share` `unc` `pat` `private-repository` `github-enterprise` `release-manager`
