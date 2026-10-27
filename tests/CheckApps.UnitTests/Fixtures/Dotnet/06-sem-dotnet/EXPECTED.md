# 06 — Repositório sem .NET (front-end)

Nenhum `*.csproj`.

- Scanner .NET: **não se aplica**. Análise `sem-scanner`, nenhum fato, sem erro.
- Serve de base para o scanner mínimo de `package.json` **apenas nos testes** (SC-005):

| Tipo | Nome | Versão | Confiança |
|---|---|---|---|
| package (npm) | `react` | `^18.3.1` | média (intervalo) |
| package (npm) | `react-dom` | `^18.3.1` | média |
| package (npm) | `axios` | `~1.7.2` | média |
| package (npm, dev) | `typescript` | `5.5.4` | alta |
| package (npm, dev) | `vite` | `^5.4.0` | média |
