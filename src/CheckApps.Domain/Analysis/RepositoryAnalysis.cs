using CheckApps.Domain.Discovery;

namespace CheckApps.Domain.Analysis;

/// <summary>
/// Uma execução de análise sobre um <see cref="Repository"/> (spec 013): snapshot, como o
/// <see cref="QualitySnapshot"/>. A análise **corrente** de um repositório é a mais recente que não falhou;
/// as anteriores ficam como histórico. Só fatos são guardados — nunca o conteúdo dos arquivos lidos.
/// </summary>
public sealed record RepositoryAnalysis
{
    public required string Id { get; init; }
    public required string RepositoryId { get; init; }
    public required DateTimeOffset AnalyzedAt { get; init; }

    /// <summary>Commit do ramo padrão que foi lido. Nulo quando a leitura falhou antes de saber.</summary>
    public string? CommitSha { get; init; }

    /// <summary>Um de <see cref="AnalysisStatuses"/>.</summary>
    public required string Status { get; init; }

    public IReadOnlyList<string> Warnings { get; init; } = [];

    /// <summary>Proveniência: a análise só lê do provedor (<see cref="RepositorySources.GithubScan"/>).</summary>
    public string Source { get; init; } = RepositorySources.GithubScan;
}

/// <summary>
/// Um fato extraído por um scanner numa análise. Neutro à tecnologia: o mesmo registro serve a pacote NuGet,
/// versão de framework e, no futuro, a pacote npm. Toda linha diz de onde veio (<see cref="Origin"/>,
/// scanner e arquivo) e o quanto o CheckApps confia nela (<see cref="Confidence"/>).
/// </summary>
public sealed record RepositoryDependency : IAnalysisFact
{
    public required string Id { get; init; }
    public required string AnalysisId { get; init; }

    /// <summary>Ecossistema do nome: <c>nuget</c> para pacotes, <c>dotnet</c> para o framework.</summary>
    public required string Ecosystem { get; init; }

    /// <summary>Um de <see cref="FactKinds"/>.</summary>
    public required string Kind { get; init; }

    public required string Name { get; init; }

    /// <summary>Versão **declarada** (não resolvida pelo CheckApps). Nula para framework e para pacote sem versão.</summary>
    public string? Version { get; init; }

    /// <summary>Projeto de origem, relativo à raiz do repositório.</summary>
    public required string ProjectPath { get; init; }

    public required string SourceFile { get; init; }

    /// <summary>Um de <see cref="FactOrigins"/>.</summary>
    public string Origin { get; init; } = FactOrigins.Parser;

    /// <summary>Um de <see cref="Confidences"/>.</summary>
    public required string Confidence { get; init; }

    /// <summary>Em texto, o motivo de a confiança não ser alta (ex.: a versão vem de uma propriedade não resolvida).</summary>
    public string? ConfidenceNote { get; init; }

    public required string ScannerId { get; init; }
    public required string ScannerVersion { get; init; }
}

/// <summary>Situação de uma análise. Texto no banco; estes são os valores que o sistema escreve.</summary>
public static class AnalysisStatuses
{
    public const string Ok = "ok";

    /// <summary>Terminou, mas com aviso (arquivo inválido, limite excedido, versão não resolvida, árvore truncada).</summary>
    public const string Partial = "parcial";

    /// <summary>Nenhum scanner se aplica ao repositório (ex.: infra, front-end): não é erro.</summary>
    public const string NoScanner = "sem-scanner";

    /// <summary>Não foi possível ler o repositório. Fica no histórico, mas nunca é a análise corrente.</summary>
    public const string Failed = "falha";
}

/// <summary>O que um fato descreve.</summary>
public static class FactKinds
{
    public const string Package = "package";
    public const string Framework = "framework";
}

/// <summary>De onde veio o fato. Hoje só parser; <c>RULE</c> e <c>AI</c> entram depois, sempre como sugestão (ADR-011).</summary>
public static class FactOrigins
{
    public const string Parser = "PARSER";
}

/// <summary>Confiança em um fato, sempre mostrada em texto (nunca só por cor).</summary>
public static class Confidences
{
    /// <summary>Valor literal no próprio projeto.</summary>
    public const string High = "alta";

    /// <summary>Gerenciamento central, herança, propriedade resolvida, versão flutuante ou intervalo.</summary>
    public const string Medium = "media";

    /// <summary>Texto cru de propriedade não resolvida, ou versão sem forma reconhecida.</summary>
    public const string Low = "baixa";
}

/// <summary>Ecossistemas conhecidos pelo scanner .NET.</summary>
public static class Ecosystems
{
    public const string NuGet = "nuget";
    public const string DotNet = "dotnet";
}
