# Amostras .NET para a spec 013

Repositórios fictícios, um por pasta, para validar o scanner .NET. Cada pasta tem um `EXPECTED.md` com o resultado esperado. **São fixtures inventadas, não arquivos reais da empresa**: servem para escrever os testes e depois conferir contra um repositório real (`--dry-run`).

| Pasta | O que cobre |
|---|---|
| `01-simples` | Caminho feliz: `TargetFramework` e versões literais |
| `02-gerenciamento-central` | `Directory.Packages.props`, `Directory.Build.props`, `VersionOverride`, herança e pacote sem versão |
| `03-multitarget-versoes-variadas` | `TargetFrameworks`, versão por elemento/propriedade/flutuante/intervalo, `Condition` |
| `04-invalido-e-ruido` | XML inválido, pasta `obj/` ignorada |
| `05-legado-nao-sdk` | Projeto antigo do .NET Framework (`TargetFrameworkVersion`, `packages.config`, namespace MSBuild) |
| `06-sem-dotnet` | Sem scanner aplicável; base do scanner de teste `package.json` |

Este é o lugar definitivo das fixtures (projeto de testes unitários). Os `.csproj` e `.props` daqui não fazem parte da solution e não são compilados. As pastas `obj/` e `bin/` de dentro de `Fixtures/` são dado de teste e estão liberadas no `.gitignore`.
