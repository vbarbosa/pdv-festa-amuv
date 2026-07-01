<#
    Gerar-VideoTreinamento.ps1
    -------------------------------------------------------------------------
    Diretor de cinema autonomo do PDV Festa Junina. Faz TUDO sozinho:
      1. Baixa o FFmpeg (open-source) se nao existir em tools/ffmpeg.
      2. Compila o app (Debug win-x64).
      3. Grava a tela (gdigrab) enquanto o FlaUI (DemoMode) opera o caixa devagar.
      4. Edita com FFmpeg: letreiros WordArt (Impact, amarelo com borda azul),
         cartoes de abertura/encerramento coloridos e transicoes (fade).
      5. Cospe 'Treinamento_PDV_FestaJunina.mp4' otimizado para WhatsApp
         (H.264, yuv420p, 720p, CRF 27, +faststart).

    Uso:  pwsh video/Gerar-VideoTreinamento.ps1
    (ou)  powershell -ExecutionPolicy Bypass -File video/Gerar-VideoTreinamento.ps1
#>
[CmdletBinding()]
param(
    [int]$DuracaoSegundos = 42,
    [string]$Saida = "Treinamento_PDV_FestaJunina.mp4",
    # Trilha sonora de fundo. Default: mp3 real em video/trilha. Se o arquivo nao
    # existir, cai automaticamente no pad ambiente sintetico (offline).
    [string]$Musica = "trilha\trilha-festa.mp3"
)

$ErrorActionPreference = "Stop"
$repo     = Split-Path -Parent $PSScriptRoot            # raiz do repositorio
$videoDir = Join-Path $repo "video"
$toolsDir = Join-Path $repo "tools\ffmpeg"
$rawFile  = Join-Path $env:TEMP "pdv_demo_raw.mkv"
$saidaMp4 = Join-Path $videoDir $Saida
$dotnet   = "C:\Program Files\dotnet\dotnet.exe"
if (-not (Test-Path $dotnet)) { $dotnet = "dotnet" }

function Info($m) { Write-Host "[VIDEO] $m" -ForegroundColor Cyan }
function Erro($m) { Write-Host "[VIDEO][ERRO] $m" -ForegroundColor Red }

# ------------------------------------------------------------------ 1) FFmpeg
function Get-Ffmpeg {
    $exe = Get-ChildItem $toolsDir -Recurse -Filter "ffmpeg.exe" -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($exe) { return $exe.FullName }

    Info "FFmpeg nao encontrado. Baixando (open-source, ~80MB)..."
    New-Item -ItemType Directory -Force $toolsDir | Out-Null
    $zip = Join-Path $env:TEMP "ffmpeg.zip"
    $url = "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip"
    try {
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        Invoke-WebRequest -Uri $url -OutFile $zip -UseBasicParsing
        Expand-Archive $zip $toolsDir -Force
        Remove-Item $zip -Force -ErrorAction SilentlyContinue
    } catch {
        throw "Falha ao baixar o FFmpeg: $($_.Exception.Message). Baixe manualmente e coloque ffmpeg.exe em $toolsDir"
    }
    $exe = Get-ChildItem $toolsDir -Recurse -Filter "ffmpeg.exe" | Select-Object -First 1
    if (-not $exe) { throw "ffmpeg.exe nao encontrado apos extracao." }
    return $exe.FullName
}

# --------------------------------------------------------- fonte WordArt (Impact)
function Get-Fonte {
    foreach ($f in @("impact.ttf","ariblk.ttf","arialbd.ttf","arial.ttf")) {
        $p = Join-Path $env:WINDIR "Fonts\$f"
        if (Test-Path $p) { return ($p -replace '\\','/') -replace ':','\:' }
    }
    return $null
}

# ---------------------------------------------------- filtro (letreiros retro)
function New-FiltroScript($fonte, $dur) {
    # cada letreiro: Impact, amarelo com borda azul grossa, centralizado no topo.
    function Letra($texto, $ini, $fim, $y = 70, $size = 52) {
        $ff = if ($fonte) { "fontfile='$fonte':" } else { "" }
        # escapa ':' no texto (FFmpeg usa ':' como separador de opcoes do drawtext).
        $t = $texto -replace ':', '\:'
        return "drawtext=${ff}text='$t':fontcolor=yellow:bordercolor=blue:borderw=6:shadowcolor=black:shadowx=3:shadowy=3:fontsize=${size}:x=(w-text_w)/2:y=${y}:enable='between(t\,$ini\,$fim)'"
    }
    $fadeOut = [math]::Max(1, $dur - 2)
    $ffi = if ($fonte) { "fontfile='$fonte':" } else { "" }

    # corpo (a gravacao): 720p, fps fixo, fade in/out + letreiros das 4 cenas
    $body = @(
        "[0:v]scale=1280:720:force_original_aspect_ratio=decrease",
        "pad=1280:720:(ow-iw)/2:(oh-ih)/2:color=black",
        "setsar=1,fps=15",
        "fade=t=in:st=0:d=1",
        "fade=t=out:st=${fadeOut}:d=2",
        (Letra "CAIXA LIVRE\!" 3 8 70 60),
        (Letra "TECLADO - CATEGORIA + ITEM\!" 8 19 70 44),
        (Letra "TROCO AUTOMATICO\!" 19 26 70 54),
        (Letra "VENDA CONCLUIDA\!" 26 29 620 54),
        (Letra "FECHAMENTO BLINDADO\!" 30 42 70 50)
    ) -join ","
    $body = "$body[body]"

    # cartao de abertura (roxo) e encerramento (azul) com titulo WordArt
    $intro = "color=c=0x6A0DAD:s=1280x720:d=3,setsar=1,fps=15,drawtext=${ffi}text='PDV FESTA JUNINA':fontcolor=yellow:bordercolor=blue:borderw=8:fontsize=80:x=(w-text_w)/2:y=(h-text_h)/2-40,drawtext=${ffi}text='TREINAMENTO DO CAIXA':fontcolor=white:bordercolor=black:borderw=4:fontsize=40:x=(w-text_w)/2:y=(h-text_h)/2+60[intro]"
    $outro = "color=c=0x001F5B:s=1280x720:d=3,setsar=1,fps=15,drawtext=${ffi}text='BOA FESTA E BOAS VENDAS\!':fontcolor=yellow:bordercolor=blue:borderw=6:fontsize=56:x=(w-text_w)/2:y=(h-text_h)/2[outro]"

    return "$intro;$outro;$body;[intro][body][outro]concat=n=3:v=1:a=0[outv]"
}

# =========================================================================
try {
    Info "Diretorio do video: $videoDir"
    $ffmpeg = Get-Ffmpeg
    Info "FFmpeg: $ffmpeg"

    Info "Compilando o app e o E2E (Debug win-x64) ANTES de gravar (evita tempo morto)..."
    & $dotnet build (Join-Path $repo "src\PdvFesta.App\PdvFesta.App.csproj") -c Debug -r win-x64 --nologo -v q | Out-Null
    & $dotnet build (Join-Path $repo "tests\PdvFesta.E2E\PdvFesta.E2E.csproj") -c Debug --nologo -v q | Out-Null

    $exe = Join-Path $repo "src\PdvFesta.App\bin\Debug\net8.0-windows\win-x64\PDV-Festa-AMUV.exe"
    $db  = Join-Path $env:TEMP ("pdvdemo_" + [guid]::NewGuid().ToString('N') + ".db")
    Get-Process -Name "PDV-Festa-AMUV","ffmpeg" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    if (Test-Path $rawFile) { Remove-Item $rawFile -Force }

    # ABRE O APP ANTES de gravar (banco novo). O app fica em cena a gravacao inteira,
    # entao a gravacao NUNCA mostra o desktop/VS Code.
    # PDV_DEMO=1 ANTES de abrir o app: o app herda a variavel e finge que imprimiu
    # (sem popup de "erro na impressora" - e so uma gravacao, sem hardware).
    Info "Abrindo o app (modo demonstracao)..."
    $env:PDVFESTA_DB = $db
    $env:PDV_DEMO = "1"
    $appProc = Start-Process $exe -PassThru
    Start-Sleep -Seconds 7   # espera a janela do caixa aparecer

    # Bounds da TELA PRIMARIA (onde o app maximiza). Capturamos essa REGIAO do desktop —
    # nao a janela (title=) nem o desktop inteiro. Assim pega o app E OS MODAIS (pagamento,
    # troco, fechamento, senha) que abrem centralizados sobre ele, mas NUNCA a 2a tela.
    Add-Type -AssemblyName System.Windows.Forms
    $tela = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
    # dimensoes PARES (libx264+yuv420p exige); a tela ja costuma ser par, mas garantimos.
    $capW = $tela.Width  - ($tela.Width  % 2)
    $capH = $tela.Height - ($tela.Height % 2)
    Info "Iniciando gravacao ($DuracaoSegundos s) - REGIAO da tela primaria (${capW}x${capH} @ $($tela.X),$($tela.Y))..."

    $recArgs = @("-y","-f","gdigrab","-framerate","15","-t","$DuracaoSegundos",
                 "-offset_x","$($tela.X)","-offset_y","$($tela.Y)",
                 "-video_size","${capW}x${capH}",
                 "-i","desktop",
                 "-c:v","libx264","-preset","ultrafast","-pix_fmt","yuv420p",$rawFile)
    $rec = Start-Process $ffmpeg -ArgumentList $recArgs -PassThru -WindowStyle Hidden
    Start-Sleep -Milliseconds 2000
    $engatou = -not $rec.HasExited
    if (-not $engatou) {
        Info "Captura por regiao nao engatou; usando desktop inteiro como fallback."
        try { if (-not $rec.HasExited) { $rec.Kill() } } catch {}
        $recArgs = @("-y","-f","gdigrab","-framerate","15","-t","$DuracaoSegundos","-i","desktop",
                     "-vf","scale=trunc(iw/2)*2:trunc(ih/2)*2",
                     "-c:v","libx264","-preset","ultrafast","-pix_fmt","yuv420p",$rawFile)
        $rec = Start-Process $ffmpeg -ArgumentList $recArgs -PassThru -WindowStyle Hidden
        Start-Sleep -Milliseconds 800
    }

    Info "Rodando a demonstracao (anexa ao app; sem mouse/teclado fisico)..."
    # PDV_DEMO=1 ja esta no ambiente (setado antes de abrir o app); o dotnet test herda.
    & $dotnet test (Join-Path $repo "tests\PdvFesta.E2E\PdvFesta.E2E.csproj") -c Debug --no-build --nologo -v q `
        --filter "FullyQualifiedName~DemoMode" | Out-Null

    Info "Encerrando gravacao e fechando o app..."
    if (-not $rec.HasExited) { $rec.WaitForExit(($DuracaoSegundos + 10) * 1000) | Out-Null }
    if ($rec -and -not $rec.HasExited) { try { $rec.Kill() } catch {} }
    if ($appProc -and -not $appProc.HasExited) { try { $appProc.Kill() } catch {} }
    Get-Process -Name "PDV-Festa-AMUV","ffmpeg" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    $env:PDV_DEMO = $null   # nao vaza o modo demo pra sessao do usuario
    if (-not (Test-Path $rawFile)) { throw "A gravacao nao gerou arquivo ($rawFile)." }

    Info "Editando (letreiros WordArt + cartoes + transicoes)..."
    $fonte = Get-Fonte
    $filtro = New-FiltroScript $fonte $DuracaoSegundos
    $filtroFile = Join-Path $env:TEMP "pdv_filtro.txt"
    Set-Content -Path $filtroFile -Value $filtro -Encoding ASCII -NoNewline

    New-Item -ItemType Directory -Force $videoDir | Out-Null
    if (Test-Path $saidaMp4) { Remove-Item $saidaMp4 -Force }

    # edita para um MUDO temporario (letreiros + cartoes); a trilha entra no passo seguinte
    $mudo = Join-Path $env:TEMP "pdv_video_mudo.mp4"
    if (Test-Path $mudo) { Remove-Item $mudo -Force }
    $edArgs = @("-y","-i",$rawFile,"-filter_complex_script",$filtroFile,"-map","[outv]",
                "-c:v","libx264","-pix_fmt","yuv420p","-crf","27","-preset","medium",
                "-movflags","+faststart",$mudo)
    & $ffmpeg @edArgs
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path $mudo)) { throw "FFmpeg falhou na edicao final." }

    # ---- trilha sonora: escreve o UNICO arquivo final (mp3 real ou pad sintetico) ----
    $trilhaPath = if ([System.IO.Path]::IsPathRooted($Musica)) { $Musica } else { Join-Path $videoDir $Musica }
    $trilhaArgs = @{ Entrada = $mudo; Saida = $saidaMp4 }
    if (Test-Path $trilhaPath) {
        Info "Adicionando trilha sonora: $([System.IO.Path]::GetFileName($trilhaPath))"
        $trilhaArgs.Musica = $trilhaPath
    } else {
        Info "Trilha nao encontrada em $trilhaPath -> usando pad ambiente sintetico."
    }
    & (Join-Path $videoDir "Adicionar-TrilhaSonora.ps1") @trilhaArgs
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path $saidaMp4)) { throw "Falha ao adicionar a trilha sonora." }
    Remove-Item $mudo -Force -ErrorAction SilentlyContinue

    $mb = [math]::Round((Get-Item $saidaMp4).Length / 1MB, 2)
    Info "PRONTO! Video gerado: $saidaMp4 ($mb MB)"
    Write-Host "Arraste esse arquivo direto no WhatsApp Web. Boa festa!" -ForegroundColor Green
}
catch {
    Erro $_.Exception.Message
    # limpeza garantida: nada de app/ffmpeg travando a tela
    Get-Process -Name "PDV-Festa-AMUV","ffmpeg" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    exit 1
}
