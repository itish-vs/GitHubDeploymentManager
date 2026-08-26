# .NET Version Upgrade

## Strategy
Upgrade both projects to .NET 10 LTS (`net10.0-windows`) and align package references, then fix any build/test issues introduced by the runtime change.

## Preferences
- **Flow Mode**: Automatic
- **Commit Strategy**: After Each Task
- **Pace**: Standard
- **Target Framework**: net10.0

## Decisions
- Target framework set to .NET 10 LTS (`net10.0-windows`) based on the current recommendation and support lifecycle.

## Build Tool Decisions
- **GitHubDeploymentManager.csproj**: dotnet build
- **GitHubDeploymentManager.Tests\GitHubDeploymentManager.Tests.csproj**: dotnet build
- **Validation**: full solution build succeeded; xUnit tests passed on `net10.0-windows`
