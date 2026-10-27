# 04 — Arquivo inválido e ruído

| Projeto | Tipo | Nome | Versão | Confiança |
|---|---|---|---|---|
| `src/Bom/Bom.csproj` | framework | `net8.0` | — | alta |
| `src/Bom/Bom.csproj` | package | `Dapper` | `2.1.35` | alta |

- `src/Quebrado/Quebrado.csproj` (XML inválido): **aviso** na análise com o caminho do arquivo; nenhum fato dele.
- `src/Bom/obj/Fantasma.csproj`: **ignorado** (pasta `obj/`); `PacoteFantasma` e `net5.0` não aparecem.

Análise: `parcial` (por causa do arquivo inválido). O erro de um arquivo não impede os fatos dos demais.
