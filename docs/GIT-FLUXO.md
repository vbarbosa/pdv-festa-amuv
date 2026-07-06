# Fluxo de Branches (Git) — PDV Festa

Convenção adotada no projeto. **Ninguém commita direto na `main`.**

## As branches

| Branch | Papel | Recebe merge de | Protegida |
|---|---|---|---|
| `main` | Produção. Cada merge vira uma **Release** (tag `vX.Y.Z`) publicada pelo CI. | `develop` (via PR) | ✅ sim |
| `develop` | Integração. Onde as features/fixes se juntam e são testadas antes de virar release. | `feat/*`, `fix/*` (via PR ou merge) | — |
| `feat/<nome>` | Uma **funcionalidade nova** (ex: `feat/cortesia`, `feat/relatorio-gerencial`). | — | — |
| `fix/<nome>` | Uma **correção** (ex: `fix/pagamento-atalho`). | — | — |

## O ciclo

```
feat/xyz  ─┐
fix/abc   ─┴──►  develop  ──(PR + release notes)──►  main  ──►  tag vX.Y.Z (CI publica)
```

1. **Começar um trabalho**: parte da `develop`.
   ```
   git checkout develop && git pull
   git checkout -b feat/nome-curto      # ou fix/nome-curto
   ```
2. **Terminar**: commits pequenos e descritivos; abre PR para `develop`.
3. **Release**: quando `develop` está estável, abre **PR `develop → main`** com as notas da
   versão. Ao mergear, o CI compila, testa e publica a Release **`vX.Y.Z`** (instalador + zip)
   com uma **tag versionada** + a tag móvel `latest`.

## Versionamento (SemVer)

`vMAJOR.MINOR.PATCH` — ver `docs/CHANGELOG.md` (o topo é sempre a próxima versão).
- **MAJOR**: mudança incompatível.
- **MINOR**: recurso novo compatível (ex: 2.4 → **2.5** = módulo gerencial).
- **PATCH**: correção compatível (ex: 2.5.0 → 2.5.1).

## Nomes de commit

Prefixo curto do tipo + escopo:
```
feat(cortesia): forma de pagamento brinde fora da gaveta
fix(pagamento): atalho de forma nao digita a letra no campo
docs: CHANGELOG 2.5.0
ci(release): publica tag versionada
```

## Regras de ouro

- `main` **sempre** deployável; nunca commit direto — só via PR de `develop`.
- Uma branch = um assunto. PR pequeno revisa melhor.
- Tag = foto imutável do que foi pra produção. Nunca mover uma tag versionada.
