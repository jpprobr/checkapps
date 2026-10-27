using CheckApps.Application.Analysis;
using CheckApps.Domain.Analysis;
using CheckApps.Infrastructure.Analysis.DotNet;
using CheckApps.UnitTests.TestDoubles;
using static CheckApps.UnitTests.Analysis.FixtureFiles;

namespace CheckApps.UnitTests.Analysis;

/// <summary>
/// O scanner .NET contra as fixtures de <c>Fixtures/Dotnet/</c>: cada teste confere o resultado com a tabela do
/// <c>EXPECTED.md</c> do cenário. Nenhum usa rede.
/// </summary>
public class DotNetScannerTests
{
    private readonly DotNetScanner _scanner = new();

    [Fact]
    public void Simple_ReadsTheFrameworkAndTheLiteralVersions()
    {
        var result = Scan(_scanner, "01-simples");

        Assert.Equal(
            Sorted(
                "src/Meu.App/Meu.App.csproj|framework|net6.0||alta",
                "src/Meu.App/Meu.App.csproj|package|Newtonsoft.Json|13.0.1|alta",
                "src/Meu.App/Meu.App.csproj|package|Serilog|2.10.0|alta"),
            Rows(result));
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void CentralPackageManagement_ResolvesVersionsAndInheritsTheFramework()
    {
        const string api = "src/Empresa.Pagamentos.Api/Empresa.Pagamentos.Api.csproj";
        const string domain = "src/Empresa.Pagamentos.Domain/Empresa.Pagamentos.Domain.csproj";
        const string tests = "tests/Empresa.Pagamentos.Tests/Empresa.Pagamentos.Tests.csproj";

        var result = Scan(_scanner, "02-gerenciamento-central");

        Assert.Equal(
            Sorted(
                $"{api}|framework|net9.0||alta",              // o projeto vale mais que o Directory.Build.props
                $"{api}|package|Serilog|3.1.1|media",         // $(SerilogVersion) resolvida no Directory.Packages.props
                $"{api}|package|Refit|7.2.1|media",
                $"{api}|package|Empresa.Contratos.Cliente|4.2.0|media",
                $"{domain}|framework|net8.0||media",          // herdado
                $"{domain}|package|Newtonsoft.Json|13.0.3|media",
                $"{tests}|framework|net8.0||media",
                $"{tests}|package|xunit|2.9.0|alta",          // VersionOverride literal
                $"{tests}|package|Moq||baixa"),               // sem versão em lugar nenhum
            Rows(result));

        var warning = Assert.Single(result.Warnings);
        Assert.Contains("'Moq'", warning);
        Assert.Contains("não tem versão", warning);
    }

    [Fact]
    public void CentralPackageManagement_ExplainsInTextWhyTheConfidenceIsNotHigh()
    {
        var result = Scan(_scanner, "02-gerenciamento-central");

        var serilog = result.Facts.Single(f => f.Name == "Serilog");
        var inherited = result.Facts.Single(f => f.Kind == FactKinds.Framework && f.ProjectPath.Contains("Domain"));
        var moq = result.Facts.Single(f => f.Name == "Moq");

        Assert.Contains("gerenciamento central", serilog.ConfidenceNote);
        Assert.Equal("Directory.Packages.props", serilog.SourceFile);
        Assert.Contains("Directory.Build.props", inherited.ConfidenceNote);
        Assert.Contains("sem versão", moq.ConfidenceNote);
    }

    [Fact]
    public void MultiTarget_HandlesEveryShapeOfVersion()
    {
        const string project = "src/Empresa.Cliente/Empresa.Cliente.csproj";

        var result = Scan(_scanner, "03-multitarget-versoes-variadas");

        Assert.Equal(
            Sorted(
                $"{project}|framework|net6.0||alta",
                $"{project}|framework|net8.0||alta",
                $"{project}|package|Polly|8.4.0|alta",
                $"{project}|package|Humanizer.Core|2.14.1|alta",                       // <Version> como elemento filho
                $"{project}|package|Microsoft.Extensions.Http|8.0.2|media",            // $(HttpVersion) resolvida no projeto
                $"{project}|package|Microsoft.Extensions.Logging|$(VersaoQueNaoExiste)|baixa",
                $"{project}|package|AutoMapper|13.*|media",                            // flutuante
                $"{project}|package|FluentValidation|[11.0,12.0)|media",               // intervalo
                $"{project}|package|System.Text.Json|6.0.10|alta"),                    // Condition não é avaliada
            Rows(result));

        var warning = Assert.Single(result.Warnings);
        Assert.Contains("VersaoQueNaoExiste", warning);
    }

    [Fact]
    public void InvalidFileAndNoise_AreHandledWithoutLosingTheGoodProject()
    {
        var paths = Paths("04-invalido-e-ruido");
        Assert.Contains("src/Bom/obj/Fantasma.csproj", paths);   // a fixture existe, para o teste de "ignorado" valer

        var result = Scan(_scanner, "04-invalido-e-ruido");

        Assert.Equal(
            Sorted("src/Bom/Bom.csproj|framework|net8.0||alta", "src/Bom/Bom.csproj|package|Dapper|2.1.35|alta"),
            Rows(result));
        Assert.DoesNotContain(result.Facts, f => f.Name == "PacoteFantasma" || f.Name == "net5.0");

        var warning = Assert.Single(result.Warnings);
        Assert.Contains("src/Quebrado/Quebrado.csproj", warning);
        Assert.Contains("XML inválido", warning);
        Assert.DoesNotContain("Este arquivo não é XML", warning);   // aviso nunca traz conteúdo do arquivo
        Assert.DoesNotContain(_scanner.SelectFiles(paths), p => p.Contains("/obj/"));
    }

    [Fact]
    public void Legacy_ReadsTargetFrameworkVersionAndPackagesConfig_DespiteTheMsBuildNamespace()
    {
        const string project = "Legado/Legado.csproj";

        var result = Scan(_scanner, "05-legado-nao-sdk");

        Assert.Equal(
            Sorted(
                $"{project}|framework|net48||alta",
                $"{project}|package|Newtonsoft.Json|12.0.3|alta",   // do packages.config; o Reference/HintPath não duplica
                $"{project}|package|log4net|2.0.15|alta"),
            Rows(result));
        Assert.Empty(result.Warnings);
        Assert.Equal("Legado/packages.config", result.Facts.Single(f => f.Name == "log4net").SourceFile);
    }

    [Fact]
    public void WithoutDotNet_NoScannerApplies()
    {
        var paths = Paths("06-sem-dotnet");

        Assert.False(_scanner.AppliesTo(paths));
        Assert.Empty(_scanner.SelectFiles(paths));
    }

    [Fact]
    public void SelectFiles_ChoosesTheProjects_ThePackagesConfig_AndTheNearestPropsFiles()
    {
        var selected = _scanner.SelectFiles(Paths("02-gerenciamento-central"));

        Assert.Equal(
            [
                "Directory.Build.props",
                "Directory.Packages.props",
                "src/Empresa.Pagamentos.Api/Empresa.Pagamentos.Api.csproj",
                "src/Empresa.Pagamentos.Domain/Empresa.Pagamentos.Domain.csproj",
                "tests/Empresa.Pagamentos.Tests/Empresa.Pagamentos.Tests.csproj",
            ],
            selected);

        Assert.Contains("Legado/packages.config", _scanner.SelectFiles(Paths("05-legado-nao-sdk")));
    }

    [Fact]
    public void TheNearestPropsFileWins_AndTheProjectDirectoryDoesNotLeakToSiblings()
    {
        string[] paths =
        [
            "Directory.Build.props", "a/Directory.Build.props", "a/b/App.csproj", "c/Other.csproj",
        ];
        var files = new Dictionary<string, string>
        {
            ["Directory.Build.props"] = "<Project><PropertyGroup><TargetFramework>net6.0</TargetFramework></PropertyGroup></Project>",
            ["a/Directory.Build.props"] = "<Project><PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup></Project>",
            ["a/b/App.csproj"] = "<Project Sdk=\"Microsoft.NET.Sdk\" />",
            ["c/Other.csproj"] = "<Project Sdk=\"Microsoft.NET.Sdk\" />",
        };

        Assert.Equal(
            ["Directory.Build.props", "a/Directory.Build.props", "a/b/App.csproj", "c/Other.csproj"],
            _scanner.SelectFiles(paths));

        var result = _scanner.Analyze(files);

        Assert.Equal("net8.0", result.Facts.Single(f => f.ProjectPath == "a/b/App.csproj").Name);
        Assert.Equal("net6.0", result.Facts.Single(f => f.ProjectPath == "c/Other.csproj").Name);
    }

    [Fact]
    public void CentralVersions_AreIgnored_WhenCentralManagementIsNotEnabled()
    {
        var result = _scanner.Analyze(new Dictionary<string, string>
        {
            ["Directory.Packages.props"] =
                "<Project><ItemGroup><PackageVersion Include=\"Serilog\" Version=\"3.1.1\" /></ItemGroup></Project>",
            ["App.csproj"] =
                "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup>"
                + "<ItemGroup><PackageReference Include=\"Serilog\" /></ItemGroup></Project>",
        });

        var serilog = result.Facts.Single(f => f.Name == "Serilog");
        Assert.Null(serilog.Version);
        Assert.Equal(Confidences.Low, serilog.Confidence);
    }

    [Fact]
    public void ADuplicatedReference_IsReportedOnce_ButDifferentVersionsInAConditionalGroupAreKept()
    {
        var result = _scanner.Analyze(new Dictionary<string, string>
        {
            ["App.csproj"] =
                "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup>"
                + "<ItemGroup><PackageReference Include=\"Polly\" Version=\"8.4.0\" /><PackageReference Include=\"polly\" Version=\"8.4.0\" /></ItemGroup>"
                + "<ItemGroup Condition=\"'$(X)'=='1'\"><PackageReference Include=\"Polly\" Version=\"7.0.0\" /></ItemGroup></Project>",
        });

        Assert.Equal(
            Sorted("App.csproj|framework|net8.0||alta", "App.csproj|package|Polly|8.4.0|alta", "App.csproj|package|Polly|7.0.0|alta"),
            Rows(result));
    }

    [Fact]
    public void FrameworkFromAnUnresolvedProperty_IsKeptRaw_WithLowConfidenceAndAWarning()
    {
        var result = _scanner.Analyze(new Dictionary<string, string>
        {
            ["App.csproj"] = "<Project><PropertyGroup><TargetFramework>$(Tfm)</TargetFramework></PropertyGroup></Project>",
        });

        var framework = Assert.Single(result.Facts);
        Assert.Equal("$(Tfm)", framework.Name);
        Assert.Equal(Confidences.Low, framework.Confidence);
        Assert.Single(result.Warnings);
    }

    [Fact]
    public void LegacyVersionOutsideThePattern_IsKeptAsDeclared_WithLowConfidence()
    {
        var result = _scanner.Analyze(new Dictionary<string, string>
        {
            ["App.csproj"] = "<Project><PropertyGroup><TargetFrameworkVersion>4.8</TargetFrameworkVersion></PropertyGroup></Project>",
        });

        var framework = Assert.Single(result.Facts);
        Assert.Equal("4.8", framework.Name);
        Assert.Equal(Confidences.Low, framework.Confidence);
        Assert.Contains("vN.N", framework.ConfidenceNote);
    }

    [Fact]
    public void AProjectWithoutAnyFramework_YieldsAWarning_NotAnError()
    {
        var result = _scanner.Analyze(new Dictionary<string, string>
        {
            ["App.csproj"] = "<Project Sdk=\"Microsoft.NET.Sdk\" />",
        });

        Assert.Empty(result.Facts);
        Assert.Contains("nenhum TargetFramework", Assert.Single(result.Warnings));
    }

    [Fact]
    public void AByteOrderMark_DoesNotBreakTheParser()
    {
        var result = _scanner.Analyze(new Dictionary<string, string>
        {
            ["App.csproj"] = "﻿<Project><PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup></Project>",
        });

        Assert.Equal("net8.0", Assert.Single(result.Facts).Name);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void ADocumentWithADtd_IsRefused_AsInvalidXml()
    {
        var result = _scanner.Analyze(new Dictionary<string, string>
        {
            ["App.csproj"] =
                "<!DOCTYPE Project [<!ENTITY x \"boom\">]><Project><PropertyGroup><TargetFramework>&x;</TargetFramework></PropertyGroup></Project>",
        });

        Assert.Empty(result.Facts);
        Assert.Contains("XML inválido", Assert.Single(result.Warnings));
    }

    [Fact]
    public void EveryFact_CarriesNoFileContent_AndTheScannerIdentityIsStable()
    {
        Assert.Equal("dotnet", _scanner.Id);
        Assert.False(string.IsNullOrWhiteSpace(_scanner.Version));

        var result = Scan(_scanner, "02-gerenciamento-central");
        Assert.All(result.Facts, f =>
        {
            Assert.DoesNotContain("<Project", f.Name + f.Version + f.ConfidenceNote);
            Assert.False(string.IsNullOrWhiteSpace(f.SourceFile));
        });
    }

    // ---- Contrato extensível (SC-005) --------------------------------------------------------------------------------

    [Fact]
    public void ASecondScanner_FitsTheSameContract_WithoutChangingTheModel()
    {
        var registry = new ScannerRegistry([_scanner, new PackageJsonTestScanner()]);

        var frontEnd = registry.Applicable(Paths("06-sem-dotnet"));
        var dotnet = registry.Applicable(Paths("01-simples"));

        Assert.Equal(["npm-test"], frontEnd.Select(s => s.Id));
        Assert.Equal(["dotnet"], dotnet.Select(s => s.Id));

        var result = Scan(frontEnd.Single(), "06-sem-dotnet");
        Assert.Equal(
            Sorted(
                "package.json|package|axios|~1.7.2|media",
                "package.json|package|react|^18.3.1|media",
                "package.json|package|react-dom|^18.3.1|media",
                "package.json|package|typescript|5.5.4|alta",
                "package.json|package|vite|^5.4.0|media"),
            Rows(result));
        Assert.All(result.Facts, f => Assert.Equal("npm", f.Ecosystem));
    }
}
