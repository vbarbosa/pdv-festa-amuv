# 🎪 PDV Festa Junina — Arraiá da AMUV

Sistema de **Ponto de Venda (PDV)** portátil para festa junina, feito para rodar em
qualquer PC Windows **sem instalar nada** (sem Node, sem Python, sem banco externo).
Um único `.exe` autossuficiente, com impressão de cupom em impressora térmica de 58mm.

[![Build e Release](../../actions/workflows/release.yml/badge.svg)](../../actions/workflows/release.yml)

---

## ⬇️ Baixar e usar (para o caixa da festa)

1. Vá em **[Releases](../../releases)**.
2. Baixe **`Setup_PDVFestaJunina.exe`** (instalador) ou **`FestaJuninaPDV_Release.zip`** (portable).
3. **Instalador:** dê dois cliques, avance e pronto — cria atalho na Área de Trabalho.
   **Portable:** extraia o zip e rode `PDV-Festa-AMUV.exe`.
4. Configure a impressora com **F12** (veja o [Manual do Operador](docs/MANUAL-OPERADOR.md)).

> O banco de dados fica em `%AppData%\FestaJuninaPDV` e **sobrevive a reinstalação**.

---

## ⌨️ Atalhos (operação sem mouse)

| Tecla | Ação |
|-------|------|
| `1`–`9` | Adiciona o produto do atalho (Quentão, Refri, Bingo...) |
| `F2` / `Enter` | Ir para o pagamento |
| `Esc` | Limpar o carrinho / cancelar |
| `F8` | Segurança de dados (backup / restaurar) |
| `F9` | Fechamento de caixa (dashboard do tesoureiro) |
| `F12` | Configuração da impressora |
| Na tela de pagamento: `D`/`P`/`C` | Dinheiro / Pix / Cartão |

---

## 🛡️ Resiliência (à prova de queda de energia)

- **SQLite com WAL** — uma queda de energia no meio de uma venda **não corrompe** o histórico.
- **Backup automático** em background (intervalo configurável) para uma pasta secundária (ex: OneDrive).
- **Backup manual** em `.zip` e **restauração** em 2 cliques (troca de PC no meio da festa).
- **Fallback de caminho** — se `%AppData%` não for gravável (pen drive), usa a pasta do `.exe`.

---

## 🧾 Impressão térmica 58mm

Cupom não-fiscal impresso via **ESC/POS RAW** direto no spooler (sem janela de diálogo,
sem margens desconfiguradas). Layout fixo de **32 colunas**. Suporta a impressora **MPT-II**
e similares. Um pacote de instalação do driver acompanha a Release.

---

## 🏗️ Arquitetura

```
src/
  PdvFesta.Core/   -> logica de negocio pura + SQLite + ESC/POS (testavel)
  PdvFesta.App/    -> UI WinForms (telas, atalhos, marca AMUV)
tests/
  PdvFesta.Tests/  -> testes unitarios (xUnit) - TDD, rodam no CI
  PdvFesta.E2E/    -> testes funcionais (FlaUI) - rodam LOCAL
build/             -> build-release.ps1 (empacotamento)
installer/         -> PDVFesta.iss (instalador Inno Setup)
.github/workflows/ -> release.yml (CI/CD -> GitHub Releases)
```

**Princípios:** TDD (lógica testada antes da UI), preços em **centavos** (int, sem erro de
arredondamento), Core desacoplado da UI.

---

## 👩‍💻 Desenvolvimento

```bash
dotnet test tests/PdvFesta.Tests/PdvFesta.Tests.csproj   # testes unitarios
dotnet run --project src/PdvFesta.App                    # rodar o app
pwsh build/build-release.ps1 -Versao 1.0                 # gerar release local
```

Detalhes de compilação e empacotamento: **[docs/BUILD.md](docs/BUILD.md)**.

---

## 📄 Licença

[MIT](LICENSE) — uso livre. Feito com carinho para o **Arraiá da AMUV**. 🌽🔥
