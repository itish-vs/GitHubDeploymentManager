# .NET 10 Upgrade Plan

## Overview

**Target**: Upgrade the GitHubDeploymentManager solution to .NET 10 LTS (`net10.0-windows`) and keep the WinForms app and test project aligned.
**Scope**: 2 projects, Windows Forms desktop app plus test project, with package refreshes and compatibility fixes as needed.

## Tasks

### 01-project-targets-and-packages

Update both project files to target .NET 10 and refresh package references to versions compatible with the new runtime. Keep the app and test project aligned so the solution remains buildable as a whole.

**Done when**: Both project files target `net10.0-windows`, package references are updated where needed, and the solution still restores successfully.

---

### 02-build-fix-windows-forms-compatibility

Resolve any compile-time issues introduced by the framework upgrade in the WinForms application code. Focus on Windows Forms, configuration, and other APIs highlighted by the assessment so the app can build cleanly on .NET 10.

**Done when**: The solution builds without errors on the upgraded target framework.

---

### 03-test-validation

Run the test project against the upgraded solution and fix any remaining compatibility or runtime issues uncovered by validation. Ensure the test project stays aligned with the upgraded application project.

**Done when**: Tests build and pass successfully against the upgraded solution.