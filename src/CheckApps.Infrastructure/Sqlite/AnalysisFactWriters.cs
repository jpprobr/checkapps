using System.Data;
using CheckApps.Domain.Analysis;
using Dapper;

namespace CheckApps.Infrastructure.Sqlite;

/// <summary>
/// Grava um tipo de fato na transação da análise (spec 021). Um tipo novo é um escritor novo, sem mudar o
/// <see cref="AnalysisRepository"/>. As consultas de leitura continuam no repositório, específicas de cada tipo.
/// </summary>
public interface IAnalysisFactWriter
{
    Type FactType { get; }

    Task WriteAsync(
        IDbConnection connection, IDbTransaction transaction, IReadOnlyList<IAnalysisFact> facts, CancellationToken cancellationToken);
}

public sealed class DependencyFactWriter : IAnalysisFactWriter
{
    public Type FactType => typeof(RepositoryDependency);

    public async Task WriteAsync(
        IDbConnection connection, IDbTransaction transaction, IReadOnlyList<IAnalysisFact> facts, CancellationToken cancellationToken)
    {
        foreach (var fact in facts)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO repository_dependencies
                    (id, analysis_id, ecosystem, kind, name, version, project_path, source_file, origin,
                     confidence, confidence_note, scanner_id, scanner_version)
                VALUES
                    (@Id, @AnalysisId, @Ecosystem, @Kind, @Name, @Version, @ProjectPath, @SourceFile, @Origin,
                     @Confidence, @ConfidenceNote, @ScannerId, @ScannerVersion)
                """,
                (RepositoryDependency)fact, transaction, cancellationToken: cancellationToken));
        }
    }
}

/// <summary>Endpoints declarados e seus campos (spec 021). Listas são guardadas como texto, um item por linha, como os avisos da análise.</summary>
public sealed class ApiContractFactWriter : IAnalysisFactWriter
{
    public Type FactType => typeof(RepositoryApiEndpoint);

    public async Task WriteAsync(
        IDbConnection connection, IDbTransaction transaction, IReadOnlyList<IAnalysisFact> facts, CancellationToken cancellationToken)
    {
        foreach (var endpoint in facts.Cast<RepositoryApiEndpoint>())
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO repository_api_endpoints
                    (id, analysis_id, mechanism, method, path, path_key, declared_id, summary, tags, security, deprecated,
                     api_title, api_version, source_file, origin, confidence, confidence_note, scanner_id, scanner_version)
                VALUES
                    (@Id, @AnalysisId, @Mechanism, @Method, @Path, @PathKey, @DeclaredId, @Summary, @Tags, @Security, @Deprecated,
                     @ApiTitle, @ApiVersion, @SourceFile, @Origin, @Confidence, @ConfidenceNote, @ScannerId, @ScannerVersion)
                """,
                new
                {
                    endpoint.Id, endpoint.AnalysisId, endpoint.Mechanism, endpoint.Method, endpoint.Path, endpoint.PathKey,
                    endpoint.DeclaredId, endpoint.Summary, Tags = Lines(endpoint.Tags), Security = Lines(endpoint.Security),
                    Deprecated = endpoint.Deprecated ? 1 : 0, endpoint.ApiTitle, endpoint.ApiVersion, endpoint.SourceFile,
                    endpoint.Origin, endpoint.Confidence, endpoint.ConfidenceNote, endpoint.ScannerId, endpoint.ScannerVersion
                },
                transaction, cancellationToken: cancellationToken));

            foreach (var field in endpoint.Fields)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    """
                    INSERT INTO repository_api_fields
                        (id, endpoint_id, location, status_code, media_type, path, type, format, required, enum_values, note)
                    VALUES
                        (@Id, @EndpointId, @Location, @StatusCode, @MediaType, @Path, @Type, @Format, @Required, @EnumValues, @Note)
                    """,
                    new
                    {
                        field.Id, field.EndpointId, field.Location, field.StatusCode, field.MediaType, field.Path, field.Type,
                        field.Format, Required = field.Required ? 1 : 0, EnumValues = Lines(field.EnumValues), field.Note
                    },
                    transaction, cancellationToken: cancellationToken));
            }
        }
    }

    private static string? Lines(IReadOnlyList<string> values) => values.Count == 0 ? null : string.Join('\n', values);
}

public sealed class IntegrationFactWriter : IAnalysisFactWriter
{
    public Type FactType => typeof(RepositoryIntegration);

    public async Task WriteAsync(
        IDbConnection connection, IDbTransaction transaction, IReadOnlyList<IAnalysisFact> facts, CancellationToken cancellationToken)
    {
        foreach (var fact in facts)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO repository_integrations
                    (id, analysis_id, mechanism, scheme, host, port, base_path, reference, config_key, environment,
                     environment_kind, state, project_path, source_file, origin, confidence, confidence_note,
                     scanner_id, scanner_version)
                VALUES
                    (@Id, @AnalysisId, @Mechanism, @Scheme, @Host, @Port, @BasePath, @Reference, @ConfigKey, @Environment,
                     @EnvironmentKind, @State, @ProjectPath, @SourceFile, @Origin, @Confidence, @ConfidenceNote,
                     @ScannerId, @ScannerVersion)
                """,
                (RepositoryIntegration)fact, transaction, cancellationToken: cancellationToken));
        }
    }
}
