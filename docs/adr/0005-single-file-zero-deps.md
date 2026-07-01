# ADR-0005 — Executável único, self-contained, zero dependências de UI

**Status:** Aceito

## Contexto
O sistema roda no PC de um voluntário, muitas vezes sem internet e sem permissão de admin.
Instalar .NET, drivers ou bibliotecas no dia da festa é inviável.

## Decisão
Publicar como **single-file, self-contained, win-x64, ReadyToRun**, com o SQLite nativo
embutido. Não usar bibliotecas externas de UI: os gráficos do dashboard são desenhados
à mão em **GDI+** (ver [ADR-0010](0010-graficos-gdi.md)).

## Consequências
- ✅ Um `.exe` que roda em qualquer Windows x64, sem instalar nada.
- ✅ Instalação no perfil do usuário (Inno Setup, `PrivilegesRequired=lowest`).
- ⚠️ Binário maior (~70 MB) — aceitável para o ganho de portabilidade.
