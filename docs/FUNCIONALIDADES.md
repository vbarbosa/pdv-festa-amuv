# Catálogo de Funcionalidades — PDV Festa Junina

Lista das funcionalidades do produto, com critérios de aceite e onde vivem no código.

---

## 1. Venda (frente de caixa)
- Catálogo em **abas por categoria** (TabControl) + aba **"Todos"** com tudo agrupado.
- Rolagem horizontal de abas quando há muitas; scroll vertical dentro de cada aba.
- Botões de produto sóbrios (cores pastel por categoria) com **atalho no canto** (badge).
- Carrinho em **DataGridView** (Qtd | Descrição | V. Unit | Subtotal), **TOTAL gigante**.
- Adição por **clique** ou **atalho numérico 1–9** (global, independe da aba visível).
- `Delete` remove a linha selecionada; `Esc`/CANCELAR limpa o carrinho.
- **Critério de aceite:** clicar/atalho soma ao carrinho; total confere; carrinho nunca colapsa (≥420px).
- **Código:** `FormVendas`, `Carrinho`.

## 2. Pagamento
- Formas: **Dinheiro, Pix, Débito, Crédito** (atalhos D/P/B/C).
- Dinheiro: cálculo de **troco ao vivo** + botões de **valor rápido** (Exato, R$20/50/100).
- Cartão/Pix: valor **pré-preenchido** com o total (troco zero).
- **Anti-crash de impressão:** se a impressora falhar, a venda já está salva e o sistema
  oferece **Repetir/Ignorar**.
- **Código:** `FormPagamento`, `Servico.FinalizarVenda`.

## 2.1 Correção de pedido e estorno (audit trail)
- **Pré-venda**: remover item do carrinho por **Delete/Backspace** ou botão **"Remover Item
  Selecionado"**; total recalcula na hora. `Esc` limpa tudo.
- **Pós-venda**: **Histórico de Vendas** (`F3`/menu) lista as vendas do turno; **Cancelar/
  Estornar** exige **senha de admin** e avisa que o estorno físico (maquininha/dinheiro) é
  manual.
- **Soft delete**: a venda vira `Cancelada` (nunca é apagada). Canceladas saem dos totais,
  do **Total em Gaveta** e da **Leitura Z** → conciliação exata.
- **Código:** `FormHistoricoVendas`, `Repositorio.CancelarVenda`, `Caixa.Consolidar/ContarItens`.

## 2.2 Motor de promoções/combos (auto-detecção)
- Ao adicionar/remover itens, o carrinho **auto-detecta** combos/promoções ativas
  (`PricingEngine`) e aplica o desconto por **conjunto completo**, dentro da janela de horário.
- Mostra uma **linha verde** `* DESCONTO: <combo>` (não substitui os itens); some ao remover item.
- **Gestão** (`FormPromocoes`): preço especial ou combo, horários (DateTimePicker), liga/desliga,
  itens exigidos, soft delete.
- **Seed do cartaz**: combos "até 20h" (Refri+Bolo, Refri+Pinhão, 2 Refri = R$ 10).
- **Eco-print**: cupom só imprime Recebido/Troco no Dinheiro (economia de bobina no Pix/cartão).
- **Código:** `Promocao`, `PricingEngine`, `Carrinho`, `FormPromocoes`, `CupomFormatter`.

## 3. Turno de caixa
- **Abertura** com fundo de caixa (troco inicial). Sem caixa aberto, não vende.
- **Sangria** (retirada) e **Suprimento** (entrada) de dinheiro.
- **Fechamento**: dashboard em tempo real, **Total em Gaveta**, **gráficos**, **Leitura Z**
  impressa (itens vendidos p/ auditoria) e **exportação CSV**.
- **Código:** `FormAberturaCaixa`, `FormMovimento`, `FormFechamento`, `Caixa`, `Repositorio`.

## 4. Catálogo (gestão)
- **CRUD de Produtos**: nome, preço (parse monetário rigoroso), categoria, atalho, ativo.
  **Soft delete** (inativar). Refresh **dinâmico** da tela de vendas ao fechar.
- **CRUD de Categorias**: nome, **ordem de exibição**, ativo (oculta a aba).
- **Customizar Atalhos**: mapeia teclas 1–9 → produtos (auto-sugestão, editável).
- **Código:** `FormGerenciarProdutos`, `FormCategorias`, `FormCustomizarAtalhos`.

## 5. Layout do cupom (impressão térmica 58mm)
- Modos **Recibo Completo** e **Ficha de Consumo** (fonte expandida, sem valores).
- Cabeçalho (evento/subtítulo), rodapé, **separar ficha por item**.
- **Preview ao vivo** em fonte mono simulando a bobina de 32 colunas.
- **Código:** `FormLayoutCupom`, `CupomFormatter`, `EscPosPrinter`, `ConfigCupom`.

## 6. Segurança e estabilidade
- **Senha de administrador** (padrão `0000`) protege telas sensíveis (config, produtos,
  fechamento, backup, sangria/suprimento).
- Telas secundárias **modais + singleton + Dispose** (sem vazamento de memória em 12h).
- **Blindagem global** de exceções + **logs** em arquivo.
- **Código:** `FormSenhaAdmin`, `Dialogos`, `Program`, `Log`.

## 7. Segurança de dados (disaster recovery)
- **Backup** manual (`.json`/`.zip`) e **auto-backup** em background (intervalo configurável).
- **Restauração** em 2 cliques (troca de PC no meio da festa).
- SQLite **WAL** (resiliente a queda de energia).
- **Código:** `FormBackup`, `BackupService`, `BackupManager`, `AutoBackupTimer`.

## 8. Impressora
- Descoberta de impressoras e portas COM; salvar padrão; **imprimir teste**.
- **Código:** `FormPrinterConfig`, `PrinterDiscovery`, `EscPosPrinter`.

## 9. Carga inicial (seed)
- Primeira execução semeia o cardápio (`cardapio.json`) + categorias na ordem do cartaz.
- **Código:** `CardapioLoader`, `Repositorio.SemearCategoriasSeVazio`.
