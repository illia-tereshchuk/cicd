# CI/CD для .NET + GitHub Actions — Cheatsheet

Шпаргалка за курсом: від нуля до реального деплою .NET Web API на Azure.

---

## 1. Концепції

| Термін | Суть |
|--------|------|
| **CI** (Continuous Integration) | Кожна зміна коду автоматично збирається й тестується. Мета — ловити помилки рано. |
| **CD — Continuous Delivery** | Реліз завжди готовий; фінальний деплой запускає людина (кнопка / apruv). |
| **CD — Continuous Deployment** | Кожна зелена зміна деплоїться в прод **автоматично**. |
| **Pipeline** | Ланцюг кроків: `build → test → publish → deploy`. Впав крок — конвеєр стоп. |

## 2. Словник GitHub Actions

| Термін | Що це |
|--------|-------|
| **workflow** | YAML-файл у `.github/workflows/` |
| **event / trigger** (`on:`) | що запускає (push, pull_request, workflow_dispatch, schedule) |
| **job** | набір кроків на одному runner; jobs **паралельні** за замовчуванням |
| **runner** | чиста ВМ (`ubuntu-latest`), де все виконується |
| **step** | окремий крок; `uses:` (готова action) або `run:` (команда) |
| **action** | перевикористовуваний блок (`actions/checkout`) |

## 3. Анатомія workflow

```yaml
name: CI                      # назва в UI (косметика)

on:                           # ТРИГЕРИ
  push:
    branches: [ main ]
  pull_request:               # фільтр = БАЗОВА гілка PR (куди зливаємо)
    branches: [ main ]

permissions:                  # least privilege для GITHUB_TOKEN
  contents: read

jobs:
  build:                      # id job-а (вигадуєш сам)
    runs-on: ubuntu-latest    # runner
    steps:
      - uses: actions/checkout@v4        # @v4 = пін версії action
      - uses: actions/setup-dotnet@v4
        with:                            # with = входи (inputs) action
          dotnet-version: '10.0.x'
      - run: dotnet build                # run = shell-команда
```

Ключове:
- **jobs — паралельні**, **steps — послідовні** (спільна файлова система).
- `needs: build` → зробити job залежним (послідовність між jobs).
- `if: ...` → умова запуску job/step.
- YAML тримається на **відступах пробілами** (таби заборонені).

## 4. Команди .NET у CI

| Команда | Що робить |
|---------|-----------|
| `dotnet restore` | тягне NuGet-пакети → `~/.nuget/packages` |
| `dotnet build` | компілює у `bin/` (сам робить restore, якщо не `--no-restore`) |
| `dotnet test` | збирає + ганяє тести (сам робить build, якщо не `--no-build`) |
| `dotnet publish` | готує папку до розгортання (`-o ./publish`) |

Прапорці «не роби двічі»: `--no-restore`, `--no-build`. Конфігурація: `-c Release` (оптимізована, «як у прод»).

```bash
dotnet restore
dotnet build   --no-restore --configuration Release
dotnet test    --no-build   --configuration Release
dotnet publish src/Api/Api.csproj --no-build -c Release -o ./publish
```

## 5. Кешування NuGet (пришвидшує CI)

```yaml
- name: Cache NuGet packages
  uses: actions/cache@v4
  with:
    path: ~/.nuget/packages
    key: ${{ runner.os }}-nuget-${{ hashFiles('**/*.csproj') }}
    restore-keys: |
      ${{ runner.os }}-nuget-
```
`key` через `hashFiles(...)` = «відбиток» залежностей: не змінились → cache hit.

## 6. Тести (xUnit)

```bash
dotnet new xunit -o tests/Api.Tests
dotnet add tests/Api.Tests/Api.Tests.csproj reference src/Api/Api.csproj
dotnet sln LearnCicd.slnx add tests/Api.Tests/Api.Tests.csproj
```
- `[Fact]` — один тест-кейс; `[Theory]` + `[InlineData]` — той самий тест на кількох даних.
- Провалений тест → ненульовий exit code → **червоний job**.
- Тестований тип має бути `public` (інакше тестовий проєкт його не бачить).

**Matrix** — один job у кількох комбінаціях паралельно:
```yaml
strategy:
  matrix:
    os: [ubuntu-latest, windows-latest]
    dotnet: ['9.0.x', '10.0.x']         # 2×2 = 4 паралельні job-и
runs-on: ${{ matrix.os }}
```

## 7. Артефакти

Правило: **build once, deploy the same** — деплой саме перевіреного бінарника.

```yaml
- uses: actions/upload-artifact@v4      # у job build
  with: { name: app, path: ./publish, retention-days: 7 }

- uses: actions/download-artifact@v4    # у job deploy (needs: build)
  with: { name: app, path: ./publish }
```
Артефакт ≠ кеш: артефакт — свідомий **результат**; кеш — прозора **оптимізація швидкості**.

## 8. CD / Деплой

- **Секрети:** ніколи не хардкодь. GitHub → Settings → Secrets and variables → Actions. У yaml: `${{ secrets.NAME }}` (маскуються в логах).
- **Environment** (`production`): іменована ціль із власними секретами й правилами (required reviewers = ручний апрув = Continuous Delivery).
- **GITHUB_TOKEN:** авто-токен прогону; права звужуй блоком `permissions:`.
- **OIDC:** best practice для хмари — короткоживучі токени замість вічних секретів.
- **Ручний запуск:** тригер `workflow_dispatch` (кнопка «Run workflow»).

## 9. Best practices

- **Branch protection** на `main`: require PR + **required status checks** (CI має пройти) + review. Робить CI обов'язковим бар'єром.
- Працюй через **Pull Request**, не push прямо в `main`.
- **Пінь версії actions** (`@v4`) + увімкни **Dependabot** (авто-PR на оновлення й патчі).
- **`concurrency`** — скасовувати застарілі прогони:
  ```yaml
  concurrency: { group: ${{ github.workflow }}-${{ github.ref }}, cancel-in-progress: true }
  ```
  (для деплою → `cancel-in-progress: false`).
- **`timeout-minutes`** на job — щоб завислий крок не палив хвилини.
- **Вразливі пакети:** `NU1903` → перебий транзитивну залежність прямим `PackageReference` на пропатчену версію:
  ```xml
  <PackageReference Include="Microsoft.OpenApi" Version="2.11.0" />
  ```
  Перевірка: `dotnet list package --vulnerable --include-transitive`.

## 10. Деплой на Azure App Service (publish profile)

**Портал (клік-by-клік):**
1. App Services → **+ Create → Web App**: Code, Linux, .NET, регіон, **Free F1**.
2. Settings → Configuration → General → **Basic authentication = On** (інакше publish profile не залогіниться).
3. Overview → **Download publish profile** (файл `.PublishSettings`).
4. GitHub → Settings → Secrets → Actions → New secret `AZURE_WEBAPP_PUBLISH_PROFILE` = вміст файлу.

**Крок у workflow:**
```yaml
- uses: azure/webapps-deploy@v3
  with:
    app-name: learncicd-illia-tereshchuk-001
    publish-profile: ${{ secrets.AZURE_WEBAPP_PUBLISH_PROFILE }}
    package: ./publish
```

⚠️ **Граблі:**
- За реверс-проксі App Service `app.UseHttpsRedirection()` дає цикл редіректів → тримай його лише для Development, а HTTPS вмикай тумблером **HTTPS Only** у порталі.
- Порожній корінь `/` віддає 404 — тестуй конкретний ендпоінт (`/weatherforecast`).
- **Реальний URL бери з порталу, не вигадуй.** Azure тепер дає **унікальне доменне ім'я** з випадковим токеном + регіоном (захист від subdomain takeover), а не просте `<app>.azurewebsites.net`. Шукай його в **Overview → Default domain**. Формат: `https://<app>-<токен>.<регіон>-01.azurewebsites.net`.

  Живий приклад цього проєкту:
  ```
  https://learncicd-illia-tereshchuk-001-fqa4fqefapgwbuac.polandcentral-01.azurewebsites.net/weatherforecast
  ```

## 11. Корисні команди

```bash
# git / gh
git remote add origin https://github.com/<user>/<repo>.git
git push -u origin main
gh secret set NAME < file.txt         # додати секрет із файлу

# .NET діагностика
dotnet list package --vulnerable --include-transitive
dotnet list package --outdated
```

---

*Курс CI/CD для .NET · зроблено крок за кроком на власному репозиторії.*
