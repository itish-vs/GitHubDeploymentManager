# 01-project-targets-and-packages: Project Targets and Packages

## Objective
Update both project files to target .NET 10 and refresh package references to versions compatible with the new runtime. Keep the app and test project aligned so the solution remains buildable as a whole.

## Scope Inventory
- **Projects affected**: `GitHubDeploymentManager.csproj`, `GitHubDeploymentManager.Tests\GitHubDeploymentManager.Tests.csproj`
- **Distinct concerns**: target framework upgrade, package alignment, pruning-warning cleanup, and solution validation
- **Change signals**:
  - App project: WinForms desktop app targeting `net10.0-windows`
  - Test project: xUnit test project targeting `net10.0-windows`
  - Package pruning warning encountered for `System.Configuration.ConfigurationManager`, which was removed because the SDK provides it automatically
- **Skill matches**: managing-target-frameworks, managing-package-references, building-projects

## Findings
- Both projects were already updated to `net10.0-windows`.
- `Microsoft.Extensions.DependencyInjection` remains on `10.0.11` in the app project.
- Test package versions are stable and build successfully on .NET 10.
- Solution builds successfully and tests pass.

**Done when**: Both project files target `net10.0-windows`, package references are updated where needed, and the solution still restores successfully.
