# dotnet-cicd-azure-cheatsheet

[![CI](https://github.com/illia-tereshchuk/dotnet-cicd-azure-cheatsheet/actions/workflows/ci.yml/badge.svg)](https://github.com/illia-tereshchuk/dotnet-cicd-azure-cheatsheet/actions/workflows/ci.yml)

**ASP.NET Core Web API** wired to a full **CI/CD pipeline** with **GitHub Actions**.

Deployed to **Azure App Service** — staging on PR, production on merge.

The API exposes `GET /weatherforecast` (root `/` returns 404 by design).

| Trigger | build/test | staging | production |
|---------|:---:|:---:|:---:|
| push to any branch | ✅ | — | — |
| PR → main | ✅ | ✅ | — |
| merge (push) → main | ✅ | — | ✅ |

## How CI/CD works here
- **Triggers** — `push` (any branch → tests) and `pull_request` to `main`.
- **Jobs** — `build`, then `deploy-staging` (on PR) and `deploy-prod` (on merge).
- **Environments** — `staging` and `production` (GitHub Settings → Environments); can add required reviewers, etc.
- **Secrets** — publish profiles stored as `STAGE` / `DEPLOY` (never hard-coded); `permissions: contents: read` for `GITHUB_TOKEN`.

**Core concepts**

| Term | Meaning |
|------|---------|
| CI | Every change is auto-built and tested → catch bugs early |
| CD | Ready to release, human clicks / auto to prod |
| workflow · job · step | YAML file · steps on one runner (jobs parallel) · a step (`uses`/`run`) |
| runner · action · event | the VM · reusable step · what triggers (`on:`) |

**.NET commands in CI**

```bash
dotnet restore                                     # pull NuGet packages
dotnet build   --no-restore --configuration Release
dotnet test    --no-build   --configuration Release   # non-zero exit = red job
dotnet publish src/Api/Api.csproj --no-build -c Release -o ./publish
```

**Key snippets**

```yaml
# NuGet cache (speed)
- uses: actions/cache@v4
  with: { path: ~/.nuget/packages, key: ${{ runner.os }}-nuget-${{ hashFiles('**/*.csproj') }} }

# Pass build output to deploy
- uses: actions/upload-artifact@v4    # in build
- uses: actions/download-artifact@v4  # in deploy (needs: build)

# Least privilege + concurrency
permissions: { contents: read }
concurrency: { group: ${{ github.workflow }}-${{ github.ref }}, cancel-in-progress: true }
```
> `cancel-in-progress: true` for CI on branches (fast feedback on the latest commit); `false` for deploy (don't interrupt a rollout).

**Security**
- Never hard-code secrets → GitHub Secrets + `${{ secrets.NAME }}` (masked, write-only).
- Prefer **OIDC** over long-lived cloud secrets.
- `NU1903` (vulnerable package) → override the transitive dep with a direct `PackageReference`:
  ```xml
  <PackageReference Include="Microsoft.OpenApi" Version="2.11.0" />
  ```
  Check with `dotnet list package --vulnerable --include-transitive`.

**Azure App Service gotchas**
- Enable **Basic authentication = On**, else publish-profile deploy fails to log in.
- Keep `app.UseHttpsRedirection()` **Development-only** (behind the reverse proxy it causes a redirect loop); enforce HTTPS with the **HTTPS Only** toggle.
- The real hostname is Azure's **unique default hostname** (`<app>-<token>.<region>-01.azurewebsites.net`) — copy it from **Overview → Default domain**.

**Handy git / gh**

```bash
git push -u origin main                # set upstream, then just `git push`
gh secret list                         # names only (values are write-only)
gh pr checkout <n>                     # test someone's PR locally (git stash first)
```
