# 05 — Projeto legado (.NET Framework, fora do formato SDK)

Projeto antigo: `TargetFrameworkVersion` (`v4.8`), `Reference` com `HintPath` e `packages.config` em vez de `PackageReference`. O `.csproj` declara o namespace MSBuild (`xmlns`), então o parser compara pelo nome local dos elementos.

Decisão do usuário (2026-10-24): **este formato é lido**.

| Projeto | Tipo | Nome | Versão | Confiança | Como se chega |
|---|---|---|---|---|---|
| `Legado/Legado.csproj` | framework | `net48` | — | alta | `TargetFrameworkVersion` `v4.8` normalizado |
| `Legado/Legado.csproj` | package | `Newtonsoft.Json` | `12.0.3` | alta | `Legado/packages.config` |
| `Legado/Legado.csproj` | package | `log4net` | `2.0.15` | alta | `Legado/packages.config` |

- O `Reference Include="Newtonsoft.Json, Version=12.0.0.0, ..."` com `HintPath` **não** gera fato: o pacote já vem do `packages.config`, e ler as duas fontes duplicaria.
- Normalização: `v4.8` → `net48`, `v4.7.2` → `net472`, `v4.0` → `net40`; fora do padrão, fica como declarado com confiança baixa.

Análise: `ok`.
