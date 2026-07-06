# DOCUMENTAÇÃO_CORE — Especificação de Requisitos de Software (SRS)

> **Sistema de Ponto de Venda (PDV) para Eventos** — festa junina / quermesse.
>
> **Este documento é a Fonte Única de Verdade (Single Source of Truth) do sistema.**
> Ele descreve **o que o sistema faz e como se comporta** — nunca *com qual tecnologia*.
> Qualquer equipe (ou IA) deve conseguir **reconstruir o PDV do zero em qualquer stack**
> (Java, Node, Go, Python, C#, Rust…) a partir daqui, sem consultar o código original.
>
> **Regra de leitura:** onde este documento diz "o sistema DEVE", é um requisito obrigatório.
> Nenhuma seção menciona linguagem, framework de UI, motor de banco ou SO — apenas
> comportamento, regras de negócio, fórmulas e protocolos.

---

## 0. Convenções Globais (invariantes do sistema inteiro)

Estas invariantes valem em **toda** a especificação:

1. **Dinheiro é sempre inteiro de CENTAVOS.** R$ 7,00 → `700`. Nunca usar ponto flutuante para
   valores monetários (evita erro de arredondamento em dinheiro). Toda soma, desconto e troco
   opera sobre inteiros de centavos.
2. **Tempo é carimbo ISO-8601** (com data e hora, formato round-trip). As strings ISO-8601
   **ordenam lexicograficamente**, o que o sistema explora para filtrar por período comparando
   strings diretamente (`data >= inicio AND data <= fim`).
3. **Nada financeiro é apagado de verdade.** Estorno, exclusão de produto/categoria/promoção
   usam *soft delete* (marca inativo/cancelado). Exclusão física só é permitida quando não há
   dependências (ver §7).
4. **Persistência do dado ANTES de qualquer efeito colateral** (impressão, backup). Em nenhum
   fluxo a impressão precede a gravação da venda.
5. **Resiliência acima de conveniência:** nenhuma falha de periférico (impressora) ou de
   infraestrutura (backup, energia) pode derrubar a operação de venda.

---

## 1. Domínio e Dicionário de Dados

### 1.1 Enumerações (valores fixos — **a ordem/valor inteiro é estável e nunca deve ser reordenado**, pois é persistido)

**FormaPagamento**
| Valor | Nome | Significado |
|---|---|---|
| 0 | Dinheiro | Espécie física; gera troco; entra na gaveta |
| 1 | Pix | Transferência instantânea; **não** entra na gaveta |
| 2 | Cartao | Cartão genérico/legado (antes de separar bandeiras) |
| 3 | CartaoDebito | Débito |
| 4 | CartaoCredito | Crédito |
| 5 | **Cortesia** | Brinde: entrega os itens **sem cobrar**; fora da gaveta e do faturamento |

**StatusVenda:** `Concluida = 0`, `Cancelada = 1` (estorno = soft delete).

**StatusCaixa (turno):** `Aberto = 0`, `Fechado = 1`.

**TipoMovimento:** `Sangria = 0` (retirada de dinheiro da gaveta), `Suprimento = 1` (entrada).

**TipoPromocao:** `PrecoEspecial = 0`, `Combo = 1`.
> *Regra oculta:* o motor de preços **não diferencia** os dois no cálculo — ambos aplicam a
> mesma matemática de "conjunto completo". O tipo é apenas rótulo semântico.

**DiasSemana** (flags bit-a-bit, combináveis):
`Nenhum=0`, `Domingo=1`, `Segunda=2`, `Terca=4`, `Quarta=8`, `Quinta=16`, `Sexta=32`,
`Sabado=64`, `Todos=127`.
> `Nenhum` **ou** `Todos` significam ambos "vale todo dia" (não restringe).

**ModoCupom** (modo de impressão do ticket):
| Valor | Nome | Descrição |
|---|---|---|
| 0 | Completo | Recibo com valores, total, pagamento, troco |
| 1 | FichaConsumo | Só qtd+nome em fonte grande, sem valores (econômico) |
| 2 | **ReciboComVales** | Recibo gerencial + vales destacáveis — **DEFAULT do sistema** |
| 3 | SoVales | Só os vales, com mini-cabeçalho da festa em cada ficha |

**EstiloLinha** (estilo de renderização de uma linha do cupom): `Normal=0` (texto ≤32 col),
`Titulo=1` (centralizado, fonte dupla), `Expandida=2` (dupla altura+largura, útil 16 col),
`Corte=3` (marcador de corte, não imprime texto).

**Export — ModoItens:** `AmontoadoNaVenda` (1 linha por venda, itens numa célula),
`UmaLinhaPorItem` (explode: 1 linha por item).
**Export — SecoesExport (flags):** `Resumo=1`, `Vendas=2`, `Itens=4`, `Precos=8`, `Tudo=15`.
**Export — FormatoExport:** `CsvUnico`, `CsvMultiplos`, `XlsxUnico`, `XlsxAbas`.

### 1.2 Entidades de negócio

> Tipos descritos semanticamente. "centavos" = inteiro de centavos; "ISO" = carimbo ISO-8601.

**Produto** (catálogo)
- `Id`: texto (chave). `Nome`: texto. `PrecoCentavos`: centavos. `Categoria`: texto (default "Geral").
  `Atalho`: inteiro 1..9 (0=sem) — **campo LEGADO**, não governa mais a navegação. `Ativo`: booleano
  (default verdadeiro). `Composicao`: lista de **ComboItem**.
- Derivado `EhCombo` = a composição tem ≥1 item.
- **Invariante:** um combo tem **preço próprio**; a composição é **informativa** (não recalcula
  preço nem baixa estoque).

**ComboItem:** `ProdutoId`: texto; `Quantidade`: inteiro (default 1).

**Categoria** (aba do caixa): identidade pelo `Nome` (chave); `Ordem`: inteiro (posição);
`Ativo`: booleano (default verdadeiro; soft delete oculta a aba).

**ItemVenda** (linha do carrinho / linha persistida da venda)
- `ProdutoId`: texto. `Nome`: texto (snapshot). `PrecoUnitarioCentavos`: centavos (snapshot no
  momento da venda). `Quantidade`: inteiro (default 1).
- Derivado `SubtotalCentavos = PrecoUnitarioCentavos × Quantidade`.
- ⚠️ **Convenção CRÍTICA (sentinela de desconto):** uma linha com `ProdutoId` **vazio** E
  `PrecoUnitarioCentavos` **negativo** representa um **desconto de combo/promoção**, não um
  produto. Persistida com `Quantidade=1` e preço = `−valorDesconto`. **Toda lógica que percorre
  itens (impressão, export, contagem, motor de preços) DEVE filtrar essa sentinela.**

**Venda** (venda finalizada e persistida)
- `Id`: inteiro sequencial. `DataHora`: ISO (default agora). `Itens`: lista de ItemVenda
  (inclui linhas de desconto). `TotalCentavos`: centavos (já com desconto). `Forma`:
  FormaPagamento. `RecebidoCentavos`: centavos (só em dinheiro; 0 nas demais). `TrocoCentavos`:
  centavos. `Operador`: texto. `CaixaId`: inteiro nullable (null = venda legada sem turno).
  `Status`: StatusVenda (default Concluida). `Impressoes`: inteiro (nº de vias: 1ª + reimpressões).
  `Observacao`: texto livre — **em Cortesia guarda o NOME de quem recebeu** (`"CORTESIA: <nome>"`).
- Derivados: `Cancelada` (Status==Cancelada), `EhCortesia` (Forma==Cortesia).
- **Invariante:** `Recebido` e `Troco` são forçados a 0 quando a forma não é Dinheiro.

**Turno** (sessão de caixa)
- `Id`: inteiro. `Abertura`: ISO (default agora). `Fechamento`: ISO nullable.
  `FundoCentavos`: centavos (troco inicial). `Operador`: texto. `Status`: StatusCaixa (default Aberto).
- Derivado `EstaAberto`. Propósito: contabilidade separada por dia/turno.

**MovimentoCaixa** (sangria/suprimento): `Id`; `CaixaId` (a qual turno pertence); `Tipo`
(TipoMovimento); `ValorCentavos`: centavos **sempre positivo** (o sinal vem do Tipo);
`Motivo`: texto; `DataHora`: ISO (default agora).

**Promocao** (regra agendada)
- `Id`; `Descricao`; `Tipo` (TipoPromocao); `ValorDescontoCentavos`: desconto **por conjunto
  completo**; `HoraInicio`/`HoraFim`: janela de hora nullable; `DataInicio`/`DataFim`:
  intervalo de datas nullable (data única = Inicio==Fim; grava só a parte de DATA);
  `Dias`: DiasSemana (default Todos=127); `Ativo`: booleano (default verdadeiro); `Itens`: lista de
  **PromocaoItem**.

**PromocaoItem:** `ProdutoId`; `Quantidade` (mínima exigida no carrinho, default 1).

**DescontoAplicado** (transitório, linha "verde" no carrinho): `Descricao`; `ValorCentavos`
(positivo).

### 1.3 Entidades de resultado (calculadas, não persistidas)

**ResumoCaixa:** `TotalDinheiroCentavos`, `TotalPixCentavos`, `TotalCartaoCentavos` (genérico +
débito + crédito), `TotalDebitoCentavos`, `TotalCreditoCentavos`, `FaturamentoBrutoCentavos`,
`QuantidadeVendas`, `TotalCortesiaCentavos`, `QuantidadeCortesias`.

**ResumoTurno:** referência ao Turno + `Vendas` (ResumoCaixa) + `SuprimentosCentavos` +
`SangriasCentavos` + `FundoCentavos` + derivado **`TotalGavetaCentavos`** (§3.6).

**ResultadoBatimento:** `EsperadoCentavos`, `ContadoCentavos`, `DiferencaCentavos`
(=Contado−Esperado), `Bate` (==0), `Sobra` (>0), `Falta` (<0).

**ItemVendido:** `Nome`, `Quantidade`, `TotalCentavos`.

**PrecoPraticado:** `Nome`, `PrecoUnitarioCentavos`, `Quantidade`, `TotalCentavos`.

---

## 2. Relacionamentos e Cardinalidade

- **Venda → ItemVenda**: 1:N (itens carregam a venda; incluem linhas de desconto).
- **Venda → Turno**: N:1 (`CaixaId` nullable — vendas legadas sem turno).
- **Turno → MovimentoCaixa**: 1:N.
- **Turno → Venda**: 1:N (filtra `Venda.CaixaId == Turno.Id`).
- **Produto → Categoria**: N:1 **por nome** (não por chave estrangeira formal).
- **Produto → ComboItem**: 1:N (composição do combo).
- **Promocao → PromocaoItem**: 1:N.
- **PromocaoItem/ComboItem → Produto**: referência lógica por `ProdutoId` (sem FK física).
- **ItemVenda → Produto**: referência histórica **frouxa** — o item guarda **snapshot** de nome
  e preço, então continua válido mesmo se o produto mudar ou for inativado.

**Integridade de negócio:** nada financeiro é apagado (soft delete). Exclusão física só quando
não há dependências (§7).

---

## 3. Regras de Negócio Críticas (Core Business Rules)

### 3.1 Carrinho
- **Agregação:** adicionar produto já existente (mesmo `ProdutoId`) **soma a quantidade** na
  linha existente (não cria linha nova). Quantidade ≤ 0 no add é ignorada.
- **Snapshot de preço:** ao adicionar, copia o preço atual do produto; mudanças posteriores no
  catálogo **não** afetam o carrinho.
- **Diminuir um:** decrementa 1; se chegar a 0, remove a linha.
- `Subtotal` = soma dos `SubtotalCentavos` de todos os itens.
- `DescontoTotal` = soma dos descontos aplicados (positivos).
- **`Total = Subtotal − DescontoTotal`.**
- **Reavaliação:** a cada add/remove, o sistema **DEVE** limpar e recalcular os descontos
  rodando o motor de preços (§3.3) sobre os itens atuais.

### 3.2 Troco e "recebido insuficiente"
- `CalcularTroco(total, recebido)`: se `recebido < total` → **erro "pagamento insuficiente"**
  (bloqueia a venda). Senão `troco = recebido − total`.
- No fechamento da venda, o troco só é calculado quando a forma é **Dinheiro**; nas demais
  `troco = 0` e `recebido = 0`.

### 3.3 Motor de Promoções / Combos (função pura)
Entrada: itens do carrinho, promoções, instante `agora`.
1. Conta a quantidade total **por produto** no carrinho, **ignorando** linhas de desconto
   (sentinela). Agrupa por `ProdutoId` somando quantidades.
2. Para cada promoção, **pula** se: não passa `ValidaAgora(agora)` (§3.9) **ou** não tem itens
   exigidos **ou** `ValorDescontoCentavos ≤ 0`.
3. **Quantos conjuntos completos cabem:**
   ```
   conjuntos = MIN sobre todos os itens exigidos req de:
               floor( qtdNoCarrinho[req.ProdutoId] / max(1, req.Quantidade) )
   ```
   Se algum item exigido não está no carrinho → conjuntos = 0 (para).
4. Se `conjuntos > 0`: aplica desconto de **`ValorDescontoCentavos × conjuntos`** com a descrição
   da promoção.

> **Regras ocultas do motor:** promoções são **independentes** — várias podem aplicar
> simultaneamente, cada uma gera sua linha de desconto. **Não há alocação exclusiva de unidades**:
> a mesma unidade pode satisfazer duas promoções ao mesmo tempo. Não há limite de "1 desconto por
> venda".

### 3.4 Persistência do desconto na Venda
Ao converter carrinho → venda: copia os itens normais e **acrescenta uma linha por desconto** com
`ProdutoId=""`, `Nome=descrição`, `PrecoUnitarioCentavos = −valorDesconto`, `Quantidade=1`. Assim o
desconto persiste, imprime, entra no total e é rastreável. `Total` gravado = já com desconto.

### 3.5 Consolidação de Caixa (sobre uma lista de vendas)
- **Ignora vendas Canceladas** (para a gaveta bater centavo a centavo).
- Soma por forma: dinheiro, pix, débito, crédito, cartão genérico. `TotalCartao = genérico +
  débito + crédito`.
- **`Faturamento bruto = dinheiro + pix + cartãoTotal`. Cortesia NÃO entra.**
- `QuantidadeVendas` = contagem de vendas **pagas** (exclui cortesias).
- Cortesias: contadas e somadas **separadamente** (`TotalCortesia`, `QuantidadeCortesias`).

### 3.6 Consolidação de Turno e **TOTAL EM GAVETA**
- Suprimentos = soma dos movimentos Suprimento; Sangrias = soma dos Sangria.
- **Fórmula fundamental:**
  ```
  TotalGaveta = FundoInicial + VendasEmDinheiro + Suprimentos − Sangrias
  ```
- **Por que Pix e cartão NÃO entram na gaveta:** não há dinheiro físico na gaveta por eles — o
  valor caiu na conta/maquininha. A gaveta rastreia **apenas espécie física**.

### 3.7 Cortesia (por que fica de fora)
Entrega itens sem cobrança → não gera receita nem dinheiro físico:
- **Fora** do faturamento bruto e **fora** da gaveta.
- Contabilizada numa conta **separada** (valor teórico dos itens dados) para o gestor ver "quanto
  custou em brindes".
- **Não** conta como venda paga em `QuantidadeVendas`.
- Exige o **nome do beneficiário** (rastreabilidade), gravado na Observação.

### 3.8 Batimento de gaveta
Compara o **contado fisicamente** com o **esperado** (TotalGaveta). `Diferença = Contado −
Esperado`: `>0` = SOBRA, `<0` = FALTA, `0` = bate.

### 3.9 `ValidaAgora` da Promoção — 4 filtros combinados (AND)
Todos precisam passar; cada filtro só restringe se configurado:
1. **Ativa:** se inativa → false.
2. **Intervalo de datas** (compara **só a DATA**, ignora hora): `hoje < DataInicio` → false;
   `hoje > DataFim` → false. Lados nulos não restringem.
3. **Dia da semana:** se `Dias` é Nenhum ou Todos → passa. Senão, mapeia o dia de hoje ao bit e
   verifica `(Dias & bit) != 0`.
4. **Janela de hora:** ambos nulos → passa. Senão `ini = HoraInicio ?? 00:00:00`,
   `fim = HoraFim ?? 23:59:59`:
   - **Janela normal** (`ini ≤ fim`, ex. 08:00–20:00): vale se `hora ≥ ini E hora ≤ fim`.
   - **Janela que cruza a meia-noite** (`ini > fim`, ex. 22:00–02:00): vale se
     `hora ≥ ini OU hora ≤ fim`.

### 3.10 Contagem de itens vendidos (auditoria da Leitura Z)
Ignora canceladas; achata itens; **ignora sentinelas de desconto**; agrupa **por Nome**
(não por ProdutoId — dois produtos homônimos se fundem, deliberado para relatório legível);
soma quantidade e total; ordena por total desc, depois nome.

### 3.11 Preços praticados (detecção de mudança de preço)
Ignora canceladas e sentinelas; agrupa **por tupla (Nome, PrecoUnitario)**; soma quantidade e
total; ordena por nome, depois preço.
> **Regra:** se o mesmo item aparece com preços unitários diferentes no período (ex.: Chopp R$10 →
> R$12 durante a festa), gera **linhas separadas**. No relatório, quando um produto tem >1 preço,
> todas as linhas ganham a marca **`"PREÇO MUDOU"`**. (O item guarda o preço do momento da venda,
> então o histórico é fiel.)

---

## 4. Desmembramento de Impressão (regra exata dos modos de cupom)

Contexto: térmica **58mm = 32 colunas**; fonte **Expandida** = **16 colunas** úteis. Formatação é
pura (texto in / texto out).

**Regra "item físico":** um item vira ficha/vale, **exceto** a sentinela de desconto
(`ProdutoId` vazio E subtotal negativo). Itens normais entram mesmo sem `ProdutoId` (compat. com
vendas legadas).

### 4.1 Modo Completo
Cabeçalho (Evento título; Subtítulo; "Cupom nao fiscal"; `Venda #Id`; Data; Operador se houver).
Itens: sentinela vira `* Nome ... -R$ x`; itens normais **sempre com quantidade** (`2x Pipoca ...
R$ 10,00`). Se houve desconto: **Subtotal** (soma dos positivos) + **Descontos (combo)** (soma dos
negativos) + **TOTAL** já com desconto. Depois `Pagamento`; se Dinheiro, `Recebido` e `Troco`.
**Rodapé** uma vez no fim.

### 4.2 Modo FichaConsumo
Mini-cabeçalho (Evento + linha fina com `#Id` + data + divisória).
**Desmembramento exato:** achata os itens físicos em **uma unidade por vez** (repete o Nome
`Quantidade` vezes). Logo **`3x Cachorro` vira 3 fichas independentes `1X CACHORRO`** (não agrupa
"3X"). Cada ficha em fonte **Expandida** (16 col, com quebra). Entre fichas (exceto após a última):
se **"separar por item"** ligado → insere **marcador de Corte** (cada ficha sai fisicamente
separada, para barracas diferentes); senão → linha em branco. Rodapé no fim, se configurado.

### 4.3 Modo ReciboComVales (DEFAULT)
1. Imprime o **recibo gerencial completo** (idêntico ao Completo).
2. Divisória **dupla** (`===`) + `FICHAS DE CONSUMO` + divisória dupla + 1 linha em branco.
3. **Desmembramento:** para cada item físico, imprime `Quantidade` vales. Cada vale =
   `1X NOME` (maiúsculas), fonte **Expandida**, quebra em **28 colunas**, seguido de `Vale 1 item`
   centralizado, **1 linha em branco + pontilhado (rasgar) + 1 linha em branco** (folga simétrica).
   Ex.: `3x Cachorro` → recibo + 3 vales `1X CACHORRO`.
4. Remove linhas em branco no fim.

### 4.4 Modo SoVales
Sem recibo gerencial. Para cada item físico, `Quantidade` vales; **cada vale carrega seu próprio
mini-cabeçalho da festa** (Evento + `Subtitulo - #Id dd/MM HH:mm`) para se identificar sozinho na
barraca. Depois linha em branco, `1X NOME` expandido, `Vale 1 item`, branco, pontilhado, branco.
**Rodapé impresso UMA vez no fim** (não em cada vale, para economizar papel). Remove brancos no fim.

### 4.5 Onde entra o rodapé por modo
| Modo | Rodapé |
|---|---|
| Completo | uma vez no fim |
| FichaConsumo | uma vez no fim |
| ReciboComVales | dentro do recibo gerencial (uma vez), antes dos vales |
| SoVales | uma vez no fim, após todos os vales |

### 4.6 Leitura Z (Fechamento)
`FECHAMENTO DE CAIXA` + `Leitura Z` + Evento; dados do turno (#Id, Abertura, Fechamento — usa
"agora" se ainda não fechado, Operador); bloco **ENTRADAS POR PAGAMENTO** (Dinheiro/Pix/Débito/
Crédito); `VENDAS BRUTAS` + `Nº de vendas`; bloco **CONFERÊNCIA DA GAVETA** (Fundo inicial,
(+) Dinheiro, (+) Suprimentos, (−) Sangrias, **TOTAL EM GAVETA** em destaque); bloco **ITENS
VENDIDOS (auditoria)** se houver; fecha com `Confira a gaveta!` + data/hora.

---

## 5. Casos de Uso e Fluxos de Usuário

### 5.1 Abrir Caixa → Venda → Fechamento

**Abrir Caixa (turno)**
1. Ao entrar na tela principal, o sistema verifica se há turno aberto.
2. Se **não** há, o sistema **abre imediatamente e de forma bloqueante** o diálogo de abertura —
   **sem caixa aberto não se vende**.
3. Operador informa **fundo inicial** e **nome do operador**.
4. Turno passa a "Aberto", com carimbo de abertura, e é vinculado a todas as vendas.
5. **Só pode existir UM turno aberto por vez** — abrir outro é recusado.
6. Abrir caixa exige senha de admin por padrão (configurável).

**Venda**
1. Operador monta o carrinho (teclado ou clique — §6).
2. A cada add/remove, o sistema **reavalia promoções** e exibe linhas de desconto separadas.
3. **TOTAL** exibido ao vivo.
4. Dispara **Pagamento** (§5.5). Guardas: carrinho vazio → não abre; caixa fechado → bloqueia.
5. A venda é **persistida ANTES de imprimir**, o carrinho é limpo, então imprime.
6. Falha de impressão **não** perde a venda (pode reimprimir).

**Fechamento (Leitura Z)**
1. Aciona Fechamento (senha de admin por padrão).
2. Consolida o turno (fundo, vendas por forma, sangrias/suprimentos, **Total em Gaveta**, itens
   vendidos).
3. Turno marcado "Fechado" com carimbo; deixa de ser o atual.
4. **Gatilho de backup ao fechar** (§8.4) dispara (best-effort, nunca impede o fechamento).
5. Opcional: imprime a Leitura Z; conferência de gaveta (contado × esperado).

### 5.2 Troca de Operador
Exige caixa aberto + senha. **Fecha o turno atual** (com Leitura Z / auditoria) e **abre um novo**
para o próximo operador, **sem parar o evento**. **Fundo do novo turno = Total em Gaveta esperado
do turno que fechou** (o dinheiro que fica na gaveta), não zero. Como fecha um caixa, o **backup ao
fechar também dispara**.

### 5.3 Sangria / Suprimento
Exige caixa aberto + senha. Sangria = retirada; Suprimento = entrada. Registra **valor + motivo +
carimbo**, vinculado ao turno. Entram no Total em Gaveta (Suprimentos somam, Sangrias subtraem).

### 5.4 Reimpressão
A partir do Histórico. **Venda cancelada não pode ser reimpressa.** O operador **escolhe o modo do
cupom** na reimpressão. Cada impressão bem-sucedida **incrementa o contador de vias**.

### 5.5 Pagamento (detalhe)
Formas: Dinheiro, Pix, Débito, Crédito, **Cortesia**.
- **Dinheiro:** digita o recebido; **troco ao vivo**; `recebido < total` mostra "Falta"; confirma
  exige `recebido ≥ total`.
- **Pix/Débito/Crédito:** valor pré-preenchido com o total (troco zero).
- **Cortesia:** recebido = 0, sem troco; **exige o NOME de quem recebeu** (gravado na observação).
- Confirmação finaliza: persiste → limpa carrinho → imprime.

### 5.6 Módulo Gerencial ("time travel")
Desacoplado do caixa aberto; consulta pelo **carimbo de tempo**.

**Relatório Gerencial por Período:** filtro **De/Até** + atalhos **Hoje/Ontem/7 dias/Tudo** (abre
em Hoje). Mostra resumo (faturamento, por forma, canceladas, cortesias), gráficos e grade. Exibe
**Preços Praticados** (marca "PREÇO MUDOU"). **Exporta exatamente o período filtrado** (§9),
nome com as datas. Somente leitura.
> Regras: "Tudo" usa `2020-01-01` como início efetivo; datas invertidas (De > Até) são **trocadas
> automaticamente**; "Até" sempre vai ao **último instante do dia (23:59:59.999)**.

**Balanço Geral (todos os caixas):** lista **todos os turnos** (do mais recente ao antigo) com
operador, abertura/fechamento, nº de vendas, faturamento, Total em Gaveta; resumo global; detalhe
por turno (fundo, formas, suprimentos, sangrias, gaveta, canceladas, movimentos). Contém a
**exclusão de turno** (§7).

---

## 6. Requisitos de UX / Agilidade (navegação por teclado)

> **Princípio:** o caixa **DEVE** ser 100% operável por teclado, sem depender do mouse, e nunca
> travar por causa de periféricos. Estes requisitos são **agnósticos de biblioteca gráfica**.

### 6.1 Padrão central: **Letra da categoria → Número do item → Enter**
- Cada categoria **DEVE** ter uma **letra de atalho** (a inicial livre; se colidir, a próxima
  letra livre do nome; se esgotar, qualquer A–Z livre), exibida no título da aba e num **badge**
  em cada botão (`C2` = categoria Comidas, item 2).
- Cada item da categoria **DEVE** ter um **número posicional determinístico** (1..N), com ordem
  estável (por Nome).
- Sequência: **letra** entra no "modo de seleção" e destaca o 1º item; **número** (1–9) destaca o
  N-ésimo; **Enter** adiciona o destacado e sai do modo.
- **Anti-erro:** número **só** age **dentro** do modo. Fora, é ignorado — **não existe atalho
  global 1–9**.
- Feedback: item destacado recebe realce forte; rodapé mostra a dica ("► Comidas (C) 2: Pastel —
  Enter adiciona").

### 6.2 Enter / Esc / Del contextuais
- **Enter:** no modo, adiciona o destacado; **fora do modo, vai ao Pagamento**.
- **Esc:** no modo, **sai do modo**; fora, **cancela a venda** (limpa o carrinho). Cancelar a
  venda **DEVE pedir confirmação** (default "não apagar") — não perde o pedido por Esc acidental.
  Remover 1 item **não** pede confirmação.
- **Del / Backspace:** remove item do carrinho **de qualquer lugar**. Se nada válido está
  selecionado (ou é linha de desconto), remove o **último item adicionado** (correção rápida).

### 6.3 Teclas de função e atalhos
| Tecla | Ação | | Atalho | Ação |
|---|---|---|---|---|
| F1 | Sobre | | Ctrl+T | Trocar Operador |
| F2 | Pagar | | Ctrl+R | Relatório Gerencial |
| F3 | Histórico | | Ctrl+E | Exportar do turno |
| F4 | Painel em Tempo Real | | Ctrl+P | Gerenciar Produtos |
| F6 | Balanço Geral | | Ctrl+G | Gerenciar Categorias |
| F7 | Abrir Caixa | | Ctrl+M | Gerenciar Promoções |
| F8 | Backup / Restauração | | Ctrl+L | Layout do Cupom |
| F9 | Fechamento | | Ctrl+K | Permissões |
| F12 | Gerenciar Impressora | | Ctrl+H | Trocar Senha Admin |
| Del | Remove item | | Ctrl+S | Sangria |
| Esc | Sai modo / Cancela venda | | Ctrl+U | Suprimento |
| Alt+F4 | Sair | | | |

> Combinações com **Ctrl/Alt não devem ser capturadas** pela navegação de catálogo.

### 6.4 Atalhos da tela de Pagamento
- **D**=Dinheiro, **P**=Pix, **B**=Débito, **C**=Crédito, **O**=Cortesia. **Enter**=confirmar
  (mesmo dentro do campo de nome da cortesia), **Esc**=voltar.
- **Requisito de robustez:** ao pressionar a letra da forma, o caractere **não deve** ser digitado
  no campo de valor (evita "p7,00").
- Valor rápido (Dinheiro): **"Exato"** (preenche o total), **"Limpar"** (zera), e botões de
  nota/moeda que **SOMAM** ao recebido (R$50 + R$20 → R$70). Denominações: 1, 2, 5, 10, 20, 50,
  100, 200 reais.

### 6.5 Outros requisitos
- O painel do **carrinho DEVE ter largura mínima e nunca colapsar**.
- Catálogo **recarrega dinamicamente** (produtos/categorias/promoções refletem na hora). Promoção
  cadastrada com itens já no carrinho **aplica o desconto imediatamente**.
- Aba "Todos" agrupa tudo numa lista rolável.
- Remover item também por **duplo-clique** e **menu de contexto**, além do botão e do Del.
- **Combo ativo vira botão-atalho** próprio (produto-combo espelho) que adiciona todos os itens de
  uma vez com o desconto aplicado.

---

## 7. Regras de Exclusão e Segurança de Turno

- **Soft delete** em tudo financeiro: estorno marca **Cancelada** (nunca apaga); produto/categoria
  inativados (nunca removidos se têm histórico).
- **Exclusão física só sem dependências:** produto com vendas → só inativar; categoria com
  produtos → só inativar.
- **Exclusão de turno** (no Balanço Geral), dois níveis:
  1. Turno **sem vendas** (teste): confirmação simples Sim/Não.
  2. Turno **com vendas** (teste que não se quer manter): **trava forte** — senha de admin **+**
     digitar exatamente a palavra **`EXCLUIR`** **+** aviso do que será apagado. Exclusão **em
     cascata** (itens → vendas → movimentos → caixa) em transação atômica.
  3. **Nunca** se pode excluir o **caixa aberto no momento**.

---

## 8. Requisitos Não-Funcionais e Resiliência

### 8.1 Persistência (propriedades ACID esperadas)
- O armazenamento **DEVE** ser transacional (ACID). Toda venda grava cabeçalho + itens numa
  **transação atômica** (tudo-ou-nada).
- **Tolerância a queda de energia:** uma falha no meio de uma gravação **não pode corromper o
  histórico** (esperado: journaling / write-ahead log com sincronização segura). Ambiente de festa
  tem quedas de energia — é requisito de primeira classe.
- Valores monetários **em centavos inteiros**; tempo em **ISO-8601**.

### 8.2 Retomada de turno após queda
Ao iniciar, o sistema **reabre automaticamente** o turno que ficou aberto (busca o turno "aberto"
mais recente) — a festa continua de onde parou.

### 8.3 Migração de schema sem perda de dados
Na inicialização, cria estruturas ausentes e **adiciona campos novos a bancos antigos**,
**preservando o histórico**. Defaults de migração **preservam o comportamento antigo** (ex.:
"dias da semana" default = todos os dias → promoções antigas continuam valendo). Índices
dependentes de campos novos são criados **só depois** de garantir o campo.

### 8.4 Disaster Recovery (backup / restauração)

**Gatilhos de backup automático** (cada um liga/desliga independente):
| Gatilho | Default |
|---|---|
| A cada N minutos (timer que não trava a UI; 0 = desligado) | desligado |
| A cada N vendas concluídas (0 = desligado) | desligado |
| **Ao fechar o caixa** (dispara também na troca de operador) | **LIGADO** |
| Ao abrir o app | desligado |
| Ao sair do app | desligado |

**Propriedades do backup**
- **Backup físico** do banco inteiro (arquivo), ideal para "trocar de PC em 2 minutos".
- Antes de copiar, força **checkpoint** do log de escrita (garante que o disco tem tudo) e libera
  o arquivo.
- **Nome único por timestamp**; dois no mesmo segundo → sufixo `_2`, `_3`… (**nunca sobrescreve**
  um backup anterior).
- **Best-effort absoluto:** **nunca lança**; falha não derruba o caixa nem impede o fechamento
  (erros só em log).
- **Retenção:** manter apenas os **N mais recentes** (default **10**; 0 = todos); apaga os antigos.

**Restauração**
- Detecta `.db` ou `.zip` pela extensão; de zip, extrai o primeiro banco.
- **Antes de sobrescrever**, guarda o atual como cópia de segurança (`.bak`).
- Remove artefatos órfãos do log de escrita do destino.
- **Preserva os Ids originais** das vendas (substitui o conjunto, não duplica/renumera).

---

## 9. Requisitos de Integração de Hardware — Impressora Térmica (protocolo)

### 9.1 Físico / transporte
- Alvo: térmica **58mm, 32 colunas**. Comunicação por **envio RAW de bytes**, **não** via
  subsistema gráfico de impressão de documentos (evita diálogos, imagens lentas, margens erradas).
- Dois caminhos, escolhidos pelo formato do alvo:
  1. **Fila do SO / USB** (nome de impressora instalada) → envio via spooler, documento tipo "RAW".
  2. **Serial / COM (Bluetooth SPP ou USB-serial)** — alvo começa com "COMx". Parâmetros: **9600
     bps, 8 data bits, sem paridade, 1 stop bit, sem handshake, DTR+RTS ligados, timeout de escrita
     5s**; após escrever, flush + aguarda ~400ms antes de fechar.
- **Plug-and-play:** sem impressora configurada, o sistema **autodetecta** a térmica, com **USB
  prioritário sobre Bluetooth**.

### 9.2 Codepage / acentuação
- Codepage **PC860 (Português)** selecionado no reset.
- **Estratégia de robustez:** **remove acentos antes de imprimir** (á→a, ç→c, ã→a…). Impressoras
  baratas cospem lixo em acentos; ASCII puro sai limpo em qualquer modelo. **A remoção afeta só a
  impressão — a tela mantém acentuação.**

### 9.3 Comandos ESC/POS
| Função | Sequência (hex) |
|---|---|
| Reset / init | `1B 40` (ESC @) |
| Codepage PC860 | `1B 74 02` (ESC t 2) |
| Espaçamento de linha compacto | `1B 33 1D` (ESC 3 29 dots — economiza bobina) |
| Alinhar esquerda / centro | `1B 61 00` / `1B 61 01` |
| Fonte normal | `1B 21 00` |
| Título (altura+largura duplas) | `1B 21 30` |
| Expandida (só altura dupla — cabe nome inteiro) | `1B 21 10` |
| **Corte de papel** | `1D 56 01` (GS V 1 — corte parcial **sem** avanço extra) |

> **Regra crítica de compatibilidade:** usar **`GS V 1`**. A variante com avanço parametrizado
> (`GS V 66 n`) **TRAVA o firmware** do modelo de referência (MPT-II). Cortar logo após a última
> linha; **sem LFs extras no fim** (economiza bobina).
> **Estrutura de um job:** init → codepage → espaçamento; para cada linha aplica o estilo (Título
> = centralizado 2×2; Expandida = altura dupla; Corte = LF + corte; Normal); ao fim, avanço mínimo
> + corte. O estilo "Corte" no meio permite **separar fichas/vias**.

### 9.4 Resiliência (o caixa nunca trava por causa da impressora)
> **Proteger o caixa vale mais que o papel.** Nenhuma falha de hardware pode lançar exceção não
> tratada — toda falha vira `(false, mensagem)`.
1. **Teto de tempo (15s):** se a impressora não responde em 15s, a operação é **abandonada** e a
   venda segue; o cupom pode ser reimpresso.
2. **Venda salva antes de imprimir** (§0.4).
3. **Impressora ausente ≠ com defeito:** sem impressora configurada, o fluxo segue **em silêncio**
   (sem popup Repetir/Ignorar). Com impressora que falhou, oferece **Repetir / Ignorar**.
4. **Limpeza de fila (anti-cupom-fantasma)** — só para fila do SO/USB, antes de cada envio:
   religa a impressora se estiver offline; **remove todos os jobs pré-existentes** desta impressora
   (cada cupom é enviado individualmente e sai na hora — qualquer job remanescente é lixo de
   tentativa anterior, que o SO reprocessaria sozinho e "imprimiria do nada"); **salvaguarda:** se
   sobrar job preso **já em impressão**, **reinicia o serviço de spooler** (único jeito de soltá-lo).
   Best-effort.
5. **Confirmação real no teste:** o cupom de teste **verifica a fila** por ~4s após enviar; se o
   job travar (Error/Offline/PaperOut), retorna a **causa** em vez de um falso "enviado!". Para
   porta serial, o próprio envio é a confirmação.

---

## 10. Matriz de Permissões

Modelo: cada ação tem um flag "**exige senha de admin**" configurável pelo admin (persistido).
Senha inicial padrão = **"0000"** (trocável). Validação por comparação exata.

| Ação | Default | Observações |
|---|---|---|
| Abrir caixa | **Exige senha** | dinheiro/turno |
| Fechar caixa (Leitura Z) | **Exige senha** | dinheiro/turno |
| Trocar operador | **Exige senha** | fecha+abre turno |
| Sangria / Suprimento | **Exige senha** | mexe em dinheiro |
| Gerenciar produtos | **Exige senha** | dados |
| Estornar venda | **Exige senha** | cancela venda |
| Backup / Restauração | **Exige senha** | disaster recovery |
| Balanço Geral / Relatório Gerencial | **Exige senha** | dado gerencial global |
| Exportar (relatórios/CSV/XLSX) | **Exige senha** | dados financeiros saindo |
| **Tela de Permissões** | **Exige senha SEMPRE** | não pode ser desligada |
| Gerenciar categorias | Liberado | config leve |
| Gerenciar promoções/combos | Liberado | config leve |
| Configurar impressora | Liberado | config leve |
| Layout do cupom | Liberado | config leve |

Regras de permissão:
- **Trocar a senha do admin** exige a mesma trava da tela de Permissões (e a senha atual dentro
  da própria tela).
- **Ver** Histórico e Painel em Tempo Real são **livres** (só leitura); é o **estorno** dentro do
  histórico que pede senha.
- Filosofia dos defaults: dinheiro / dados / exclusão / visão gerencial exigem senha; configuração
  leve fica liberada.

---

## 11. Requisitos de Exportação / Relatórios

Conteúdo é montado **independente do formato** (o construtor de relatório é puro; os formatos só
consomem as tabelas). O operador escolhe **seções**, **modo de itens** e **formato**.

**Tabela Resumo:** Período, Nº de vendas, Faturamento bruto, Dinheiro, Pix, Débito, Crédito,
**Canceladas (qtd)** (do conjunto original, não do consolidado), Cortesias (qtd), Cortesias (R$).

**Tabela Vendas — dois modos:**
- *Amontoado* (1 linha/venda): Venda, Data/Hora, Itens (`2x A | 1x B`, só físicos), Total,
  Pagamento, Status, Impressões, Observação.
- *Uma linha por item* (explode, só físicos): Venda, Data/Hora, Item, Qtd, Preço Unit, Subtotal,
  Pagamento, Status, Observação.
- Em ambos, **linhas de desconto são omitidas** das linhas de item, mas o Total é líquido.

**Tabela Itens vendidos:** Produto, Quantidade, Total (agrupa por nome, ignora canceladas/descontos).

**Tabela Preços praticados:** Produto, Preço Unit, Qtd, Total, Obs (marca "PREÇO MUDOU" quando o
produto tem >1 preço).

**Formatos:**
- **CsvUnico:** um arquivo com seções empilhadas (`===== NOME =====`).
- **CsvMultiplos:** um arquivo por seção numa pasta (`prefixo_slug.csv`).
- **XlsxUnico:** uma aba com tudo empilhado.
- **XlsxAbas:** uma aba por seção.
- **CSV:** separador = separador de lista da cultura (fallback `;`); campos com separador/aspas/
  quebra são citados; gravado com **BOM UTF-8** (acento correto na planilha).
- **XLSX:** planilha aberta em qualquer editor de planilhas; **regra de tipagem de célula:** texto
  que parece número é gravado como **numérico** (facilita somas — inclusive Ids e quantidades),
  demais como texto; nome de aba ≤ 31 caracteres, sem `: \ / ? * [ ]`.
- **Nome de arquivo com as datas do período** → nunca sobrescreve.

---

## 12. Regras Implícitas / Ocultas (consolidado — atenção ao reconstruir)

1. **Sentinela de desconto:** desconto de combo não é entidade própria; é reconhecido em **todo o
   sistema** pela dupla `ProdutoId vazio + subtotal negativo`. Toda varredura de itens deve filtrar.
2. **Combo tem preço fixo; composição é decorativa** (não recalcula preço nem baixa estoque). O
   desconto de combo vem de uma **Promoção**, não da composição do Produto.
3. **Motor de preços não é exclusivo:** múltiplas promoções podem usar as mesmas unidades; sem
   reserva/alocação; sem limite de "1 desconto por venda".
4. **Contagem de itens agrupa por NOME; o motor de preços opera por ProdutoId** (inconsistência
   deliberada — relatório de barraca é por nome legível).
5. **Estorno preserva tudo** (itens, impressões): só muda o status. Some dos totais/gaveta/Z, mas
   aparece no export marcado CANCELADA.
6. **Contador de impressões** incrementa por impressão bem-sucedida (antifraude de reimpressão).
7. **Datas ISO como comparação de string:** filtro por período compara texto diretamente
   (`data >= ini AND <= fim`); o `fim` é inclusivo literal — o chamador deve estender ao fim do dia
   (23:59:59.999) se passar só a data.
8. **Migração preserva comportamento antigo** (ex.: "dias da semana" default = todos).
9. **Sem caixa aberto, não se vende;** **um único turno aberto por vez;** **retomada automática**
   do turno aberto após reinício.
10. **Restore preserva Ids** (não duplica/renumera).
11. **Campo Atalho do produto é legado** — navegação por posição (letra+número), ordenação por
    nome.
12. **Rótulo de pagamento no cupom:** cartão genérico e valores não mapeados caem em "CARTAO";
    Débito/Crédito/Cortesia têm rótulos próprios.
13. **Modo demonstração** (variável de ambiente): força janela maximizada e **finge impressão
    bem-sucedida** — só para gravação de vídeo/testes; **não deve afetar produção**.
14. **Combo-espelho:** promoção do tipo Combo cria um produto na categoria "Promoções" com id
    previsível (para upsert idempotente); inativar/excluir a promoção inativa o espelho.

---

## Apêndice A — Glossário

- **Turno / Caixa:** sessão de operação com abertura (fundo) e fechamento (Leitura Z).
- **Total em Gaveta:** dinheiro físico esperado na gaveta = fundo + vendas em dinheiro +
  suprimentos − sangrias.
- **Leitura Z:** relatório de fechamento do turno.
- **Sangria / Suprimento:** retirada / entrada manual de dinheiro na gaveta.
- **Vale / Ficha:** cupom destacável de 1 unidade de um item, entregável na barraca.
- **Cortesia:** entrega de itens sem cobrança (brinde), rastreada pelo nome do beneficiário.
- **Sentinela de desconto:** linha especial de item (ProdutoId vazio + valor negativo) que
  representa um desconto, não um produto.
- **Time travel (gerencial):** capacidade de consultar/exportar vendas de qualquer período pelo
  carimbo de tempo, independente de haver caixa aberto.

---

*Documento gerado por engenharia reversa do sistema. Descreve comportamento e regras — não
tecnologia. Deve permanecer a Fonte Única de Verdade; ao mudar uma regra no software, atualizar
aqui.*
