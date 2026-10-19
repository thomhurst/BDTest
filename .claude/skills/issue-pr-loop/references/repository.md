# BDTest

BDTest has no root agent instructions file. Follow `.editorconfig`, `Directory.Build.props`, and the conventions of the surrounding code.

- Libraries (`BDTest`, `BDTest.NUnit`, `BDTest.xUnit`, `BDTest.MSTest`, and other framework adapters) target `netstandard2.0` and ship as NuGet packages. Keep that target framework and treat their public types as a shipped contract. Example, report-server, pipeline, and test projects have no compatibility contract.
- Build with `dotnet build BDTest.sln -c Release`. Run each test project that CI runs: every `*.csproj` whose path contains `Tests`, through `dotnet test <project> -c Release`. TUnit test projects and `BDTest.xUnit.Tests` run on Microsoft.Testing.Platform, so run them with `dotnet run --project <project> -c Release` instead.
- CI is the [.NET workflow](../../../../.github/workflows/dotnet.yml): it runs `dotnet run -c Release` in `BDTest.Pipeline` (a ModularPipelines pipeline) that runs the tests, then packs the packages. Pull requests do not publish packages. Do not run the pipeline locally in `Production`, because that environment uploads packages to NuGet.
- Add a regression test in the test project of the framework the change affects.
- PRs labelled `dependencies` (Renovate/Dependabot) enable auto-merge through the [dependencies auto-merge workflow](../../../../.github/workflows/dependencies-automerge.yml). Do not merge them with `Merge-Pr.ps1`; fix their CI failures or close a superseded one and link the replacement.
- No Aspire AppHost or other services. Docker serves only the shared lock Redis; never stop it.
