# 01 — Simples (caminho feliz)

Um projeto SDK, `TargetFramework` literal, versões literais. Baseado no exemplo enviado, sem a linha de comentário `//` (que não é XML válido).

| Projeto | Tipo | Nome | Versão | Confiança |
|---|---|---|---|---|
| `src/Meu.App/Meu.App.csproj` | framework | `net6.0` | — | alta |
| `src/Meu.App/Meu.App.csproj` | package | `Newtonsoft.Json` | `13.0.1` | alta |
| `src/Meu.App/Meu.App.csproj` | package | `Serilog` | `2.10.0` | alta |

Análise: `ok`, sem avisos. O item `None Update="appsettings.json"` não gera fato.
