# 03 — Vários frameworks e formas de versão

| Projeto | Tipo | Nome | Versão | Confiança | Observação |
|---|---|---|---|---|---|
| `src/Empresa.Cliente/…Cliente.csproj` | framework | `net6.0` | — | alta | uma linha por valor de `TargetFrameworks` |
| idem | framework | `net8.0` | — | alta | |
| idem | package | `Polly` | `8.4.0` | alta | |
| idem | package | `Humanizer.Core` | `2.14.1` | alta | `<Version>` como elemento filho |
| idem | package | `Microsoft.Extensions.Http` | `8.0.2` | média | `$(HttpVersion)` resolvida no próprio projeto |
| idem | package | `Microsoft.Extensions.Logging` | `$(VersaoQueNaoExiste)` | baixa | propriedade não resolvida: texto cru e aviso |
| idem | package | `AutoMapper` | `13.*` | média | flutuante, guardada como declarada |
| idem | package | `FluentValidation` | `[11.0,12.0)` | média | intervalo, guardado como declarado |
| idem | package | `System.Text.Json` | `6.0.10` | alta | `Condition` do `ItemGroup` não é avaliada (limite conhecido) |

Análise: `parcial` (aviso da propriedade não resolvida).
