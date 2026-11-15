using CheckApps.Domain;

namespace CheckApps.Application.Journeys;

/// <summary>
/// Operações sobre a árvore de jornadas, em memória (o número de jornadas é pequeno e SQL recursivo diverge entre bancos, ADR-009).
/// Funções puras. Toleram dados ruins (ciclo ou pai que não existe) sem travar: um nó já visitado não é visitado de novo.
/// </summary>
public static class JourneyTree
{
    /// <summary>Os ancestrais da jornada, da raiz até o pai. Vazio para uma raiz.</summary>
    public static IReadOnlyList<Journey> PathTo(IReadOnlyList<Journey> all, Journey journey)
    {
        var byId = all.ToDictionary(j => j.Id, StringComparer.Ordinal);
        var path = new List<Journey>();
        var seen = new HashSet<string>(StringComparer.Ordinal) { journey.Id };
        var current = journey;

        while (current.ParentId is { } parentId && byId.TryGetValue(parentId, out var parent) && seen.Add(parent.Id))
        {
            path.Insert(0, parent);
            current = parent;
        }

        return path;
    }

    /// <summary>Os ids de todos os descendentes (filhos, netos…), sem o próprio nó.</summary>
    public static HashSet<string> DescendantIds(IReadOnlyList<Journey> all, string journeyId)
    {
        var childrenOf = all.Where(j => j.ParentId is not null).ToLookup(j => j.ParentId!, StringComparer.Ordinal);
        var result = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<string>([journeyId]);

        while (pending.Count > 0)
        {
            foreach (var child in childrenOf[pending.Pop()])
            {
                if (child.Id != journeyId && result.Add(child.Id))
                {
                    pending.Push(child.Id);
                }
            }
        }

        return result;
    }

    /// <summary>Em que nível o nó está: a raiz é 1; <c>null</c> (sem pai) é 0.</summary>
    public static int Depth(IReadOnlyList<Journey> all, string? journeyId)
    {
        if (journeyId is null)
        {
            return 0;
        }

        var byId = all.ToDictionary(j => j.Id, StringComparer.Ordinal);
        var depth = 0;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var current = journeyId;

        while (current is not null && byId.TryGetValue(current, out var node) && seen.Add(node.Id))
        {
            depth++;
            current = node.ParentId;
        }

        return depth;
    }

    /// <summary>Quantos níveis a subárvore do nó tem, contando o próprio nó: uma folha é 1.</summary>
    public static int Height(IReadOnlyList<Journey> all, string journeyId)
    {
        var childrenOf = all.Where(j => j.ParentId is not null).ToLookup(j => j.ParentId!, StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal) { journeyId };

        int Visit(string id)
        {
            var deepest = 0;
            foreach (var child in childrenOf[id])
            {
                if (seen.Add(child.Id))
                {
                    deepest = Math.Max(deepest, Visit(child.Id));
                }
            }

            return deepest + 1;
        }

        return Visit(journeyId);
    }
}
