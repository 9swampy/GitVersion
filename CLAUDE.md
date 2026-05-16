# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this repo does

GitVersion calculates Semantic Versions from Git history. It outputs version info as JSON (stdout) and writes `GitVersion_`-prefixed environment variables for build agents.

## Two parallel solution trees

**`src/`** — legacy/stable CLI (`src/GitVersion.slnx`)

| Project | Role |
|---|---|
| `GitVersion.Core` | Core version calculation |
| `GitVersion.Configuration` | Config loading/validation |
| `GitVersion.App` | CLI entry point |
| `GitVersion.BuildAgents` | Build agent adapters (GitHub Actions, Azure Pipelines, etc.) |
| `GitVersion.LibGit2Sharp` | Git repo access |
| `GitVersion.Output` | Output formatting |

**`new-cli/`** — new CLI under active development (`new-cli/GitVersion.slnx`)

Plugin-based architecture: `GitVersion.Calculation`, `GitVersion.Configuration`, `GitVersion.Normalization`, `GitVersion.Output`, `GitVersion.Common`, `GitVersion.Core.Libgit2Sharp`, `GitVersion.Cli.Generator` (source gen for commands).

## Commands

```bash
# src/ (legacy CLI)
dotnet build ./src/GitVersion.slnx
dotnet test ./src/GitVersion.slnx
dotnet test --project ./src/GitVersion.Core.Tests/GitVersion.Core.Tests.csproj
dotnet run --project src/GitVersion.App
dotnet format ./src/GitVersion.slnx
dotnet format --verify-no-changes ./src/GitVersion.slnx   # CI check

# new-cli/
dotnet build ./new-cli/GitVersion.slnx
dotnet test ./new-cli/GitVersion.slnx
dotnet run --project new-cli/GitVersion.Cli
```

## Key conventions

- **Package versions**: edit `src/Directory.Packages.props` (or `new-cli/Directory.Packages.props`), never individual csproj files. Add with `dotnet add package <Package> --version <Version>`.
- **SDK**: .NET 10, pinned in `global.json`. Most projects target `net10.0`.
- **Code style**: `.editorconfig` defines style; `dotnet format` applies it.
- **Config file names**: `GitVersion.yml`, `GitVersion.yaml`, `.GitVersion.yml`, `.GitVersion.yaml`. Logic lives in `src/GitVersion.Configuration/ConfigurationFileLocator.cs`.
- **Build agent outputs**: always use the `GitVersion_` prefix on environment variable names.

## When changing behavior

- **CLI output shape changed** → update `docs/` examples and build-agent adapters in `src/GitVersion.BuildAgents/Agents/`.
- **Configuration schema changed** → regenerate schemas:
  ```bash
  ./build.ps1 -Stage build -Target BuildPrepare
  ./build.ps1 -Stage docs -Target GenerateSchemas
  ```

## Testing

Integration tests live in `src/GitVersion.Core.Tests/IntegrationTests/` — one scenario class per branch type (e.g. `MainScenarios`, `FeatureBranchScenarios`). Use `EmptyRepositoryFixture` / `BaseGitFlowRepositoryFixture` and builder patterns.

```csharp
using var fixture = new EmptyRepositoryFixture();
fixture.Repository.MakeATaggedCommit("1.0.0");
fixture.Repository.CreateBranch("feature/my-feature");
fixture.Checkout("feature/my-feature");  // use fixture.Checkout(), not fixture.Repository.Checkout()
fixture.Repository.MakeACommit();

var configuration = GitFlowConfigurationBuilder.New.Build();
fixture.AssertFullSemver("1.0.1-my-feature.1", configuration);
```
