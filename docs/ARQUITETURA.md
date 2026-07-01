# Arquitetura — PDV Festa Junina (Arraiá da AMUV)

> Documento de arquitetura de software. Público-alvo: quem for manter ou evoluir o
> sistema. Para o guia de uso no dia da festa, veja **[MANUAL-OPERADOR.md](MANUAL-OPERADOR.md)**.

---

## 1. Visão geral

Sistema de **Ponto de Venda (PDV)** para eventos, empacotado como um único `.exe`
autossuficiente para Windows. Sem servidor, sem banco externo, sem runtime a instalar.
Feito para operar 12h seguidas num PC modesto, à prova de queda de energia e de
operador inexperiente.

| Atributo | Escolha |
|---|---|
| Plataforma | .NET 8, WinForms (`net8.0-windows`, `win-x64`) |
| Persistência | SQLite (arquivo único) em modo **WAL** |
| Impressão | ESC/POS **RAW** direto no spooler (`winspool.drv`) |
| Empacotamento | Single-file, self-contained, ReadyToRun |
| Dependências externas de UI | **Nenhuma** (gráficos em GDI+ próprio) |
| Testes | xUnit (unitário) + FlaUI (E2E funcional) |

---

## 2. Princípios de design

1. **Core desacoplado da UI.** Toda a regra de negócio vive em `PdvFesta.Core`, sem
   referência a WinForms. Isso a torna 100% testável sem tela nem hardware.
2. **Dinheiro em centavos (`int`).** Nunca `double`/`float` para valores — zero erro
   de arredondamento. Formatação/parse isolados (`CupomFormatter.Moeda`, `Dinheiro`).
3. **Segurança do dado primeiro.** Numa venda, grava-se no SQLite **antes** de imprimir.
   Se a impressora falhar, a venda já está salva e pode ser reimpressa.
4. **Blindagem (idiot-proofing).** Nenhuma falha de hardware ou erro inesperado derruba
   o caixa: impressão nunca lança, exceções globais são capturadas e logadas.
5. **Zero dependências supérfluas.** Um `.exe` que roda em qualquer PC. Até os gráficos
   do dashboard são desenhados à mão em GDI+ para não incluir bibliotecas de chart.
6. **Layout fluido.** UI 100% ancorada (`Dock`/`Anchor`/`TableLayoutPanel`/`FlowLayoutPanel`
   + `AutoScroll`), sem coordenadas absolutas — sobrevive a qualquer resolução/zoom.

---

## 3. Camadas e dependências

```
┌───────────────────────────────────────────────────────────┐
│  PdvFesta.App  (WinForms)                                  │
│  Program, Servico, Forms (Vendas, Pagamento, Fechamento,   │
│  GerenciarProdutos, Categorias, LayoutCupom, ...),         │
│  Dialogos, Dinheiro, GraficoBarras, Marca                  │
└──────────────────────────┬────────────────────────────────┘
                           │  (referencia)
                           ▼
┌───────────────────────────────────────────────────────────┐
│  PdvFesta.Core  (dominio puro, sem WinForms)               │
│  Models, Carrinho, Caixa, Repositorio (SQLite),            │
│  CupomFormatter, EscPosPrinter, ConfigCupom, Log,          │
│  CardapioLoader, Backup*, PrinterDiscovery, AppPaths       │
└───────────────────────────────────────────────────────────┘
```

**Regra de dependência:** `App → Core`. O Core **nunca** referencia o App nem WinForms.
A única exceção pragmática é `EscPosPrinter`/`PrinterDiscovery`, que usam P/Invoke do
Windows (marcados com `[SupportedOSPlatform("windows")]`), mas continuam sem UI.

---

## 4. Catálogo de classes

### 4.1 Core (`src/PdvFesta.Core`)

| Classe | Responsabilidade |
|---|---|
| `Models` | `Produto`, `ItemVenda`, `Venda`, `Categoria`, `Turno`, `MovimentoCaixa`, enums `FormaPagamento`, `StatusCaixa`, `TipoMovimento`. |
| `Carrinho` | Carrinho em andamento; agrupa itens iguais; fecha a venda (snapshot de preço). |
| `Caixa` | Regras puras: troco, consolidação por forma, **Total em Gaveta**, contagem de itens. |
| `Repositorio` | Persistência SQLite (WAL). CRUD de produtos/categorias, turnos, movimentos, config, agregações. Migração suave de schema. |
| `CupomFormatter` | Formatação de texto do cupom (32 col) — recibo completo, ficha de consumo (16 col), Leitura Z. Puro/testável. |
| `EscPosPrinter` | Tradução das linhas em bytes ESC/POS + envio RAW ao spooler. Blindado (nunca lança). |
| `ConfigCupom` | Modelo + persistência do layout do cupom; `LinhaCupom`, `ModoCupom`, `EstiloLinha`. |
| `Log` | Log de arquivo thread-safe em `logs/pdv-AAAAMMDD.log`. |
| `CardapioLoader` | Carrega o `cardapio.json` e faz o seed inicial (produtos + categorias). |
| `Backup*`, `AutoBackupTimer`, `PrinterDiscovery`, `AppPaths`, `ComboSerializer` | Backup/restore, auto-backup, descoberta de impressoras, caminhos, serialização de combos. |

### 4.2 App (`src/PdvFesta.App`)

| Classe | Responsabilidade |
|---|---|
| `Program` | Entry point; handlers globais de exceção; inicializa `Log`; sobe o `Servico` e o `FormVendas`. |
| `Servico` | Fachada de aplicação: amarra `Repositorio` + carrinho + turno + config. Ponto único que as telas usam. |
| `FormVendas` | Tela principal (Menu, StatusStrip, SplitContainer, DataGridView, TabControl, atalhos, refresh dinâmico). |
| `FormPagamento` | Formas de pagamento, troco ao vivo, valores rápidos, anti-crash de impressão. |
| `FormAberturaCaixa` / `FormMovimento` | Abertura de turno; sangria/suprimento. |
| `FormFechamento` | Dashboard do turno, gráficos, Leitura Z, exportação CSV, backup. |
| `FormGerenciarProdutos` / `FormCategorias` | CRUD de produtos (soft delete) e de categorias (ordem/ativo). |
| `FormLayoutCupom` | Editor do layout do cupom com **preview ao vivo**. |
| `FormCustomizarAtalhos` | Mapeia teclas 1–9 → produtos. |
| `FormPrinterConfig` / `FormSobre` / `FormSenhaAdmin` | Impressora; sobre; prompt de senha admin. |
| `Dialogos` | Abre telas modais como **singleton** com `Dispose` garantido; trava de admin. |
| `Dinheiro` | Parse robusto texto → centavos e formatação. |
| `GraficoBarras` | Gráfico de barras horizontais em GDI+ (escala com o tamanho = zoom). |
| `Marca` | Identidade visual AMUV (cores, ícone, logo). |

---

## 5. Modelo de dados (SQLite)

```
produtos(id PK, nome, preco_cent, categoria, atalho, ativo, composicao)
categorias(nome PK, ordem, ativo)
vendas(id PK, data_hora, total_cent, forma, recebido_cent, troco_cent, operador, caixa_id → caixa.id)
venda_itens(id PK, venda_id → vendas.id, produto_id, nome, preco_unit_cent, quantidade)
caixa(id PK, abertura, fechamento, fundo_cent, operador, status)          -- turno
caixa_mov(id PK, caixa_id → caixa.id, tipo, valor_cent, motivo, data_hora)-- sangria/suprimento
config(chave PK, valor)                                                    -- key/value
```

- **Preços/valores** sempre em **centavos** (`INTEGER`).
- `produtos.categoria` referencia `categorias.nome` **por nome** (ver [ADR-0008](adr/0008-categoria-por-nome.md)).
- `vendas.caixa_id` vincula a venda ao turno; **nullable** para vendas legadas.
- **Migração suave:** bancos antigos ganham `vendas.caixa_id` via `ALTER TABLE` e as
  categorias são semeadas se a tabela estiver vazia — sem quebrar dados existentes.

---

## 6. Fluxos principais

### 6.1 Venda
```
Operador clica/atalho → Carrinho.Adicionar → DataGridView atualiza + TOTAL
F2 → FormPagamento (exige caixa aberto) → escolhe forma/troco → Confirmar
   → Servico.FinalizarVenda:
        1) Carrinho.FecharVenda (snapshot, vincula caixa_id)
        2) Repositorio.SalvarVenda   (GRAVA PRIMEIRO)
        3) Log.Info(venda)
        4) EscPosPrinter.ImprimirTicket (se falhar → Repetir/Ignorar; venda já salva)
```

### 6.2 Turno de caixa
```
Startup sem caixa → FormAberturaCaixa (fundo inicial) → Servico.AbrirCaixa
... vendas vinculadas ao turno ...
Sangria/Suprimento → caixa_mov
F9 Fechamento → dashboard (Total em Gaveta = fundo + dinheiro + suprimentos − sangrias)
   → imprime Leitura Z (itens vendidos p/ auditoria) → Servico.FecharCaixa
```

### 6.3 Impressão (ESC/POS)
```
CupomFormatter.MontarTicket(venda, ConfigCupom) → List<LinhaCupom> (texto + estilo)
EscPosPrinter.MontarBytes → aplica ESC/POS por estilo (titulo/expandida/corte) → EnviarRaw
Modo Completo: recibo com valores.  Modo Ficha: só qtd+nome em fonte dupla (16 col), corte por item.
```

### 6.4 Refresh dinâmico do catálogo
```
Fecha FormGerenciarProdutos/FormCategorias → FormVendas.RecarregarProdutos()
   → relê produtos ativos + ordem das categorias → recria abas/botões na hora
```

---

## 7. Pontos de extensão

- **Novo meio de pagamento:** acrescente ao enum `FormaPagamento` (valores estáveis, nunca
  reordene), trate em `Caixa.Consolidar` e `FormPagamento`.
- **Novo modo de cupom:** acrescente ao enum `ModoCupom` e trate em `CupomFormatter.MontarTicket`.
- **Novo relatório impresso:** adicione um `Montar*` em `CupomFormatter` retornando `List<LinhaCupom>`.
- **Nova tela:** abra sempre via `Dialogos.Modal<T>(...)` (singleton + Dispose).

---

## 8. Testes

- **Unitários (xUnit)** — `tests/PdvFesta.Tests`: Core puro (troco, consolidação, turno,
  cupom nos 2 modos, soft delete, categorias, persistência WAL, migração). Rodam no CI.
- **E2E funcional (FlaUI)** — `tests/PdvFesta.E2E`: dirige o `.exe` real (teclado/mouse),
  valida a venda no SQLite. Roda **local** (precisa de desktop), não no CI headless.

---

## 9. Build, empacotamento e CI

- `dotnet build` / `dotnet test` — ver **[BUILD.md](BUILD.md)**.
- Publicação single-file self-contained (win-x64) + Inno Setup → `Setup_PDVFestaJunina.exe`.
- CI/CD: `.github/workflows/release.yml` roda testes e publica na Release a cada push na `main`.

> Nota: a *solution* usa o formato novo `.slnx`. SDKs 8.0.4xx podem não compilá-la via
> `dotnet build PdvFesta.slnx`; compile os projetos diretamente (o CI usa SDK compatível).

---

## 10. Diagnóstico

Logs em `%AppData%\FestaJuninaPDV\logs\pdv-AAAAMMDD.log`. Ver **[DIAGNOSTICO.md](DIAGNOSTICO.md)**.
