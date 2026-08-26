# GitHubDeploymentManager
A small Windows desktop tool to discover GitHub repositories and published releases, validate release packages (ZIP/EXE/source ZIP), and deploy them to a local folder or network share. Designed for simple, repeatable deployments with pre-deployment validation and progress feedback.
Highlights
•	Three-step workflow: select GitHub source → choose release package → confirm deployment destination
•	Validates release packages before deploying (prefers ZIP with contained EXE, EXE asset, or source zip)
•	Supports private repositories and authenticated API requests via Personal Access Token (PAT)
•	Deploys ZIPs (extracted) or single-files (EXE) to local or network paths with progress reporting
•	Built with .NET 10 and WinForms
Quick Demo
•	Step 1: Enter a GitHub owner (or restore from App.config) and fetch repositories.
•	Step 2: Select a release — the app validates package accessibility and contents.
•	Step 3: Enter a deployment path (local folder or network share) and Test connection, then Deploy.
Screenshots
•	Add images/screenshots into the repo and reference them here:
•	docs/screenshot-releases.png
•	docs/screenshot-deploy.png
Requirements
•	Windows 10/11
•	.NET 10 Runtime (for running) or Visual Studio 2022/2026 (or dotnet SDK) for building
•	Network access to GitHub and target deployment destination (for network shares)
Build & Run
•	Using Visual Studio:
1.	Open GitHubDeploymentManager.slnx in Visual Studio.
2.	Build and run (F5).
•	Using dotnet CLI:
1.	dotnet build
2.	dotnet run --project src/GitHubDeploymentManager (adjust path as needed)
Configuration (App.config)
•	The app reads configuration from App.config (AppSettings). Example keys:
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

•	Notes:
•	GitHubToken: best left empty here for security; you can paste a PAT in the UI. If you store it, treat the file as sensitive.
•	DefaultDeploymentPath: will be validated on startup and after Reset.
•	GitHubOwner (optional): when Reset/Clear is used, the owner field will be restored from this value but repositories are not auto-fetched.
Personal Access Token (PAT)
•	For private repositories or to avoid API rate limits, supply a GitHub PAT.
•	Recommended scopes:
•	repo (for private repo asset access)
•	Without a PAT the app uses unauthenticated GitHub API limits (~60 req/hr).
Usage
1.	(Optional) Paste a PAT in the PAT panel for authenticated requests.
2.	Enter GitHub owner and click Fetch.
3.	Select a repository and then a release from the list.
4.	Wait for release validation (app checks asset accessibility and contents).
5.	Enter or browse to a deployment path (local or UNC) and Test connection.
6.	Click Deploy release. Monitor progress and logs.
Logging
•	Logs are written to the configured LogFolderPath (defaults to Logs inside the app folder).
•	See deployment-YYYY-MM-DD.log for operation traces and error details.
Troubleshooting
•	"Release unavailable / HTTP 404" when validating:
•	For private assets ensure PAT with repo scope is provided.
•	Some assets require calling the GitHub release asset API endpoint with the Authorization header and Accept: application/octet-stream. The app uses that approach; if you still see 404 paste the log lines here (include attempted URL).
•	Rate limit / 403:
•	Add a PAT or wait for the rate limit to reset. Logs include X-RateLimit-Reset time if applicable.
•	Deployment path inaccessible:
•	Confirm network share credentials and permissions (the app uses the current user's credentials).
•	Test connection via the Test button and check logs for underlying exception messages.
•	UI scroll jumping:
•	The app preserves scroll/focus for transient UI changes; if you still see jumps note which control and actions reproduce the behavior.
Security
•	PATs are sensitive — avoid committing them into version control.
•	Consider using system-level secure storage if you need persistence (not implemented by default).
Extending / Contributing
•	Contributions welcome. Suggested areas:
•	Add support for .tar.gz source packages
•	Add secure storage for PATs
•	Add more deployment strategies (rsync/SFTP/etc.)
•	Open an issue or submit a pull request. Follow repository coding style and tests.
Tests
•	Unit tests exist for core services (GitHub service, Deployment service). Run with your test runner in Visual Studio or dotnet test.
License
•	Add your license here (e.g., MIT). Example:
•	MIT License — see LICENSE file
Contact / Issues
•	Report issues in the project GitHub Issues with logs and reproduction steps.
Changelog (quick)
•	v1.0 — Basic release discovery, validation, and deployment workflow.
•	v1.x — Addments and fixes (see project CHANGELOG.md if present).
