# 02 — Gerenciamento central de pacotes + Directory.Build.props

Cobre `ManagePackageVersionsCentrally`, herança de `TargetFramework`, versão por propriedade definida no próprio `Directory.Packages.props`, `VersionOverride` e pacote sem versão em lugar nenhum.

| Projeto | Tipo | Nome | Versão | Confiança | Como se chega |
|---|---|---|---|---|---|
| `src/Empresa.Pagamentos.Api/…Api.csproj` | framework | `net9.0` | — | alta | declarado no próprio projeto (vale mais que o `Directory.Build.props`) |
| `src/Empresa.Pagamentos.Api/…Api.csproj` | package | `Serilog` | `3.1.1` | média | `PackageVersion` central, que usa `$(SerilogVersion)` resolvida no mesmo arquivo |
| `src/Empresa.Pagamentos.Api/…Api.csproj` | package | `Refit` | `7.2.1` | média | `PackageVersion` central |
| `src/Empresa.Pagamentos.Api/…Api.csproj` | package | `Empresa.Contratos.Cliente` | `4.2.0` | média | `PackageVersion` central (prefixo `Empresa.` = candidato a pacote interno, ainda sem marcação) |
| `src/Empresa.Pagamentos.Domain/…Domain.csproj` | framework | `net8.0` | — | média | herdado do `Directory.Build.props` |
| `src/Empresa.Pagamentos.Domain/…Domain.csproj` | package | `Newtonsoft.Json` | `13.0.3` | média | `PackageVersion` central |
| `tests/Empresa.Pagamentos.Tests/…Tests.csproj` | framework | `net8.0` | — | média | herdado do `Directory.Build.props` |
| `tests/Empresa.Pagamentos.Tests/…Tests.csproj` | package | `xunit` | `2.9.0` | alta | `VersionOverride` literal no projeto |
| `tests/Empresa.Pagamentos.Tests/…Tests.csproj` | package | `Moq` | (sem versão) | baixa | sem `Version`, sem `PackageVersion` central: aviso na análise |

Análise: `parcial` (o `Moq` sem versão gera aviso). `ProjectReference` não gera fato (fora da spec).

**Pacote interno** (FR-021): com `Analysis:InternalPackagePrefixes` = `Empresa.`, `Empresa.Contratos.Cliente` é marcado como interno; os demais não. Sem prefixo configurado, nenhum é marcado.
