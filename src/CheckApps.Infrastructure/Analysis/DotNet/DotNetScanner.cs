using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using CheckApps.Application.Analysis;
using CheckApps.Domain.Analysis;

namespace CheckApps.Infrastructure.Analysis.DotNet;

/// <summary>
/// Primeiro scanner (spec 013, ADR-011): lê projetos .NET — formato SDK e formato legado do .NET Framework — e extrai a
/// versão do framework e os pacotes **diretos**, com a versão **declarada**. É um parser: sem rede, sem relógio, e só
/// o texto dos arquivos recebidos. Aviso nunca traz conteúdo de arquivo, só o caminho e o que aconteceu.
///
/// Arquivos lidos: <c>*.csproj</c>, o <c>packages.config</c> do mesmo diretório e o <c>Directory.Build.props</c> e o
/// <c>Directory.Packages.props</c> mais próximos acima do projeto. Limites conhecidos: <c>Condition</c> não é avaliada,
/// só o arquivo mais próximo de cada tipo vale, e dependências transitivas não aparecem.
///
/// Desde a versão 1.1.0 (spec 014) lê também <c>appsettings*.json</c>, base e por ambiente, e devolve **candidatos
/// brutos** a integração (chave cujo último segmento está em <see cref="AnalysisOptions.EffectiveIntegrationKeyHints"/>
/// e valor texto). Não decide o que é gravado nem redige segredos: isso é do <see cref="IntegrationNormalizer"/>.
/// </summary>
public sealed partial class DotNetScanner : IRepositoryScanner
{
    private const string BuildPropsFile = "Directory.Build.props";
    private const string PackagesPropsFile = "Directory.Packages.props";
    private const string PackagesConfigFile = "packages.config";
    private const int MaxResolvePasses = 5;

    private static readonly string[] IgnoredFolders = ["bin", "obj", "node_modules", ".git"];

    [GeneratedRegex(@"\$\(([^)]+)\)", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex PropertyReference();

    // Nunca emitidos como integração, mesmo que alguém os ponha na lista de dicas.
    private static readonly string[] SecretKeyNames = ["Password", "Pwd", "Secret", "Token", "ApiKey", "Key", "ConnectionString"];

    private const string AppSettingsPrefix = "appsettings";

    private readonly string[] _keyHints;

    public DotNetScanner() : this(new AnalysisOptions())
    {
    }

    public DotNetScanner(AnalysisOptions options)
    {
        _keyHints = options.EffectiveIntegrationKeyHints;
    }

    public string Id => "dotnet";

    public string Version => "1.1.0";

    public bool AppliesTo(IReadOnlyList<string> paths) => ProjectPaths(paths).Count > 0;

    public IReadOnlyList<string> SelectFiles(IReadOnlyList<string> paths)
    {
        var known = Index(paths);
        var selected = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var project in ProjectPaths(paths))
        {
            selected.Add(project);
            var directory = DirectoryOf(project);

            if (known.TryGetValue(Combine(directory, PackagesConfigFile), out var packagesConfig))
            {
                selected.Add(packagesConfig);
            }

            foreach (var propsFile in new[] { BuildPropsFile, PackagesPropsFile })
            {
                if (FindNearest(known, directory, propsFile) is { } found)
                {
                    selected.Add(found);
                }
            }
        }

        foreach (var settings in paths.Where(IsAppSettings).Where(p => !IsIgnored(p)))
        {
            selected.Add(settings);
        }

        return selected.ToList();
    }

    public ScannerResult Analyze(IReadOnlyDictionary<string, string> files)
    {
        var context = new Context(files);

        foreach (var project in context.Known.Keys.Where(IsProject).Where(p => !IsIgnored(p)).Order(StringComparer.Ordinal))
        {
            AnalyzeProject(context, project);
        }

        foreach (var settings in context.Known.Keys.Where(IsAppSettings).Where(p => !IsIgnored(p)).Order(StringComparer.Ordinal))
        {
            AnalyzeAppSettings(context, settings);
        }

        return new ScannerResult(context.Facts, context.Warnings.Distinct().ToList(), context.Integrations);
    }

    private static void AnalyzeProject(Context context, string projectPath)
    {
        var document = context.Load(projectPath);
        if (document is null)
        {
            return;
        }

        var directory = DirectoryOf(projectPath);
        var buildPath = FindNearest(context.Known, directory, BuildPropsFile);
        var packagesPath = FindNearest(context.Known, directory, PackagesPropsFile);
        var build = buildPath is null ? null : context.Load(buildPath);
        var packages = packagesPath is null ? null : context.Load(packagesPath);

        // Quem vem depois vale mais: projeto > Directory.Build.props > Directory.Packages.props.
        var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        MergeProperties(properties, packages);
        MergeProperties(properties, build);
        MergeProperties(properties, document);

        AddFrameworks(context, projectPath, document, build, buildPath, properties);
        AddPackageReferences(context, projectPath, document, packages, packagesPath, properties);
        AddPackagesConfig(context, projectPath, directory);
    }

    // ---- Framework -------------------------------------------------------------------------------------------------

    private static void AddFrameworks(
        Context context, string projectPath, XDocument project, XDocument? build, string? buildPath,
        Dictionary<string, string> properties)
    {
        // Ordem: o que o próprio projeto declara (SDK, depois legado) e só então o que herda do Directory.Build.props.
        var found =
            FromFrameworks(context, projectPath, project, projectPath, properties, inheritedFrom: null)
            || FromLegacyVersion(context, projectPath, project, projectPath, properties, inheritedFrom: null)
            || (build is not null && buildPath is not null
                && (FromFrameworks(context, projectPath, build, buildPath, properties, inheritedFrom: buildPath)
                    || FromLegacyVersion(context, projectPath, build, buildPath, properties, inheritedFrom: buildPath)));

        if (!found)
        {
            context.Warnings.Add($"{projectPath}: nenhum TargetFramework encontrado.");
        }
    }

    private static bool FromFrameworks(
        Context context, string projectPath, XDocument source, string sourceFile,
        Dictionary<string, string> properties, string? inheritedFrom)
    {
        var raw = FirstProperty(source, "TargetFrameworks") ?? FirstProperty(source, "TargetFramework");
        if (raw is null)
        {
            return false;
        }

        foreach (var token in raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct())
        {
            var value = Substitute(properties, token);
            if (value.Contains("$(", StringComparison.Ordinal))
            {
                context.Warnings.Add($"{projectPath}: o TargetFramework '{token}' vem de uma propriedade não resolvida.");
                AddFramework(context, projectPath, sourceFile, token, Confidences.Low, "versão vem de propriedade não resolvida");
                continue;
            }

            var confidence = inheritedFrom is null ? Confidences.High : Confidences.Medium;
            AddFramework(context, projectPath, sourceFile, value, confidence, inheritedFrom is null ? null : $"herdado de {inheritedFrom}");
        }

        return true;
    }

    private static bool FromLegacyVersion(
        Context context, string projectPath, XDocument source, string sourceFile,
        Dictionary<string, string> properties, string? inheritedFrom)
    {
        var raw = FirstProperty(source, "TargetFrameworkVersion");
        if (raw is null)
        {
            return false;
        }

        var value = Substitute(properties, raw);
        var moniker = FrameworkMoniker.FromTargetFrameworkVersion(value);
        if (moniker is null)
        {
            context.Warnings.Add($"{projectPath}: o TargetFrameworkVersion '{raw}' não segue o padrão vN.N.");
            AddFramework(context, projectPath, sourceFile, raw, Confidences.Low, "TargetFrameworkVersion fora do padrão vN.N");
            return true;
        }

        var confidence = inheritedFrom is null ? Confidences.High : Confidences.Medium;
        AddFramework(context, projectPath, sourceFile, moniker, confidence, inheritedFrom is null ? null : $"herdado de {inheritedFrom}");
        return true;
    }

    private static void AddFramework(
        Context context, string projectPath, string sourceFile, string name, string confidence, string? note) =>
        context.Facts.Add(new ScannerFact(
            Ecosystems.DotNet, FactKinds.Framework, name, null, projectPath, sourceFile, confidence, note));

    // ---- Pacotes ---------------------------------------------------------------------------------------------------

    private static void AddPackageReferences(
        Context context, string projectPath, XDocument project, XDocument? packages, string? packagesPath,
        Dictionary<string, string> properties)
    {
        var central = packages is not null
            && properties.TryGetValue("ManagePackageVersionsCentrally", out var flag)
            && string.Equals(flag, "true", StringComparison.OrdinalIgnoreCase);

        foreach (var reference in project.Descendants().Where(e => IsNamed(e, "PackageReference")))
        {
            var name = Attribute(reference, "Include")?.Trim();
            if (string.IsNullOrEmpty(name))
            {
                continue;   // Update/Remove e itens sem nome não declaram dependência
            }

            var overridden = Attribute(reference, "VersionOverride") ?? ChildValue(reference, "VersionOverride");
            var declared = Attribute(reference, "Version") ?? ChildValue(reference, "Version");

            ResolvedVersion resolved;
            string sourceFile = projectPath;

            if (!string.IsNullOrWhiteSpace(overridden))
            {
                resolved = Resolve(properties, overridden);
            }
            else if (!string.IsNullOrWhiteSpace(declared))
            {
                resolved = Resolve(properties, declared);
            }
            else if (central && packages is not null && packagesPath is not null
                && CentralVersion(packages, name) is { } centralRaw)
            {
                // Versão do gerenciamento central: nunca "literal no projeto", então no máximo média.
                var inner = Resolve(properties, centralRaw);
                resolved = inner with
                {
                    Confidence = inner.Confidence == Confidences.Low ? Confidences.Low : Confidences.Medium,
                    Note = inner.Confidence == Confidences.Low
                        ? inner.Note
                        : "versão do gerenciamento central de pacotes (Directory.Packages.props)"
                };
                sourceFile = packagesPath;
            }
            else
            {
                resolved = new ResolvedVersion(null, Confidences.Low, "sem versão no projeto nem no gerenciamento central", null);
                context.Warnings.Add($"{projectPath}: o pacote '{name}' não tem versão.");
            }

            if (resolved.Warning is not null)
            {
                context.Warnings.Add($"{projectPath}: o pacote '{name}' {resolved.Warning}");
            }

            context.AddPackage(new ScannerFact(
                Ecosystems.NuGet, FactKinds.Package, name, resolved.Value, projectPath, sourceFile, resolved.Confidence, resolved.Note));
        }
    }

    /// <summary>Projetos legados guardam os pacotes no packages.config, ao lado do projeto. Vale só para o que o projeto não declarou.</summary>
    private static void AddPackagesConfig(Context context, string projectPath, string directory)
    {
        var configPath = Combine(directory, PackagesConfigFile);
        if (!context.Known.TryGetValue(configPath, out var actual))
        {
            return;
        }

        var document = context.Load(actual);
        if (document is null)
        {
            return;
        }

        foreach (var package in document.Descendants().Where(e => IsNamed(e, "package")))
        {
            var name = Attribute(package, "id")?.Trim();
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            var version = Attribute(package, "version")?.Trim();
            context.AddPackage(
                new ScannerFact(
                    Ecosystems.NuGet, FactKinds.Package, name, string.IsNullOrEmpty(version) ? null : version,
                    projectPath, actual,
                    string.IsNullOrEmpty(version) ? Confidences.Low : Confidences.High,
                    string.IsNullOrEmpty(version) ? "o packages.config não traz a versão" : null),
                skipIfProjectAlreadyHas: true);
        }
    }

    private static string? CentralVersion(XDocument packages, string name)
    {
        var entry = packages.Descendants()
            .FirstOrDefault(e => IsNamed(e, "PackageVersion")
                && string.Equals(Attribute(e, "Include")?.Trim(), name, StringComparison.OrdinalIgnoreCase));
        return entry is null ? null : Attribute(entry, "Version") ?? ChildValue(entry, "Version");
    }

    // ---- Versões e propriedades ------------------------------------------------------------------------------------

    private sealed record ResolvedVersion(string? Value, string Confidence, string? Note, string? Warning);

    private static ResolvedVersion Resolve(Dictionary<string, string> properties, string raw)
    {
        var declared = raw.Trim();

        if (declared.Contains("$(", StringComparison.Ordinal))
        {
            var substituted = Substitute(properties, declared);
            if (substituted.Contains("$(", StringComparison.Ordinal))
            {
                var names = string.Join(", ", PropertyReference().Matches(substituted).Select(m => m.Groups[1].Value).Distinct());
                return new ResolvedVersion(
                    declared, Confidences.Low, $"versão vem de propriedade não resolvida: {names}",
                    $"usa a propriedade não resolvida {names}.");
            }

            return new ResolvedVersion(substituted, Confidences.Medium, "versão resolvida a partir de uma propriedade", null);
        }

        if (declared.Contains('*') || declared.StartsWith('[') || declared.StartsWith('('))
        {
            return new ResolvedVersion(declared, Confidences.Medium, "versão flutuante ou intervalo, guardada como declarada", null);
        }

        return new ResolvedVersion(declared, Confidences.High, null, null);
    }

    private static string Substitute(Dictionary<string, string> properties, string value)
    {
        for (var pass = 0; pass < MaxResolvePasses && value.Contains("$(", StringComparison.Ordinal); pass++)
        {
            var next = PropertyReference().Replace(
                value, m => properties.TryGetValue(m.Groups[1].Value.Trim(), out var known) ? known : m.Value);
            if (next == value)
            {
                break;
            }

            value = next;
        }

        return value;
    }

    private static void MergeProperties(Dictionary<string, string> properties, XDocument? document)
    {
        if (document?.Root is null)
        {
            return;
        }

        foreach (var group in document.Root.Elements().Where(e => IsNamed(e, "PropertyGroup")))
        {
            foreach (var property in group.Elements())
            {
                var value = Substitute(properties, property.Value.Trim());
                if (value.Length > 0 && !value.Contains("$(", StringComparison.Ordinal))
                {
                    properties[property.Name.LocalName] = value;
                }
            }
        }
    }

    private static string? FirstProperty(XDocument document, string name)
    {
        if (document.Root is null)
        {
            return null;
        }

        return document.Root.Elements().Where(e => IsNamed(e, "PropertyGroup"))
            .SelectMany(g => g.Elements())
            .Where(e => IsNamed(e, name))
            .Select(e => e.Value.Trim())
            .FirstOrDefault(v => v.Length > 0);
    }

    // ---- appsettings (spec 014) ------------------------------------------------------------------------------------

    private static readonly JsonDocumentOptions SettingsJson = new()
    {
        // O .NET aceita comentários e vírgula sobrando no appsettings; conteúdo de repositório é entrada não confiável.
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        MaxDepth = 32
    };

    private void AnalyzeAppSettings(Context context, string path)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(context.Text(path).TrimStart((char)0xFEFF), SettingsJson);
        }
        catch (JsonException)
        {
            context.Warnings.Add($"{path}: JSON inválido; arquivo ignorado.");
            return;
        }

        using (document)
        {
            var environment = EnvironmentOf(path);
            var project = NearestProjectDirectory(context.Known, path);
            Walk(document.RootElement, [], path, environment, project, context);
        }
    }

    private void Walk(
        JsonElement element, List<string> segments, string path, string? environment, string project, Context context)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    // Connection strings carregam credenciais e não são integração HTTP: nem se entra na seção.
                    if (segments.Count == 0 && string.Equals(property.Name, "ConnectionStrings", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    segments.Add(property.Name);
                    Walk(property.Value, segments, path, environment, project, context);
                    segments.RemoveAt(segments.Count - 1);
                }

                break;

            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    segments.Add(index++.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    Walk(item, segments, path, environment, project, context);
                    segments.RemoveAt(segments.Count - 1);
                }

                break;

            case JsonValueKind.String or JsonValueKind.Null when segments.Count > 0 && IsEndpointKey(segments[^1]):
                var reference = segments.Count >= 2 ? segments[^2] : "(raiz)";
                context.Integrations.Add(new ScannerIntegration(
                    IntegrationMechanisms.Config, project, path, reference, string.Join(':', segments), environment,
                    element.ValueKind == JsonValueKind.String ? element.GetString() ?? "" : ""));
                break;
        }
    }

    private bool IsEndpointKey(string name) =>
        _keyHints.Contains(name, StringComparer.OrdinalIgnoreCase)
        && !SecretKeyNames.Contains(name, StringComparer.OrdinalIgnoreCase);

    private static bool IsAppSettings(string path)
    {
        var name = path[(path.LastIndexOf('/') + 1)..];
        return name.StartsWith(AppSettingsPrefix + ".", StringComparison.OrdinalIgnoreCase)
            && name.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary><c>appsettings.json</c> → nulo; <c>appsettings.Homolog.json</c> → <c>Homolog</c>.</summary>
    private static string? EnvironmentOf(string path)
    {
        var name = path[(path.LastIndexOf('/') + 1)..];
        if (string.Equals(name, AppSettingsPrefix + ".json", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var middle = name[(AppSettingsPrefix.Length + 1)..^".json".Length];
        return middle.Length == 0 ? null : middle;
    }

    /// <summary>O diretório do <c>.csproj</c> mais próximo acima do arquivo; sem projeto, o próprio diretório do arquivo.</summary>
    private static string NearestProjectDirectory(IReadOnlyDictionary<string, string> known, string path)
    {
        var directory = DirectoryOf(path);
        var best = known.Keys
            .Where(IsProject).Where(p => !IsIgnored(p))
            .Select(DirectoryOf)
            .Where(d => d.Length == 0 || directory == d || directory.StartsWith(d + "/", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(d => d.Length)
            .Select(d => (string?)d)
            .FirstOrDefault();
        return best ?? directory;
    }

    // ---- XML e caminhos --------------------------------------------------------------------------------------------

    // O formato legado declara xmlns do MSBuild: compara-se pelo nome local, nunca pelo nome qualificado.
    private static bool IsNamed(XElement element, string name) =>
        string.Equals(element.Name.LocalName, name, StringComparison.OrdinalIgnoreCase);

    private static string? Attribute(XElement element, string name) =>
        element.Attributes().FirstOrDefault(a => string.Equals(a.Name.LocalName, name, StringComparison.OrdinalIgnoreCase))?.Value;

    private static string? ChildValue(XElement element, string name) =>
        element.Elements().FirstOrDefault(e => IsNamed(e, name))?.Value.Trim();

    private static bool IsProject(string path) => path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase);

    private static bool IsIgnored(string path)
    {
        var segments = path.Split('/');
        return segments[..^1].Any(s => IgnoredFolders.Contains(s, StringComparer.OrdinalIgnoreCase));
    }

    private static List<string> ProjectPaths(IReadOnlyList<string> paths) =>
        paths.Where(IsProject).Where(p => !IsIgnored(p)).Order(StringComparer.Ordinal).ToList();

    private static Dictionary<string, string> Index(IEnumerable<string> paths)
    {
        var index = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in paths)
        {
            index.TryAdd(path, path);
        }

        return index;
    }

    private static string DirectoryOf(string path)
    {
        var slash = path.LastIndexOf('/');
        return slash < 0 ? string.Empty : path[..slash];
    }

    private static string Combine(string directory, string name) => directory.Length == 0 ? name : $"{directory}/{name}";

    /// <summary>O arquivo mais próximo, subindo do diretório do projeto até a raiz do repositório.</summary>
    private static string? FindNearest(IReadOnlyDictionary<string, string> known, string directory, string name)
    {
        while (true)
        {
            if (known.TryGetValue(Combine(directory, name), out var actual))
            {
                return actual;
            }

            if (directory.Length == 0)
            {
                return null;
            }

            directory = DirectoryOf(directory);
        }
    }

    /// <summary>Estado de uma execução: os arquivos lidos, os XML já interpretados, os fatos e os avisos.</summary>
    private sealed class Context
    {
        private readonly Dictionary<string, string> _contents;
        private readonly Dictionary<string, XDocument?> _documents = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<(string Project, string Name, string? Version)> _seen = [];

        public Context(IReadOnlyDictionary<string, string> files)
        {
            _contents = new Dictionary<string, string>(files, StringComparer.OrdinalIgnoreCase);
            Known = Index(files.Keys);
        }

        public IReadOnlyDictionary<string, string> Known { get; }

        public List<ScannerFact> Facts { get; } = [];

        public List<string> Warnings { get; } = [];

        public List<ScannerIntegration> Integrations { get; } = [];

        public string Text(string path) => _contents[path];

        public XDocument? Load(string path)
        {
            if (_documents.TryGetValue(path, out var cached))
            {
                return cached;
            }

            XDocument? document = null;
            if (_contents.TryGetValue(path, out var text))
            {
                try
                {
                    var settings = new XmlReaderSettings
                    {
                        // Conteúdo de repositório é entrada não confiável: sem DTD, sem entidades externas.
                        DtdProcessing = DtdProcessing.Prohibit,
                        XmlResolver = null,
                        MaxCharactersInDocument = 4_000_000,
                    };
                    using var reader = XmlReader.Create(new StringReader(text.TrimStart((char)0xFEFF)), settings);
                    document = XDocument.Load(reader);
                }
                catch (XmlException)
                {
                    Warnings.Add($"{path}: XML inválido; arquivo ignorado.");
                }
            }

            _documents[path] = document;
            return document;
        }

        /// <summary>Um pacote por projeto, nome e versão; o packages.config nunca repete o que o projeto já declarou.</summary>
        public void AddPackage(ScannerFact fact, bool skipIfProjectAlreadyHas = false)
        {
            if (skipIfProjectAlreadyHas
                && Facts.Any(f => f.Kind == FactKinds.Package && f.ProjectPath == fact.ProjectPath
                    && string.Equals(f.Name, fact.Name, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            if (_seen.Add((fact.ProjectPath, fact.Name.ToLowerInvariant(), fact.Version)))
            {
                Facts.Add(fact);
            }
        }
    }
}
