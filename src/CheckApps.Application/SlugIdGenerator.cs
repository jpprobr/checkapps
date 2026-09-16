using CheckApps.Domain;

namespace CheckApps.Application;

/// <summary>
/// Gera um id único a partir de um slug do nome, tentando criar via um Put
/// condicional (attribute_not_exists) e incrementando um sufixo numérico em caso
/// de colisão. Evita a corrida de "checar depois gravar" ao delegar a atomicidade
/// para o próprio DynamoDB.
/// </summary>
public static class SlugIdGenerator
{
    private const int MaxAttempts = 50;

    public static async Task<string> GenerateUniqueAsync(
        string name, Func<string, CancellationToken, Task<bool>> tryCreate, CancellationToken cancellationToken = default)
    {
        var baseSlug = Slug.Generate(name);
        if (baseSlug.Length == 0)
        {
            baseSlug = "item";
        }

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            var candidate = attempt == 1 ? baseSlug : $"{baseSlug}-{attempt}";
            if (await tryCreate(candidate, cancellationToken))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException($"Não foi possível gerar um id único para \"{name}\" após {MaxAttempts} tentativas.");
    }
}
