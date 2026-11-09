using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using CheckApps.Application.Analysis;
using CheckApps.Domain.Analysis;
using Microsoft.OpenApi;
using Microsoft.OpenApi.Reader;
using YamlDotNet.Core;

namespace CheckApps.Infrastructure.Analysis.OpenApi;

/// <summary>
/// Lê contratos OpenAPI 3.x e Swagger 2.0, em YAML ou JSON (spec 021). Os candidatos vêm do nome do arquivo, e quem confirma
/// é o conteúdo. É parser puro: `$ref` para outro arquivo nunca é buscado, e o conteúdo é tratado como não confiável (limites
/// de profundidade, de campos e de tamanho). Só devolve a estrutura: exemplos, valores padrão, descrições, <c>servers</c>
/// e extensões nunca saem daqui.
/// </summary>
public sealed partial class OpenApiScanner(AnalysisOptions? options = null) : IRepositoryScanner
{
    private const int MaxDepth = 12;
    private const int MaxFieldsPerEndpoint = 2000;
    private const int MaxEnumValues = 100;

    /// <summary>O caminho de um corpo que não é um objeto com propriedades (lista ou valor simples).</summary>
    public const string BodyPath = "(corpo)";

    private static readonly string[] Extensions = [".yaml", ".yml", ".json"];

    private static readonly HashSet<string> IgnoredFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin", "obj", "node_modules", ".git", ".github", ".vs", "dist"
    };

    private static readonly string[] IgnoredFilePrefixes = ["appsettings", "package", "tsconfig", "launchsettings", "global."];

    private readonly (Regex[] Files, HashSet<string> Folders) _patterns = ParsePatterns(
        (options ?? new AnalysisOptions()).EffectiveOpenApiFilePatterns);

    public string Id => "openapi";

    public string Version => "1.0.0";

    public bool AppliesTo(IReadOnlyList<string> paths) => paths.Any(IsCandidate);

    public IReadOnlyList<string> SelectFiles(IReadOnlyList<string> paths) =>
        paths.Where(IsCandidate).Order(StringComparer.Ordinal).ToList();

    public ScannerResult Analyze(IReadOnlyDictionary<string, string> files)
    {
        var context = new FileRun();

        foreach (var (path, text) in files.OrderBy(f => f.Key, StringComparer.Ordinal))
        {
            try
            {
                AnalyzeFile(path, text, context);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Só o tipo da exceção: a mensagem de um leitor pode trazer um trecho do arquivo.
                context.Warnings.Add($"{path}: não foi possível ler o contrato ({ex.GetType().Name}); arquivo ignorado.");
            }
        }

        var notes = new List<string>();
        if (context.Ignored > 0)
        {
            notes.Add($"{context.Ignored} arquivo(s) com nome de contrato ignorado(s): não têm openapi nem swagger na raiz.");
        }

        if (context.RecursiveEndpoints > 0)
        {
            notes.Add($"{context.RecursiveEndpoints} endpoint(s) com esquema recursivo: a parte que se repete não foi expandida.");
        }

        return new ScannerResult([], context.Warnings.Distinct().ToList(), notes: notes, outputs: context.Endpoints);
    }

    private sealed class FileRun
    {
        public List<ScannerApiEndpoint> Endpoints { get; } = [];
        public List<string> Warnings { get; } = [];
        public int Ignored { get; set; }
        public int RecursiveEndpoints { get; set; }
    }

    private static void AnalyzeFile(string path, string text, FileRun run)
    {
        // Filtro barato antes de interpretar: um template de Helm com nome de contrato não é contrato, nem um aviso.
        if (!RootKeyHint().IsMatch(text))
        {
            run.Ignored++;
            return;
        }

        JsonObject? root;
        try
        {
            root = path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                ? JsonNode.Parse(text, null, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }) as JsonObject
                : YamlToJson.Convert(text);
        }
        catch (Exception ex) when (ex is JsonException or YamlException or FormatException)
        {
            run.Warnings.Add($"{path}: o arquivo não pôde ser interpretado; ignorado.");
            return;
        }

        if (root is null || !(root.ContainsKey("openapi") || root.ContainsKey("swagger")))
        {
            run.Ignored++;
            return;
        }

        var read = OpenApiDocument.Parse(root.ToJsonString(), "json", new OpenApiReaderSettings
        {
            LoadExternalRefs = false,
            DefaultContentType = ["application/json"]
        });

        if (read.Diagnostic is { Errors.Count: > 0 } diagnostic)
        {
            // Só a contagem: a mensagem do leitor pode trazer valores do arquivo.
            run.Warnings.Add($"{path}: o contrato tem {diagnostic.Errors.Count} erro(s) de leitura; só o que foi possível ler foi gravado.");
        }

        if (read.Document?.Paths is not { } paths)
        {
            return;
        }

        foreach (var (route, item) in paths)
        {
            foreach (var (method, operation) in item.Operations ?? new Dictionary<HttpMethod, OpenApiOperation>())
            {
                run.Endpoints.Add(ReadEndpoint(path, route, method, item, operation, read.Document, run));
            }
        }
    }

    private static ScannerApiEndpoint ReadEndpoint(
        string path, string route, HttpMethod method, IOpenApiPathItem item, OpenApiOperation operation, OpenApiDocument document, FileRun run)
    {
        var collector = new FieldCollector();

        foreach (var parameter in (item.Parameters ?? []).Concat(operation.Parameters ?? []))
        {
            collector.AddParameter(parameter);
        }

        if (operation.RequestBody?.Content is { } requestContent)
        {
            foreach (var (mediaType, content) in requestContent)
            {
                collector.AddBody(ApiFieldLocations.Request, null, mediaType, content.Schema);
            }
        }

        if (operation.Responses is { } responses)
        {
            foreach (var (status, response) in responses)
            {
                if (response.Content is { } responseContent)
                {
                    foreach (var (mediaType, content) in responseContent)
                    {
                        collector.AddBody(ApiFieldLocations.Response, status, mediaType, content.Schema);
                    }
                }
            }
        }

        var label = $"{path}: {method.Method.ToUpperInvariant()} {route}";
        if (collector.HasExternalReference)
        {
            run.Warnings.Add($"{label}: há referência a outro arquivo ou esquema não encontrado; essa parte não foi lida.");
        }

        if (collector.Truncated)
        {
            run.Warnings.Add($"{label}: esquema além do limite de {MaxDepth} níveis ou {MaxFieldsPerEndpoint} campos; o restante não foi lido.");
        }

        if (collector.HasRecursion)
        {
            run.RecursiveEndpoints++;
        }

        var confidence = collector.HasExternalReference || collector.Truncated || collector.HasVariants
            ? Confidences.Medium
            : Confidences.High;

        return new ScannerApiEndpoint(
            ApiMechanisms.OpenApi, method.Method.ToUpperInvariant(), route, operation.OperationId, operation.Summary,
            operation.Tags?.Select(t => t.Name).OfType<string>().ToList() ?? [],
            SecurityOf(operation, document), operation.Deprecated, document.Info?.Title, document.Info?.Version, path,
            confidence, confidence == Confidences.High ? null : collector.ConfidenceNote, collector.Fields);
    }

    /// <summary>O endpoint herda a segurança do documento quando não declara a dele.</summary>
    private static IReadOnlyList<string> SecurityOf(OpenApiOperation operation, OpenApiDocument document) =>
        (operation.Security ?? document.Security ?? [])
            .SelectMany(requirement => requirement.Keys)
            .Select(scheme => scheme.Type is { } type
                ? $"{scheme.Reference?.Id} ({type.ToString().ToLowerInvariant()})"
                : scheme.Reference?.Id ?? "")
            .Where(label => label.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();

    private bool IsCandidate(string path)
    {
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            return false;
        }

        var file = segments[^1];
        var folders = segments[..^1];

        if (!Extensions.Any(e => file.EndsWith(e, StringComparison.OrdinalIgnoreCase))
            || folders.Any(IgnoredFolders.Contains)
            || IgnoredFilePrefixes.Any(p => file.StartsWith(p, StringComparison.OrdinalIgnoreCase))
            || file.EndsWith(".schema.json", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return _patterns.Files.Any(pattern => pattern.IsMatch(file)) || folders.Any(_patterns.Folders.Contains);
    }

    private static (Regex[] Files, HashSet<string> Folders) ParsePatterns(IEnumerable<string> patterns)
    {
        var files = new List<Regex>();
        var folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pattern in patterns.Select(p => p.Trim()).Where(p => p.Length > 0))
        {
            if (pattern.EndsWith('/'))
            {
                folders.Add(pattern.TrimEnd('/'));
            }
            else
            {
                files.Add(new Regex(
                    "^" + Regex.Escape(pattern).Replace(@"\*", ".*") + "$",
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking));
            }
        }

        return (files.ToArray(), folders);
    }

    [GeneratedRegex("""(^|[{,\s])["']?(openapi|swagger)["']?\s*:""", RegexOptions.Multiline)]
    private static partial Regex RootKeyHint();

    /// <summary>Achata os esquemas de um endpoint em campos, parando em ciclos e nos limites.</summary>
    private sealed class FieldCollector
    {
        private const string VariantNote = "varia conforme a variante (oneOf/anyOf); a obrigatoriedade não é garantida.";
        private const string ExternalNote = "referência a outro arquivo ou esquema não encontrado; não lida.";
        private const string RecursiveNote = "esquema recursivo; não expandido.";

        private static readonly IReadOnlySet<string> NoAncestors = new HashSet<string>();

        private readonly List<ScannerApiField> _fields = [];
        private readonly Dictionary<(string Location, string? Status, string? Media, string Path), int> _indexes = [];

        public IReadOnlyList<ScannerApiField> Fields => _fields;
        public bool HasExternalReference { get; private set; }
        public bool HasRecursion { get; private set; }
        public bool HasVariants { get; private set; }
        public bool Truncated { get; private set; }

        public string ConfidenceNote => HasExternalReference || Truncated
            ? "parte do esquema não foi lida; ver os avisos da análise."
            : "há variantes (oneOf/anyOf): nem todo campo existe em todo caso.";

        public void AddParameter(IOpenApiParameter parameter)
        {
            var location = parameter.In switch
            {
                ParameterLocation.Path => ApiFieldLocations.Path,
                ParameterLocation.Query => ApiFieldLocations.Query,
                ParameterLocation.Header => ApiFieldLocations.Header,
                ParameterLocation.Cookie => ApiFieldLocations.Cookie,
                _ => null
            };

            if (location is null || string.IsNullOrEmpty(parameter.Name))
            {
                return;
            }

            AddField(new Scope(location, null, null), parameter.Name, parameter.Schema, parameter.Required, null, 0, NoAncestors);
        }

        public void AddBody(string location, string? status, string mediaType, IOpenApiSchema? schema)
        {
            if (schema is null)
            {
                return;
            }

            var scope = new Scope(location, status, mediaType);
            var merged = Merge(schema, NoAncestors);

            if (merged.IsObjectWithFields)
            {
                Walk(merged, "", scope, null, 0);
            }
            else
            {
                AddMerged(scope, BodyPath, merged, required: false, note: null, depth: 0, childPrefix: "[]");
            }
        }

        private sealed record Scope(string Location, string? Status, string? Media);

        /// <summary>Um esquema com o <c>allOf</c> unido e as referências resolvidas.</summary>
        private sealed class MergedSchema
        {
            public Dictionary<string, IOpenApiSchema> Properties { get; } = new(StringComparer.Ordinal);
            public HashSet<string> Required { get; } = new(StringComparer.Ordinal);
            public List<IOpenApiSchema> Variants { get; } = [];
            public IOpenApiSchema? Items { get; set; }
            public JsonSchemaType? Type { get; set; }
            public string? Format { get; set; }
            public List<string> EnumValues { get; } = [];
            public bool Recursive { get; set; }
            public bool External { get; set; }

            /// <summary>Os esquemas nomeados por onde esta descida já passou: rever um deles é um ciclo.</summary>
            public HashSet<string> Ancestors { get; init; } = new(StringComparer.Ordinal);

            public bool IsObjectWithFields => Properties.Count > 0 || Variants.Count > 0;

            public string? TypeLabel(Func<IOpenApiSchema, MergedSchema, string?> itemLabel)
            {
                if (Items is not null)
                {
                    return $"array<{itemLabel(Items, this) ?? "?"}>";
                }

                return LabelOf(Type) ?? (IsObjectWithFields ? "object" : null);
            }
        }

        private static string? LabelOf(JsonSchemaType? type)
        {
            if (type is not { } value)
            {
                return null;
            }

            var withoutNull = value & ~JsonSchemaType.Null;
            return withoutNull == 0 ? "null" : withoutNull.ToString().ToLowerInvariant();
        }

        private MergedSchema Merge(IOpenApiSchema schema, IReadOnlySet<string> ancestors)
        {
            var merged = new MergedSchema { Ancestors = new HashSet<string>(ancestors, StringComparer.Ordinal) };
            MergeInto(merged, schema, ancestors, new HashSet<string>(StringComparer.Ordinal));
            return merged;
        }

        private void MergeInto(MergedSchema merged, IOpenApiSchema schema, IReadOnlySet<string> ancestors, HashSet<string> inThisMerge)
        {
            if (schema is OpenApiSchemaReference reference)
            {
                var id = reference.Reference?.Id;

                if (reference.Reference?.ExternalResource is not null || reference.RecursiveTarget is null)
                {
                    merged.External = true;
                    HasExternalReference = true;
                    return;
                }

                if (id is not null)
                {
                    if (ancestors.Contains(id))
                    {
                        merged.Recursive = true;
                        HasRecursion = true;
                        return;
                    }

                    // Dois caminhos do mesmo allOf para o mesmo esquema não são um ciclo: basta unir uma vez.
                    if (!inThisMerge.Add(id))
                    {
                        return;
                    }

                    merged.Ancestors.Add(id);
                }
            }

            merged.Type ??= schema.Type;
            merged.Format ??= schema.Format;
            merged.Items ??= schema.Items;

            if (schema.Enum is { } values && merged.EnumValues.Count == 0)
            {
                merged.EnumValues.AddRange(values.Where(v => v is not null).Take(MaxEnumValues).Select(v => v.ToString()));
            }

            foreach (var name in schema.Required ?? new HashSet<string>())
            {
                merged.Required.Add(name);
            }

            foreach (var (name, property) in schema.Properties ?? new Dictionary<string, IOpenApiSchema>())
            {
                merged.Properties.TryAdd(name, property);
            }

            foreach (var member in schema.AllOf ?? [])
            {
                MergeInto(merged, member, ancestors, inThisMerge);
            }

            merged.Variants.AddRange(schema.OneOf ?? []);
            merged.Variants.AddRange(schema.AnyOf ?? []);
        }

        private void Walk(MergedSchema merged, string prefix, Scope scope, string? inheritedNote, int depth)
        {
            foreach (var (name, property) in merged.Properties)
            {
                var path = prefix.Length == 0 ? name : $"{prefix}.{name}";
                var required = inheritedNote is null && merged.Required.Contains(name);
                AddField(scope, path, property, required, inheritedNote, depth + 1, merged.Ancestors);
            }

            foreach (var variant in merged.Variants)
            {
                HasVariants = true;
                if (depth + 1 > MaxDepth)
                {
                    Truncated = true;
                    return;
                }

                Walk(Merge(variant, merged.Ancestors), prefix, scope, VariantNote, depth + 1);
            }
        }

        private void AddField(
            Scope scope, string path, IOpenApiSchema? schema, bool required, string? note, int depth, IReadOnlySet<string> ancestors)
        {
            if (schema is null)
            {
                Add(new ScannerApiField(scope.Location, scope.Status, scope.Media, path, null, null, required, [], note));
                return;
            }

            AddMerged(scope, path, Merge(schema, ancestors), required, note, depth, childPrefix: $"{path}[]");
        }

        private void AddMerged(Scope scope, string path, MergedSchema merged, bool required, string? note, int depth, string childPrefix)
        {
            var fieldNote = merged.External ? ExternalNote : merged.Recursive ? RecursiveNote : note;
            var type = merged.TypeLabel((items, parent) =>
            {
                var item = Merge(items, parent.Ancestors);
                return LabelOf(item.Type) ?? (item.IsObjectWithFields ? "object" : null);
            });

            if (!Add(new ScannerApiField(
                    scope.Location, scope.Status, scope.Media, path, type, merged.Format, required, merged.EnumValues, fieldNote)))
            {
                return;
            }

            if (merged.External || merged.Recursive)
            {
                return;
            }

            if (depth >= MaxDepth)
            {
                Truncated |= merged.Items is not null || merged.IsObjectWithFields;
                return;
            }

            if (merged.Items is not null)
            {
                var items = Merge(merged.Items, merged.Ancestors);
                if (items.External)
                {
                    return;
                }

                Walk(items, childPrefix, scope, note, depth + 1);
                return;
            }

            Walk(merged, path, scope, note, depth);
        }

        /// <returns><c>false</c> se o limite de campos foi atingido.</returns>
        private bool Add(ScannerApiField field)
        {
            var key = (field.Location, field.StatusCode, field.MediaType, field.Path);
            if (_indexes.TryGetValue(key, out var index))
            {
                // O mesmo caminho em duas variantes: fica o primeiro, e a nota diz que varia.
                _fields[index] = _fields[index] with { Required = false, Note = field.Note ?? _fields[index].Note };
                return true;
            }

            if (_fields.Count >= MaxFieldsPerEndpoint)
            {
                Truncated = true;
                return false;
            }

            _indexes[key] = _fields.Count;
            _fields.Add(field);
            return true;
        }
    }
}
