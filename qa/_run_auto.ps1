# Wrapper para eu (agente) rodar o orquestrador elevado e capturar a saida de forma
# confiavel num arquivo fixo. Nao faz parte do produto; e util so no modo autonomo.
$ErrorActionPreference = "Continue"
$saida = Join-Path $PSScriptRoot "_ultimo_run.log"
"" | Set-Content $saida
try {
    & (Join-Path $PSScriptRoot "Run-HyperVTests.ps1") *>&1 |
        ForEach-Object { $_ | Out-File -FilePath $saida -Append -Encoding UTF8 }
} catch {
    "ERRO WRAPPER: $($_.Exception.Message)" | Out-File -FilePath $saida -Append -Encoding UTF8
}
"===FIM===" | Out-File -FilePath $saida -Append -Encoding UTF8
