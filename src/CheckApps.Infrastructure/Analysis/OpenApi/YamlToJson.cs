using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace CheckApps.Infrastructure.Analysis.OpenApi;

/// <summary>
/// Converte um YAML de contrato em JSON, para o leitor de OpenAPI não precisar de outro pacote. O conteúdo não é confiável:
/// um apelido de YAML repetido pode multiplicar o documento, por isso há limite de nós e de profundidade.
/// </summary>
internal static partial class YamlToJson
{
    private const int MaxNodes = 500_000;
    private const int MaxDepth = 200;

    /// <summary>Chaves cujo valor é sempre texto: <c>swagger: 2.0</c> e <c>version: 1.10</c>, sem aspas, não são números.</summary>
    private static readonly HashSet<string> TextKeys = new(StringComparer.Ordinal) { "swagger", "openapi", "version" };

    /// <returns>O objeto raiz, ou <c>null</c> se o documento está vazio ou a raiz não é um objeto.</returns>
    /// <exception cref="YamlException">YAML inválido.</exception>
    /// <exception cref="FormatException">Limite excedido ou chave que não é texto.</exception>
    public static JsonObject? Convert(string yaml)
    {
        var stream = new YamlStream();
        stream.Load(new StringReader(yaml));
        if (stream.Documents.Count == 0)
        {
            return null;
        }

        var nodes = 0;
        return ToJson(stream.Documents[0].RootNode, 0, ref nodes, asText: false) as JsonObject;
    }

    private static JsonNode? ToJson(YamlNode node, int depth, ref int nodes, bool asText)
    {
        if (++nodes > MaxNodes || depth > MaxDepth)
        {
            throw new FormatException("O YAML excede o limite de tamanho ou de profundidade.");
        }

        switch (node)
        {
            case YamlMappingNode mapping:
                var result = new JsonObject();
                foreach (var (key, value) in mapping.Children)
                {
                    if (key is not YamlScalarNode { Value: { } name })
                    {
                        throw new FormatException("O YAML tem uma chave que não é texto.");
                    }

                    result[name] = ToJson(value, depth + 1, ref nodes, TextKeys.Contains(name));
                }

                return result;

            case YamlSequenceNode sequence:
                var array = new JsonArray();
                foreach (var item in sequence.Children)
                {
                    array.Add(ToJson(item, depth + 1, ref nodes, asText: false));
                }

                return array;

            case YamlScalarNode scalar:
                return ScalarToJson(scalar, asText);

            default:
                return null;
        }
    }

    private static JsonNode? ScalarToJson(YamlScalarNode scalar, bool asText)
    {
        var value = scalar.Value ?? "";
        if (asText || scalar.Style != ScalarStyle.Plain)
        {
            return JsonValue.Create(value);
        }

        if (value is "" or "~" || value.Equals("null", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (value.Equals("true", StringComparison.OrdinalIgnoreCase))
        {
            return JsonValue.Create(true);
        }

        if (value.Equals("false", StringComparison.OrdinalIgnoreCase))
        {
            return JsonValue.Create(false);
        }

        return JsonNumber().IsMatch(value) ? JsonNode.Parse(value) : JsonValue.Create(value);
    }

    [GeneratedRegex(@"^-?(0|[1-9]\d*)(\.\d+)?([eE][+-]?\d+)?$")]
    private static partial Regex JsonNumber();
}
