# Impressora térmica — USB e Bluetooth

O PDV imprime cupons ESC/POS em impressoras térmicas de 58mm (ex: **MPT-II / POS58**),
por **dois caminhos**, escolhidos automaticamente pelo alvo selecionado em **F12**:

| Conexão | Como aparece no F12 | Como o app imprime |
|---|---|---|
| **USB** (com driver) | Nome da fila (ex: `POS58 Printer`) | Spooler do Windows (`winspool`, RAW) |
| **Bluetooth / serial** | `COM6 (Bluetooth)` / `COM3 (serial)` | Porta serial (`SerialPort`, 9600 8N1) |

O `EscPosPrinter.EnviarRaw` detecta: se o alvo começa com `COMx`, envia pela porta serial;
senão, envia pela fila do Windows. Assim o **mesmo binário** funciona nas duas conexões.

---

## 1. Bluetooth (recomendado para a MPT-II)

A MPT-II costuma parear como **SPP (Serial Port Profile)** — não precisa de driver.

1. **Ligue** a impressora e deixe-a *descobrível*.
2. Windows → *Bluetooth e dispositivos* → **Adicionar dispositivo** → pareie a impressora
   (ex: aparece como `MPT-II_307C`).
3. O Windows cria uma ou mais portas COM Bluetooth. **Descubra qual imprime**: no app,
   **F12** → selecione a porta rotulada `(Bluetooth)` → **Imprimir Teste**. A que sair no
   papel é a certa (no ambiente testado foi a **COM6**).
4. **Salvar**. Pronto — o app lembra a porta.

> Dica: se houver várias portas Bluetooth, teste uma a uma pelo "Imprimir Teste". Só a porta
> de **saída** da impressora aceita a escrita; as demais dão timeout (e o app trata isso sem
> travar).

## 2. USB (com driver)

1. Conecte a impressora por USB e ligue-a.
2. Instale o driver **POS58** (versionado no repositório em [`drivers/POS58`](../drivers/POS58)):
   rode `POS Printer Driver Setup V7.11.exe` (modelo **ZJiang / POS58 10.0.0.6**) e aponte
   para a porta USB da impressora. Isso cria uma **fila de impressão** no Windows.
3. No app, **F12** → selecione a fila (ex: `POS58`) → **Salvar** → **Imprimir Teste**.

## 3. Driver empacotado

O driver correto para USB fica versionado no repositório em **`drivers/POS58/`**
(INF `NEWPOS58.INF`, modelo `ZJiang POS58 10.0.0.6`, DLLs x64/x86/ia64 e o instalador
`POS Printer Driver Setup V7.11.exe`), garantindo uma cópia conhecida-que-funciona junto
do código. Instalação via INF (linha de comando):

```powershell
pnputil /add-driver "drivers\POS58\NEWPOS58.INF" /install
# depois: Painel de Controle -> Dispositivos e Impressoras -> Adicionar impressora
#         -> escolher a porta -> driver "POS58 10.0.0.6"
```

## 4. Layout do cupom

Em **Configurações → Layout do Cupom**: escolha **Recibo Completo** ou **Ficha de Consumo**
(fonte expandida, sem valores) e veja o **preview** antes de gastar bobina. Detalhes de
formatação/ESC-POS: [ADR-0009](adr/0009-cupom-por-modo.md) e [ADR-0003](adr/0003-escpos-raw.md).

## 5. Solução de problemas

| Sintoma | Causa | Ação |
|---|---|---|
| Nenhuma porta `(Bluetooth)` no F12 | Impressora não pareada | Parear no Windows e reabrir o F12 |
| "Falha na porta COMx (timeout)" | Porta errada / impressora desligada/fora de alcance | Testar outra porta; ligar/aproximar |
| Fila USB não aparece | Driver não instalado | Instalar `drivers/POS58` e adicionar a impressora |
| Cupom sai cortado no lado | Bobina não é 58mm/32 col | Usar bobina 58mm |
| Nada imprime, mas "enviado OK" | Bytes bufferizados, impressora sem papel | Repor papel e **Repetir** (a venda já foi salva) |

> Falhas de impressão **nunca** derrubam o caixa: a venda é salva antes de imprimir e o app
> oferece **Repetir/Ignorar** (ver [ADR-0011](adr/0011-blindagem-e-logs.md)).
