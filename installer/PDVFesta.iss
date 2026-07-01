; ============================================================================
;  PDVFesta.iss - Instalador do PDV Festa Junina (Inno Setup 6)
;  Gera: Setup_PDVFestaJunina.exe
;
;  Ciclo de vida completo:
;   - Instala o app em %LocalAppData%\Programs\FestaJuninaPDV (SEM exigir admin).
;   - Cria atalhos (Area de Trabalho + Menu Iniciar).
;   - Cria %AppData%\FestaJuninaPDV e a subpasta Backups (dados do SQLite).
;   - No final, roda o instalador do DRIVER da impressora de forma silenciosa,
;     capturando erro sem travar a instalacao do PDV.
;   - Desinstalacao INTELIGENTE: pergunta se mantem o historico/backups.
;
;  A versao vem do build-release.ps1 via /DMyAppVersion=x.y (default abaixo).
; ============================================================================

#ifndef MyAppVersion
  #define MyAppVersion "1.0"
#endif

#define MyAppName "PDV Festa Junina"
#define MyAppExe "PDV-Festa-AMUV.exe"
#define MyPublisher "Familia AMUV"

[Setup]
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyPublisher}
; Instala no perfil do usuario -> nao exige Administrador para o APP.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
DefaultDirName={localappdata}\Programs\FestaJuninaPDV
DisableProgramGroupPage=yes
OutputDir=..\release
OutputBaseFilename=Setup_PDVFestaJunina
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
UninstallDisplayName={#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExe}

[Languages]
Name: "brazilian"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Files]
; App publicado (single-file self-contained) + cardapio.
; Espera-se que o build-release.ps1 tenha gerado a pasta ..\release\PDV-Festa
Source: "..\release\PDV-Festa\{#MyAppExe}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\release\PDV-Festa\cardapio.json"; DestDir: "{app}"; Flags: ignoreversion
; Pacote do driver da impressora (opcional; incluido se existir).
Source: "..\release\PDV-Festa\driver-impressora\*"; DestDir: "{app}\driver-impressora"; \
    Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist
; Manual (opcional)
Source: "..\release\PDV-Festa\MANUAL-OPERADOR.txt"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist

[Dirs]
; Pasta de dados do SQLite + backups, com permissao de escrita para o usuario.
Name: "{userappdata}\FestaJuninaPDV"; Permissions: users-modify
Name: "{userappdata}\FestaJuninaPDV\Backups"; Permissions: users-modify

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExe}"; Tasks: desktopicon
Name: "{autoprograms}\Desinstalar {#MyAppName}"; Filename: "{uninstallexe}"

[Tasks]
Name: "desktopicon"; Description: "Criar atalho na Area de Trabalho"; GroupDescription: "Atalhos:"
Name: "instaldriver"; Description: "Instalar tambem o driver da impressora termica (recomendado)"; GroupDescription: "Impressora:"

[Run]
; 1) Instalador do DRIVER da impressora, silencioso, no fim da instalacao.
;    O instalador do driver e o .exe do pacote da pasta driver-impressora.
;    runasoriginaluser: o driver pode pedir elevacao proprio; erro nao trava o PDV.
Filename: "{app}\driver-impressora\Instalador-Impressora-MPT-II.exe"; \
    Parameters: "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART"; \
    StatusMsg: "Instalando driver da impressora (opcional)..."; \
    Flags: skipifdoesntexist runascurrentuser; Tasks: instaldriver; Check: DriverExiste

; 2) Abrir o PDV ao finalizar (opcional).
Filename: "{app}\{#MyAppExe}"; Description: "Abrir o PDV Festa agora"; \
    Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Remove apenas arquivos gerados na pasta do app (banco fica em %AppData%).
Type: filesandordirs; Name: "{app}\dados"

[Code]
{ ---- Verifica se o instalador do driver existe antes de tentar rodar ---- }
function DriverExiste: Boolean;
begin
  Result := FileExists(ExpandConstant('{app}\driver-impressora\Instalador-Impressora-MPT-II.exe'));
end;

{ ---- Desinstalacao inteligente: pergunta se mantem os dados ---- }
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  DataDir: String;
  Resposta: Integer;
begin
  if CurUninstallStep = usUninstall then
  begin
    DataDir := ExpandConstant('{userappdata}\FestaJuninaPDV');
    if DirExists(DataDir) then
    begin
      Resposta := MsgBox(
        'Deseja MANTER o historico de vendas e os backups salvos neste computador?' + #13#10 + #13#10 +
        'Sim = mantem os dados (para reinstalar depois).' + #13#10 +
        'Nao = apaga tudo (banco de dados e backups).',
        mbConfirmation, MB_YESNO);
      if Resposta = IDNO then
      begin
        DelTree(DataDir, True, True, True);
      end;
    end;
  end;
end;
