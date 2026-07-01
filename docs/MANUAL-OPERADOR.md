# Manual do Operador — PDV Festa Junina (Arraiá da AMUV)

Guia rápido para quem vai operar o caixa no dia da festa. Não precisa saber de computador.

---

## 1. Começando o dia

1. Dê dois cliques no atalho **PDV Festa Junina** na Área de Trabalho.
2. Na primeira tela, informe o **Troco Inicial** (o dinheiro que já está na gaveta) e
   clique **ABRIR CAIXA**. Pode deixar `0,00` se não houver troco inicial.
   > Sem abrir o caixa, o sistema **não deixa vender**.
3. A barra de baixo mostra o estado: **Caixa: ABERTO**, **BD: Conectado**, e a **Impressora**.

## 2. Configurar a impressora (só na primeira vez)

1. Ligue e conecte a impressora térmica **antes**.
2. Aperte **F12** → escolha a impressora na lista (ex: `MPT-II 58mm`) → **Salvar** →
   **Imprimir Teste** (deve sair um cupom "Status OK"). Ela fica lembrada para sempre.

## 3. Vendendo (rápido)

- Clique na **aba** da categoria (Comidas, Bebidas, Bingo...) ou na aba **Todos**.
- Clique no produto para adicioná-lo ao carrinho (à direita). O **TOTAL** aparece gigante.
- Mais rápido: use os **números 1 a 9** do teclado (o número aparece no canto do botão).
- Errou? Selecione a linha no carrinho e aperte **Delete**. Para limpar tudo: **Esc**.

### Pagamento
- Aperte **F2** (ou o botão verde **PAGAR**).
- Escolha a forma: **D** = Dinheiro, **P** = Pix, **B** = Débito, **C** = Crédito.
  - **Dinheiro:** digite quanto o cliente deu; o **TROCO** aparece na hora. Há botões de
    valor rápido (Exato, R$ 20, R$ 50, R$ 100).
  - **Pix/Cartão:** o valor já vem preenchido; é só confirmar.
- **Enter** confirma. O cupom sai na impressora.

> **Se a impressora falhar** (sem papel, cabo solto): não se preocupe — **a venda já foi
> registrada**. O sistema pergunta se quer **Repetir** a impressão ou **Ignorar**.

## 4. Atalhos do teclado

| Tecla | Ação |
|-------|------|
| `1`–`9` | Adiciona o produto do atalho |
| `F2` / `Enter` | Ir para o pagamento |
| `Esc` | Limpar o carrinho |
| `Delete` | Remover o item selecionado do carrinho |
| `F8` | Backup / Restauração (pede senha) |
| `F9` | Fechamento de caixa (pede senha) |
| `F12` | Configurar impressora (pede senha) |

## 4.1 Corrigir pedido e cancelar venda

- **Tirou um item errado do carrinho?** Clique nele na lista e aperte **Delete** (ou
  **Backspace**), ou use o botão laranja **Remover Item Selecionado**. Para limpar tudo: **Esc**.
- **Cliente desistiu depois de pagar?** Aperte **F3** (Histórico de Vendas) → selecione a
  venda → **Cancelar/Estornar** → digite a **senha de admin**. 
  > ⚠️ O dinheiro/estorno no cartão é devolvido **na mão**; o sistema só registra o
  > cancelamento. A venda cancelada some dos totais (a gaveta continua batendo).

## 5. Durante a festa (tesoureiro)

Telas protegidas por **senha de administrador** (padrão: **0000**).

- **Sangria** (menu Ferramentas): tirou dinheiro da gaveta por segurança? Registre.
- **Suprimento** (menu Ferramentas): colocou troco na gaveta? Registre.
- **Acabou um produto?** Configurações → **Gerenciar Produtos** → selecione → **Inativar**.
  Ele some da tela do caixa na hora (o histórico é preservado).
- **Mudou um preço?** Mesma tela: selecione, ajuste o preço, **Salvar**. O próximo clique
  já usa o valor novo.
- **Reorganizar abas?** Configurações → **Gerenciar Categorias** (ordem e ativar/ocultar).

## 6. Fechando o caixa (fim da noite)

1. Aperte **F9** (ou menu Arquivo → Fechamento). Digite a senha.
2. Confira o **RESUMO**: Dinheiro, Pix, Débito, Crédito e o **TOTAL EM GAVETA**
   (é quanto de dinheiro físico deve haver na gaveta).
3. Aba **Gráficos**: veja vendas por pagamento e itens mais vendidos.
4. **Exportar Excel (CSV)** — salva uma planilha na Área de Trabalho para conferir depois.
5. **FECHAR CAIXA** — imprime a **Leitura Z** (relatório com os itens vendidos) e encerra o
   turno. Para vender de novo, é preciso abrir um novo caixa.

## 7. Layout do cupom (economizar papel)

Em **Configurações → Layout do Cupom** você escolhe:
- **Recibo Completo** (com valores) ou **Ficha de Consumo** (só o produto, em letra grande,
  para a barraca ler rápido e gastar menos papel).
- **Cortar/separar por item** (quando itens são de barracas diferentes).
- O **preview** mostra como vai sair antes de imprimir.

## 8. Segurança dos dados (backup)

- Aperte **F8**. Aponte uma **pasta** do OneDrive/Drive e um **intervalo** de auto-backup.
- **Gerar Backup Agora** cria um `.zip`. **Restaurar Backup** recupera em outro PC — o caixa
  **continua de onde parou**.

## 9. Problemas comuns

| Problema | O que fazer |
|----------|-------------|
| Cupom não sai | F12 → confira a impressora → Imprimir Teste (a venda já ficou salva) |
| Impressora sumiu da lista | Verifique cabo USB / se está ligada → F12 → Atualizar |
| "O caixa está fechado" ao pagar | Menu **Arquivo → Abrir Caixa** |
| PC travou/desligou | Reabra o programa — nenhuma venda é perdida (banco seguro) |
| Precisa mudar de PC | F8 no PC velho → Backup; no PC novo → F8 → Restaurar |
| Esqueci a senha de admin | Padrão de fábrica: **0000** |

---

**Bom Arraiá! 🌽🔥** — Qualquer dúvida, chame o responsável pelo sistema.
