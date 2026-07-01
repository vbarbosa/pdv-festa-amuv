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
    [int]$DuracaoSegundos = 55,
    [string]$Saida = "Treinamento_PDV_FestaJunina.mp4"
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
        return "drawtext=${ff}text='$texto':fontcolor=yellow:bordercolor=blue:borderw=6:shadowcolor=black:shadowx=3:shadowy=3:fontsize=${size}:x=(w-text_w)/2:y=${y}:enable='between(t\,$ini\,$fim)'"
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
        (Letra "ATALHOS 1 A 9 OU CLIQUE\!" 8 15 70 46),
        (Letra "TROCO AUTOMATICO\!" 16 23 70 54),
        (Letra "VENDA CONCLUIDA\!" 23 26 620 54),
        (Letra "FECHAMENTO BLINDADO\!" 26 40 70 50)
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

    Get-Process -Name "PDV-Festa-AMUV","ffmpeg" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    if (Test-Path $rawFile) { Remove-Item $rawFile -Force }

    Info "Iniciando gravacao de tela ($DuracaoSegundos s)..."
    $recArgs = @("-y","-f","gdigrab","-framerate","15","-t","$DuracaoSegundos","-i","desktop",
                 "-c:v","libx264","-preset","ultrafast","-pix_fmt","yuv420p",$rawFile)
    $rec = Start-Process $ffmpeg -ArgumentList $recArgs -PassThru -WindowStyle Hidden
    Start-Sleep -Seconds 2

    Info "Rodando a demonstracao (FlaUI opera o caixa devagar)..."
    $env:PDV_DEMO = "1"
    & $dotnet test (Join-Path $repo "tests\PdvFesta.E2E\PdvFesta.E2E.csproj") -c Debug --no-build --nologo -v q `
        --filter "FullyQualifiedName~DemoMode" | Out-Null
    $env:PDV_DEMO = $null

    Info "Aguardando a gravacao encerrar..."
    if (-not $rec.HasExited) { $rec.WaitForExit(($DuracaoSegundos + 15) * 1000) | Out-Null }
    Get-Process -Name "PDV-Festa-AMUV" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    if (-not (Test-Path $rawFile)) { throw "A gravacao nao gerou arquivo ($rawFile)." }

    Info "Editando (letreiros WordArt + cartoes + transicoes)..."
    $fonte = Get-Fonte
    $filtro = New-FiltroScript $fonte $DuracaoSegundos
    $filtroFile = Join-Path $env:TEMP "pdv_filtro.txt"
    Set-Content -Path $filtroFile -Value $filtro -Encoding ASCII -NoNewline

    New-Item -ItemType Directory -Force $videoDir | Out-Null
    if (Test-Path $saidaMp4) { Remove-Item $saidaMp4 -Force }
    $edArgs = @("-y","-i",$rawFile,"-filter_complex_script",$filtroFile,"-map","[outv]",
                "-c:v","libx264","-pix_fmt","yuv420p","-crf","27","-preset","medium",
                "-movflags","+faststart",$saidaMp4)
    & $ffmpeg @edArgs
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path $saidaMp4)) { throw "FFmpeg falhou na edicao final." }

    $mb = [math]::Round((Get-Item $saidaMp4).Length / 1MB, 2)
    Info "PRONTO! Video gerado: $saidaMp4 ($mb MB)"
    Write-Host "Arraste esse arquivo direto no WhatsApp Web. Boa festa!" -ForegroundColor Green
}
catch {
    Erro $_.Exception.Message
    exit 1
}
