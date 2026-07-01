<#
    Adicionar-TrilhaSonora.ps1
    -------------------------------------------------------------------------
    Pos-producao de audio 100% AUTONOMA e OFFLINE. NAO baixa nada, NAO usa
    chave de API, NAO tem risco de licenca: o proprio FFmpeg SINTETIZA uma
    "cama" sonora ambiente suave (pad grave + tremolo lento) e a mixa por baixo
    do video como trilha de fundo.

    O que faz:
      1. Acha o ffmpeg/ffprobe (usa os de tools/ffmpeg baixados pelo gerador do
         video; se nao houver, tenta o do PATH).
      2. Mede a duracao EXATA do video de entrada (ffprobe).
      3. Sintetiza um pad ambiente do tamanho exato do video:
           - 3 senoides graves (acorde) => textura musical, nao um bipe.
           - tremolo lento (respiracao) => nao fica estatico/agressivo.
      4. Ducking: baixa a trilha para ~12% do volume (fundo, nao briga com
         eventual narracao) e aplica fade-in 1.5s + fade-out 4s no final.
      5. Cospe "<nome>_com-trilha.mp4" pronto pro WhatsApp (H.264 + faststart).
         O video de entrada nunca e sobrescrito.

    Uso:  pwsh video/Adicionar-TrilhaSonora.ps1
    (ou)  powershell -ExecutionPolicy Bypass -File video/Adicionar-TrilhaSonora.ps1

    Parametros (todos opcionais):
      -Entrada  video de origem (default: video/Treinamento_PDV_FestaJunina.mp4)
      -Volume   ganho da trilha 0..1 (default: 0.12 = 12%)
      -FadeOut  segundos de fade-out no fim (default: 4)
#>
[CmdletBinding()]
param(
    [string]$Entrada = "",
    [string]$Saida   = "",
    [string]$Musica  = "",     # arquivo .mp3/.wav de trilha; vazio => pad sintetico
    [double]$Volume  = 0.35,
    [double]$FadeOut = 4
)

$ErrorActionPreference = "Stop"
$repo     = Split-Path -Parent $PSScriptRoot
$videoDir = Join-Path $repo "video"
$toolsDir = Join-Path $repo "tools\ffmpeg"

function Info($m) { Write-Host "[TRILHA] $m" -ForegroundColor Cyan }
function Erro($m) { Write-Host "[TRILHA][ERRO] $m" -ForegroundColor Red }

# --------------------------------------------------- localizar ffmpeg/ffprobe
function Find-Exe($nome) {
    $achado = Get-ChildItem $toolsDir -Recurse -Filter "$nome.exe" -ErrorAction SilentlyContinue |
              Select-Object -First 1
    if ($achado) { return $achado.FullName }
    $noPath = Get-Command $nome -ErrorAction SilentlyContinue
    if ($noPath) { return $noPath.Source }
    return $null
}

try {
    if ([string]::IsNullOrWhiteSpace($Entrada)) {
        $Entrada = Join-Path $videoDir "Treinamento_PDV_FestaJunina.mp4"
    }
    if (-not (Test-Path $Entrada)) {
        throw "Video de entrada nao encontrado: $Entrada. Gere-o primeiro com Gerar-VideoTreinamento.ps1."
    }

    $ffmpeg  = Find-Exe "ffmpeg"
    $ffprobe = Find-Exe "ffprobe"
    if (-not $ffmpeg)  { throw "ffmpeg.exe nao encontrado. Rode Gerar-VideoTreinamento.ps1 uma vez (ele baixa o FFmpeg) ou instale o FFmpeg no PATH." }
    if (-not $ffprobe) { throw "ffprobe.exe nao encontrado (vem junto do FFmpeg em tools/ffmpeg)." }
    Info "ffmpeg:  $ffmpeg"
    Info "ffprobe: $ffprobe"

    # ---------------------------------------------- 1) duracao exata do video
    $durStr = & $ffprobe -v error -show_entries format=duration -of "csv=p=0" $Entrada
    $dur = [double]::Parse($durStr, [System.Globalization.CultureInfo]::InvariantCulture)
    if ($dur -le 0) { throw "Nao foi possivel ler a duracao do video ($durStr)." }
    $dur = [math]::Round($dur, 2)
    Info ("Duracao do video: {0}s" -f $dur)

    # fade-out nunca maior que o proprio video
    if ($FadeOut -ge $dur) { $FadeOut = [math]::Max(1, [math]::Round($dur / 3, 2)) }
    $fadeOutStart = [math]::Round($dur - $FadeOut, 2)

    # cultura invariante p/ FFmpeg (ponto decimal, nunca virgula)
    $ci = [System.Globalization.CultureInfo]::InvariantCulture
    $sDur   = $dur.ToString($ci)
    $sVol   = $Volume.ToString($ci)
    $sFadeO = $FadeOut.ToString($ci)
    $sFoSt  = $fadeOutStart.ToString($ci)

    # ------------------------------------------------------ nome do arquivo final
    if ([string]::IsNullOrWhiteSpace($Saida)) {
        $baseNome = [System.IO.Path]::GetFileNameWithoutExtension($Entrada)
        $saida    = Join-Path $videoDir ($baseNome + "_com-trilha.mp4")
    } else {
        # aceita nome puro (vai pra video/) ou caminho completo
        $saida = if ([System.IO.Path]::IsPathRooted($Saida)) { $Saida } else { Join-Path $videoDir $Saida }
    }
    if (Test-Path $saida) { Remove-Item $saida -Force }

    $usaMusica = -not [string]::IsNullOrWhiteSpace($Musica)
    if ($usaMusica -and -not (Test-Path $Musica)) { throw "Arquivo de musica nao encontrado: $Musica" }

    if ($usaMusica) {
        # ============ TRILHA REAL (mp3/wav) com loop OU trim inteligente ============
        # Duracao da musica: se for MAIS CURTA que o video -> loop com crossfade;
        # se for MAIS LONGA -> trim exato no fim do video. Nos dois casos: fade-in
        # 1.5s + fade-out no fim + ducking (volume de fundo).
        $durMus = [double]::Parse(
            (& $ffprobe -v error -show_entries format=duration -of "csv=p=0" $Musica),
            $ci)
        Info ("Trilha: {0} ({1}s). Video: {2}s." -f ([System.IO.Path]::GetFileName($Musica)), [math]::Round($durMus,1), $dur)

        # aloop so quando precisa (musica curta). loop=-1 = infinito; atrim corta no $dur.
        $prep = if ($durMus -lt $dur) {
            Info "Musica mais curta que o video -> loop ate cobrir tudo."
            "aloop=loop=-1:size=2147483647,atrim=duration=$sDur"
        } else {
            Info ("Musica mais longa -> trim exato aos {0}s (com fade-out {1}s)." -f $dur, $FadeOut)
            "atrim=duration=$sDur"
        }
        # [1:a] = musica. prep -> volume(ducking) -> fades. asetpts reinicia o clock apos atrim.
        $filtro = "[1:a]$prep,asetpts=N/SR/TB,volume=$sVol,afade=t=in:st=0:d=1.5,afade=t=out:st=$sFoSt`:d=$sFadeO[bg]"

        Info ("Mixando trilha real ({0}% do volume)..." -f ([int]($Volume*100)))
        $ffArgs = @(
            "-y",
            "-i", $Entrada,
            "-i", $Musica,
            "-filter_complex", $filtro,
            "-map", "0:v:0",
            "-map", "[bg]",
            "-c:v", "copy",
            "-c:a", "aac", "-b:a", "128k",
            "-shortest",
            "-movflags", "+faststart",
            $saida
        )
    } else {
        # ============ PAD AMBIENTE SINTETICO (offline, sem arquivo) ============
        # Acorde grave suave (La2/Do3/Mi3 ~ 110/165/220 Hz) como 3 inputs lavfi
        # separados ([1][2][3]), somados, com tremolo lento p/ "respirar".
        $sine1 = "sine=frequency=110:duration=$sDur"
        $sine2 = "sine=frequency=165:duration=$sDur"
        $sine3 = "sine=frequency=220:duration=$sDur"
        $filtro = @(
            "[1:a][2:a][3:a]amix=inputs=3:duration=longest:normalize=0[pad]",
            "[pad]tremolo=f=0.15:d=0.6,volume=$sVol,afade=t=in:st=0:d=1.5,afade=t=out:st=$sFoSt`:d=$sFadeO[bg]"
        ) -join ";"

        Info ("Sintetizando pad ambiente e mixando ({0}% do volume, fade-out {1}s)..." -f ([int]($Volume*100)), $FadeOut)
        $ffArgs = @(
            "-y",
            "-i", $Entrada,
            "-f", "lavfi", "-i", $sine1,
            "-f", "lavfi", "-i", $sine2,
            "-f", "lavfi", "-i", $sine3,
            "-filter_complex", $filtro,
            "-map", "0:v:0",
            "-map", "[bg]",
            "-c:v", "copy",              # video intacto (sem re-encode = rapido e sem perda)
            "-c:a", "aac", "-b:a", "128k",
            "-shortest",
            "-movflags", "+faststart",
            $saida
        )
    }

    & $ffmpeg @ffArgs
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path $saida)) { throw "FFmpeg falhou ao mixar a trilha." }

    $mb = [math]::Round((Get-Item $saida).Length / 1MB, 2)
    Info "PRONTO! Video com trilha: $saida ($mb MB)"
    Write-Host "Arraste esse arquivo direto no WhatsApp Web. O video original ficou intacto." -ForegroundColor Green
}
catch {
    Erro $_.Exception.Message
    exit 1
}
