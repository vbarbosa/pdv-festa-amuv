# Changelog

Formato baseado em [Keep a Changelog](https://keepachangelog.com/pt-BR/).
Versionamento [SemVer](https://semver.org/lang/pt-BR/).

## [2.4.1] — Central de Impressora, modos de vale e blindagem de impressão

### Adicionado
- **Novo modo de cupom "Só Vales destacáveis"**: imprime apenas as fichas (sem o recibo
  gerencial antes), com um **mini-cabeçalho da festa em cada ficha** (nome do evento + nº da
  venda + data) para a ficha se identificar sozinha na barraca. Economiza papel.
- **Coluna "Impressões"** no Histórico de Vendas: mostra quantas vezes cada nota foi impressa
  (1ª via + reimpressões). Reimpressas (2x+) aparecem em destaque; nunca impressas em cinza.

### Alterado
- **Ficha de Consumo** agora sai **1 ficha por unidade** (não agrupa "3X"): cada unidade é uma
  ficha destacável, entregável em barracas diferentes. Cabeçalho melhorado (nome da festa,
  subtítulo/nº da venda e data) — a ficha deixa de ser só o item solto no topo.
- **Impressão blindada contra travar o caixa**: teto de tempo de 15s (impressora que não
  responde não congela a venda — a nota é salva e pode ser reimpressa), e limpeza automática
  de job agarrado no spooler (reinicia o serviço se um cupom trava a fila).

### Corrigido / Segurança
- **Venda cancelada (estornada) não pode mais ser reimpressa** — evita entregar ficha de uma
  venda devolvida (bloqueio no serviço e na tela de Histórico).

## [2.4.1-central] — Central de Impressora + status honesto

### Adicionado
- **Central de Impressora** (F12) reformulada, nível profissional:
  - **Semáforo de status ao vivo** (verde PRONTA / amarelo ATENÇÃO / vermelho PARADA) com
    dica de ação, auto-atualizado a cada 3s.
  - **Detalhes técnicos**: tipo (USB/Bluetooth/serial), porta (USB001/COMx), driver e se é
    padrão do PDV **e** do Windows.
  - **Fila de impressão** na tela: lista os cupons pendentes (jobs em erro em vermelho) e
    botão **Limpar fila** para destravar cupom preso sem sair do app.
  - **Ações rápidas**: Detectar automaticamente, Salvar como impressora do PDV, Imprimir
    teste (confirma a saída), Definir como padrão do Windows.

### Corrigido
- **"Detectar automaticamente" mentia "PRONTA (online)"** com a impressora desligada / sem
  cabo. Causa: o Windows só marca `WorkOffline` quando o usuário liga "usar offline"
  manualmente — desconexão física de térmica USB barata (MPT/POS) **não** é reportada; o SO
  só descobre ao mandar o job. Agora o status lê também `PrinterStatus`/`DetectedErrorState`
  e jobs travados, e a tela é **honesta**: "Instalada — o status real só é confirmado ao
  imprimir" (em vez de afirmar PRONTA), detectando também SEM PAPEL, TAMPA ABERTA e o último
  cupom travado (sinal de aparelho desligado).
- **"Imprimir Teste" dava "enviado!" mesmo com erro**: dizia sucesso só porque o Windows
  aceitou o job na fila. Agora o teste **confirma a saída**: aguarda a fila drenar e, se o
  job travar (Error/Offline/PaperOut), mostra "Falhou: … impressora desligada, sem papel ou
  desconectada." — só fica verde ("OK — saiu papel!") se realmente imprimiu.

## [2.4.0] — Gestão avançada, permissões e QA em sandbox

### Adicionado
- **Sistema de permissões por ação**: o admin delega, numa tela (com senha), quais ações o
  operador faz sem senha. Inclui trocar a senha de administrador. Ver ADR-0020.
- **Excluir seguro** em Produtos/Categorias/Promoções: exclusão permanente com dupla
  confirmação e **trava** (item com uso só pode Inativar). Ver ADR-0021.
- **Versionamento do cardápio**: exportar/importar o catálogo como `.json` datado. ADR-0021.
- **Painel em tempo real (F4)**: dashboard com Total em Gaveta, faturamento e itens mais
  vendidos, auto-atualizado, sem senha (só visualização).
- **Pagamento estilo PDV**: notas/moedas de R$1 a R$200 que **somam**, botões Exato e Limpar,
  campo de recebido vazio ao abrir.
- **Layout do Cupom**: preview renderiza os vales destacáveis; botão "Imprimir teste de layout".
- **Vales viram o modo PADRÃO** do cupom (a reimpressão já sai com os vales).
- **QA em sandbox Hyper-V**: scripts de VM descartável + E2E com screenshots por etapa. ADR-0022.
- **Tela Sobre** e **tela da Impressora** reformuladas (status online, tipo USB/Bluetooth,
  detecção automática).

### Alterado
- **Atalho legado removido** (coluna/campo de atalho e tela "Customizar Atalhos"): a
  navegação por teclado é 100% por posição (letra da categoria + número).
- Testes E2E de UI agora são **condicionais** (rodam só na sandbox; no-op fora).
- **App robusto em sessão não-interativa**: o tratador de erro não derruba mais o app ao
  reportar (usa `DefaultDesktopOnly`/só-loga) — essencial para automação/headless.

### Corrigido
- **Impressão de vales**: margem antes do 1º vale (não fica colado no cabeçalho) e sem
  excesso de papel após o último vale.
- **Anti-duplo-clique** nos botões "Imprimir teste" (Layout e Config Impressora): não
  imprime duas vezes se o operador clicar rápido.

### Testes e QA
- **203 testes unitários** (subindo de 157): além dos anteriores, cobrem **permissões**
  (defaults, regra fixa, persistência), **troca de operador** (fecha/abre turno, fundo),
  **soma de notas** do pagamento, **auto-detecção de impressora** (USB>Bluetooth), **backup
  automático** (agendador) e **dados do dashboard** (gaveta/faturamento/itens).
- **QA em sandbox Hyper-V funcionando de ponta a ponta**: a bateria E2E roda **dentro de uma
  VM descartável** (checkpoint → injeta → instala → testa na sessão interativa → extrai
  screenshots → rollback), 5/6 aprovados + 1 skip de impressora, com evidências visuais reais
  das telas (pagamento estilo PDV, painel em tempo real, histórico). Scripts em `qa/`.

## [2.3.0] — Fichas de vales, navegação por teclado e robustez de impressão

### Adicionado
- **Fichas de consumo destacáveis** (modelo quermesse): novo modo de cupom
  `Recibo + Vales destacáveis` desmembra cada item em N vales de "1X NOME" (um por unidade),
  separados por pontilhado para rasgar e entregar nas barracas. Selecionável no Layout do
  Cupom. Ver ADR-0015.
- **Navegação 100% por teclado**: `Letra da categoria → Número do item → Enter`. Todo item
  ganha badge/atalho automático pela posição; produto/categoria novos também. Ver ADR-0016.
- **Reimprimir Nota** no Histórico de Vendas (para vendas feitas antes de a impressora estar
  pronta).
- **Troca de operador com batimento de caixa opcional**: passa o bastão entre voluntários
  sem parar a venda; na troca, pode-se conferir a gaveta (esperado × contado → sobra/falta).
  Ver ADR-0019.
- **Impressão sem acento**: o cupom sai em ASCII limpo em qualquer térmica; a tela mantém
  acentuação. Ver ADR-0017.
- **Impressora plug-and-play**: auto-detecção ao abrir (USB tem prioridade sobre Bluetooth),
  escolha da fila online, e preparo anti-embola (religa offline + limpa jobs presos) antes de
  cada cupom. Ver ADR-0017.
- **Anti-corte de layout** (`AjusteLayout`) + cabeçalhos de grid padronizados (`EstiloGrid`):
  nenhuma tela abre com botões/texto cortados. Ver ADR-0018.

### Alterado
- **Senha de admin só em ações críticas** (caixa, fechamento, sangria/suprimento, produtos,
  backup, estorno). Categorias, promoções, impressora, layout e atalhos ficaram liberados.
- **Acentuação PT-BR** em toda a UI visível (menus, títulos, mensagens, colunas).
- **Ordenação de produtos por nome** (previsível), aposentando o campo `atalho` legado.
- **Vídeo de treinamento**: captura só a janela do app (uma tela), maximizada na primária,
  com trilha real e sem popup de impressora no modo demo. Ver ADR-0018.

### Corrigido
- Popup bloqueante de "erro na impressora" quando **não há** impressora configurada: agora a
  venda segue em silêncio (o diálogo Repetir/Cancelar só aparece com impressora que falhou).
- Testes E2E que abriam duas instâncias do app e travavam o desktop (porteiro anti-órfão +
  execução serial); testes de UI marcados `Skip` por dependerem de foco de tela dedicado.

## [2.2.0] — Motor de promoções/combos automáticos

### Adicionado
- **Motor de Preços (`PricingEngine`)**: ao adicionar/remover itens, o carrinho **auto-detecta**
  combos/promoções ativas e aplica o desconto por **conjunto completo**.
- **UX rastreável**: em vez de substituir os itens, adiciona uma **linha verde**
  `* DESCONTO: <combo>` com o valor negativo. Remover um item do combo reavalia e some com a linha.
- **Cupom**: mostra a linha de desconto do combo + resumo **Subtotal / Descontos / TOTAL**.
- **Gestão de Promoções (`FormPromocoes`)**: preço especial ou combo, **janela de horário**
  (DateTimePicker), liga/desliga instantâneo, itens exigidos, **soft delete**.
- **Seed do cartaz**: os combos **"até 20h"** já vêm cadastrados (1 Refri+1 Bolo, 1 Refri+1
  Pinhão, 2 Refri = R$ 10, ou seja R$ 2 de desconto), como promoções auto-detectáveis.

### Confirmado
- **Eco-print** (Recebido/Troco só no Dinheiro) já estava ativo desde a 2.0.

## [2.1.0] — Correção de carrinho e estorno de vendas

### Adicionado
- **Remover item do carrinho** (pré-venda): tecla `Delete`/`Backspace` na grade ou botão
  **"Remover Item Selecionado"**; o total recalcula na hora.
- **Histórico de Vendas do turno** (`F3` / menu Arquivo): lista Id, hora, total, pagamento e
  status das vendas do turno.
- **Estorno/cancelamento** de venda com **trava de admin** + aviso de estorno físico. É
  **soft delete** (status `Cancelada`): a venda nunca é apagada (audit trail), mas sai dos
  totais, do **Total em Gaveta** e da **Leitura Z** — a conciliação bate centavo a centavo.

### Migração
- `vendas.status` adicionado via `ALTER TABLE` (bancos antigos preservados; migração agora
  cobre `caixa_id` e `status` juntas).

## [2.0.1] — Impressão Bluetooth

### Adicionado
- **Impressão via Bluetooth/serial**: `EscPosPrinter` agora imprime também em portas **COM**
  (`SerialPort` 9600 8N1), além da fila USB do Windows. O mesmo binário atende as duas conexões.
- **Rótulo das portas** no F12 (`COM6 (Bluetooth)`, `COM3 (serial)`) para o operador escolher.
- **Driver POS58** versionado no repo (`drivers/POS58`) + guia **[IMPRESSORA.md](IMPRESSORA.md)**.

### Validado
- MPT-II real via Bluetooth (COM6): cupom de teste e **duas vendas atômicas** impressas.

## [2.0.0] — Refatoração PDV profissional

### Adicionado
- **Turnos de caixa**: abertura com fundo, sangria/suprimento, fechamento com **Leitura Z**.
- **Total em Gaveta** e **dashboard** de fechamento com **gráficos** (GDI+), export **CSV**.
- **CRUD de Produtos** (soft delete) e **CRUD de Categorias** (ordem/ativo).
- **Customizar Atalhos** (teclas 1–9 → produtos, dinâmico).
- **Layout do Cupom** com **preview ao vivo**; modo **Ficha de Consumo** (fonte expandida,
  corte por item) além do **Recibo Completo**.
- Pagamento com **Débito/Crédito** separados, **valores rápidos** e pré-preenchimento.
- **Senha de administrador** nas telas sensíveis; janelas modais singleton com Dispose.
- **Blindagem global** de exceções + **sistema de logs** em `%AppData%\...\logs`.
- Tela principal reconstruída: **MenuStrip**, **StatusStrip**, **SplitContainer** (carrinho
  fixo ≥420px), **DataGridView**, **TabControl** por categoria + aba **"Todos"**.
- Documentação: arquitetura, ADRs, funcionalidades, diagnóstico, manual.

### Alterado
- `FormaPagamento` agora inclui `CartaoDebito`/`CartaoCredito` (aditivo, valores estáveis).
- Impressão migrada para `ConfigCupom`/`LinhaCupom` (estilos por linha) e constantes ESC/POS nomeadas.
- `EscPosPrinter.EnviarRaw` blindado: falha de hardware nunca lança.

### Corrigido
- Crash de `SplitContainer` no startup (min sizes aplicados antes da largura real).
- Cabeçalho/coluna do carrinho cortados na borda (grid em host com margem + estilo de header).

### Migração
- `vendas.caixa_id` adicionado via `ALTER TABLE` (bancos antigos preservados).
- Tabela `categorias` semeada na ordem do cartaz se estiver vazia.

## [1.0.0] — MVP
- Venda, carrinho, cupom 32 col, SQLite WAL, backup, fechamento simples, instalador, CI/CD.
