# Guia de Build e Empacotamento

## Pré-requisitos

- **.NET SDK 8+** (`dotnet --version`)
- **Inno Setup 6** (opcional, só para gerar o `Setup.exe`) — https://jrsoftware.org/isdl.php

## Rodar os testes

```powershell
# Testes unitarios (rapidos, headless) - os mesmos que rodam no CI
dotnet test tests/PdvFesta.Tests/PdvFesta.Tests.csproj

# Testes E2E funcionais (FlaUI) - abrem a janela real, rode LOCALMENTE
dotnet test tests/PdvFesta.E2E/PdvFesta.E2E.csproj
```

> Os E2E abrem a UII de verdade (mouse/teclado). Não rode em background nem no CI headless.

## Rodar o app em desenvolvimento

```powershell
dotnet run --project src/PdvFesta.App
```

## Gerar a release localmente

```powershell
# Gera: release/PDV-Festa/PDV-Festa-AMUV.exe (single-file),
#       release/Setup_PDVFestaJunina.exe (se Inno Setup instalado),
#       release/FestaJuninaPDV_Release_v1.0.zip
pwsh build/build-release.ps1 -Versao 1.0

# Só o zip portable, sem o instalador:
pwsh build/build-release.ps1 -SemInstalador
```

## Como o `.exe` fica autossuficiente

No `PdvFesta.App.csproj`:

- `SelfContained=true` + `PublishSingleFile=true` → um único `.exe` que **não precisa**
  do .NET instalado na máquina de destino.
- `IncludeNativeLibrariesForSelfExtract=true` → embute o binário nativo do **SQLite**
  (`e_sqlite3`) dentro do `.exe`.
- `PublishReadyToRun=true` → inicia mais rápido em PCs fracos.

## CI/CD (GitHub Actions)

`.github/workflows/release.yml` roda **a cada push na `main`**:

1. Restore + **testes unitários** (aborta se algum falhar).
2. `dotnet publish` single-file self-contained (win-x64).
3. Copia cardápio, logo e driver.
4. Instala o Inno Setup no runner e compila o `Setup.exe`.
5. Zipa o portable.
6. Publica tudo na **Release `latest`** (baixável pela aba Releases).

Para forçar sem push: aba **Actions → Build e Release → Run workflow**.
