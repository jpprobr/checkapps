using System.Security.Cryptography;
using System.Text;
using CheckApps.Application.Analysis;
using Repository = CheckApps.Domain.Repository;

namespace CheckApps.Infrastructure.Analysis;

/// <summary>
/// Fonte de demonstração e de teste da análise: serve arquivos fictícios dos repositórios do scan fake
/// (<c>fake-ab1-app-pagamentos</c> e companhia), escolhidos pelo **final do nome**. Cada final mostra uma situação:
/// gerenciamento central de pacotes, vários frameworks, projeto simples, projeto legado, arquivo inválido
/// (análise parcial) e repositórios sem .NET (sem scanner). Sem rede e sem credencial.
///
/// O conjunto completo e comentado vive em <c>tests/CheckApps.UnitTests/Fixtures/Dotnet/</c>; aqui há só o que a
/// demonstração precisa. O commit é derivado do conteúdo, então o mesmo conteúdo é sempre o mesmo commit.
/// </summary>
public class FakeRepositoryContentSource(int revision = 0) : IRepositoryContentSource
{
    public const string ProviderName = "fake";

    public string? ConfigurationProblem => null;

    public bool CanRead(Repository repository) =>
        string.Equals(repository.Provider, ProviderName, StringComparison.OrdinalIgnoreCase);

    public Task<RepositoryTree> ListAsync(Repository repository, CancellationToken cancellationToken = default)
    {
        var files = FilesFor(repository.Name);
        return Task.FromResult(new RepositoryTree(
            ShaOf(files),
            files.Select(f => new RepositoryFile(f.Key, Encoding.UTF8.GetByteCount(f.Value))).ToList()));
    }

    public Task<string?> ReadFileAsync(
        Repository repository, string commitSha, string path, CancellationToken cancellationToken = default) =>
        Task.FromResult(FilesFor(repository.Name).GetValueOrDefault(path));

    private string ShaOf(IReadOnlyDictionary<string, string> files)
    {
        var text = string.Join('\n', files.OrderBy(f => f.Key, StringComparer.Ordinal).Select(f => $"{f.Key}\n{f.Value}")) + $"\nrev:{revision}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..12].ToLowerInvariant();
    }

    private static IReadOnlyDictionary<string, string> FilesFor(string repositoryName)
    {
        foreach (var (suffix, files) in Catalog.OrderByDescending(c => c.Suffix.Length))
        {
            if (repositoryName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return files;
            }
        }

        return new Dictionary<string, string> { ["README.md"] = "# Repositório de demonstração sem projeto .NET." };
    }

    private static readonly (string Suffix, IReadOnlyDictionary<string, string> Files)[] Catalog =
    [
        // Gerenciamento central de pacotes + Directory.Build.props, com um pacote interno (prefixo Empresa.).
        ("app-pagamentos", new Dictionary<string, string>
        {
            ["Directory.Build.props"] = "<Project><PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup></Project>",
            ["Directory.Packages.props"] =
                """
                <Project>
                  <PropertyGroup>
                    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
                    <SerilogVersion>3.1.1</SerilogVersion>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageVersion Include="Serilog" Version="$(SerilogVersion)" />
                    <PackageVersion Include="Refit" Version="7.2.1" />
                    <PackageVersion Include="Empresa.Contratos.Cliente" Version="4.2.0" />
                    <PackageVersion Include="Newtonsoft.Json" Version="13.0.3" />
                  </ItemGroup>
                </Project>
                """,
            ["src/Pagamentos.Api/Pagamentos.Api.csproj"] =
                """
                <Project Sdk="Microsoft.NET.Sdk.Web">
                  <PropertyGroup><TargetFramework>net9.0</TargetFramework></PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="Serilog" />
                    <PackageReference Include="Refit" />
                    <PackageReference Include="Empresa.Contratos.Cliente" />
                  </ItemGroup>
                </Project>
                """,
            ["src/Pagamentos.Domain/Pagamentos.Domain.csproj"] =
                """
                <Project Sdk="Microsoft.NET.Sdk">
                  <ItemGroup><PackageReference Include="Newtonsoft.Json" /></ItemGroup>
                </Project>
                """,
        }),

        // Vários frameworks e formas de versão (flutuante, intervalo, propriedade resolvida e não resolvida).
        ("app-bff-pagamentos", new Dictionary<string, string>
        {
            ["src/Bff/Bff.csproj"] =
                """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFrameworks>net6.0;net8.0</TargetFrameworks>
                    <HttpVersion>8.0.2</HttpVersion>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="Polly" Version="8.4.0" />
                    <PackageReference Include="Microsoft.Extensions.Http" Version="$(HttpVersion)" />
                    <PackageReference Include="Microsoft.Extensions.Logging" Version="$(VersaoQueNaoExiste)" />
                    <PackageReference Include="AutoMapper" Version="13.*" />
                    <PackageReference Include="Empresa.SDK.TokenManager" Version="2.0.0" />
                  </ItemGroup>
                </Project>
                """,
        }),

        // Projeto simples, tudo literal.
        ("app-migrado", new Dictionary<string, string>
        {
            ["src/App/App.csproj"] =
                """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="Newtonsoft.Json" Version="13.0.1" />
                    <PackageReference Include="Serilog" Version="2.10.0" />
                  </ItemGroup>
                </Project>
                """,
        }),

        // .NET Framework antigo: TargetFrameworkVersion e packages.config, com namespace do MSBuild.
        ("app-legado", new Dictionary<string, string>
        {
            ["Legado/Legado.csproj"] =
                """
                <?xml version="1.0" encoding="utf-8"?>
                <Project ToolsVersion="15.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
                  <PropertyGroup><TargetFrameworkVersion>v4.8</TargetFrameworkVersion></PropertyGroup>
                  <ItemGroup><None Include="packages.config" /></ItemGroup>
                </Project>
                """,
            ["Legado/packages.config"] =
                """
                <?xml version="1.0" encoding="utf-8"?>
                <packages>
                  <package id="Newtonsoft.Json" version="12.0.3" targetFramework="net48" />
                  <package id="log4net" version="2.0.15" targetFramework="net48" />
                </packages>
                """,
        }),

        // Um projeto bom e um inválido: a análise fica parcial e o bom continua analisado.
        ("app-faturas", new Dictionary<string, string>
        {
            ["src/Bom/Bom.csproj"] =
                """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup>
                  <ItemGroup><PackageReference Include="Dapper" Version="2.1.35" /></ItemGroup>
                </Project>
                """,
            ["src/Quebrado/Quebrado.csproj"] = "isto não é XML <Project",
        }),

        // Infra e contrato: sem .NET, nenhum scanner se aplica.
        ("infra-fila", new Dictionary<string, string> { ["main.tf"] = "# infraestrutura da fila (demonstração)" }),
        ("infra-conta", new Dictionary<string, string> { ["main.tf"] = "# infraestrutura da conta (demonstração)" }),
        ("contrato-api-pagamentos", new Dictionary<string, string> { ["openapi.yaml"] = "openapi: 3.0.3" }),
    ];
}
