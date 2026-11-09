namespace CheckApps.Domain.Analysis;

/// <summary>
/// Um método e uma rota **declarados** num contrato, vistos numa análise (spec 021). O que está no contrato pode não ser o
/// que a aplicação implementa: <see cref="Mechanism"/> diz de onde veio. Chama-se endpoint, e nunca "operação" como no
/// OpenAPI, porque Operação é termo de negócio. Só guarda a estrutura: nunca exemplos, valores padrão, descrições,
/// <c>servers</c> nem extensões.
/// </summary>
public sealed record RepositoryApiEndpoint : IAnalysisFact
{
    public required string Id { get; init; }
    public required string AnalysisId { get; init; }

    /// <summary>Um de <see cref="ApiMechanisms"/>.</summary>
    public required string Mechanism { get; init; }

    /// <summary>Em maiúsculas (<c>POST</c>).</summary>
    public required string Method { get; init; }

    /// <summary>A rota como está no contrato (<c>/simulacoes/{id}</c>).</summary>
    public required string Path { get; init; }

    /// <summary>A rota com os parâmetros neutralizados (<see cref="ApiPath.KeyOf"/>): identifica o endpoint entre commits.</summary>
    public required string PathKey { get; init; }

    /// <summary>O <c>operationId</c> do contrato, se houver.</summary>
    public string? DeclaredId { get; init; }

    public string? Summary { get; init; }

    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>Esquemas de segurança efetivos, só nome e tipo (<c>oauth (oauth2)</c>); nunca um valor.</summary>
    public IReadOnlyList<string> Security { get; init; } = [];

    public bool Deprecated { get; init; }

    public string? ApiTitle { get; init; }

    public string? ApiVersion { get; init; }

    public required string SourceFile { get; init; }

    /// <summary>Um de <see cref="FactOrigins"/>.</summary>
    public string Origin { get; init; } = FactOrigins.Parser;

    /// <summary>Um de <see cref="Confidences"/>.</summary>
    public required string Confidence { get; init; }

    /// <summary>Em texto, o motivo de a confiança não ser alta.</summary>
    public string? ConfidenceNote { get; init; }

    public required string ScannerId { get; init; }
    public required string ScannerVersion { get; init; }

    public IReadOnlyList<RepositoryApiField> Fields { get; init; } = [];
}

/// <summary>Um campo achatado de um endpoint (<c>cliente.contas[].numero</c>). Identificado por lugar, status, mídia e caminho.</summary>
public sealed record RepositoryApiField
{
    public required string Id { get; init; }
    public required string EndpointId { get; init; }

    /// <summary>Um de <see cref="ApiFieldLocations"/>.</summary>
    public required string Location { get; init; }

    /// <summary>Status HTTP da resposta; nulo nos demais lugares.</summary>
    public string? StatusCode { get; init; }

    /// <summary>Tipo de mídia do pedido ou da resposta; nulo nos parâmetros.</summary>
    public string? MediaType { get; init; }

    public required string Path { get; init; }

    public string? Type { get; init; }

    public string? Format { get; init; }

    public bool Required { get; init; }

    public IReadOnlyList<string> EnumValues { get; init; } = [];

    /// <summary>Em texto, o que é preciso saber sobre o campo (variante, referência não lida, esquema recursivo).</summary>
    public string? Note { get; init; }
}

/// <summary>De onde veio o endpoint. Hoje só contrato declarado; a leitura do código entra depois, para comparar os dois.</summary>
public static class ApiMechanisms
{
    public const string OpenApi = "openapi";
}

/// <summary>Onde o campo fica. Texto no banco.</summary>
public static class ApiFieldLocations
{
    public const string Path = "path";
    public const string Query = "query";
    public const string Header = "header";
    public const string Cookie = "cookie";
    public const string Request = "pedido";
    public const string Response = "resposta";
}

public static partial class ApiPath
{
    /// <summary>A rota com os parâmetros neutralizados: <c>/a/{id}</c> e <c>/a/{fundoId}</c> são o mesmo endpoint.</summary>
    public static string KeyOf(string path) => Parameter().Replace(path, "{}");

    /// <summary>A posição do parâmetro na rota, a partir de zero; -1 se a rota não o declara.</summary>
    public static int ParameterPosition(string path, string parameter)
    {
        var matches = Parameter().Matches(path);
        for (var i = 0; i < matches.Count; i++)
        {
            if (matches[i].Value == $"{{{parameter}}}")
            {
                return i;
            }
        }

        return -1;
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"\{[^}]*\}")]
    private static partial System.Text.RegularExpressions.Regex Parameter();
}
