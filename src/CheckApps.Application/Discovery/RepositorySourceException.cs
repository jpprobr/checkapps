namespace CheckApps.Application.Discovery;

/// <summary>
/// A fonte não conseguiu ler (token inválido, sem permissão, limite de chamadas, rede, organização
/// inexistente). A mensagem diz o que fazer e **nunca** contém credencial — quem a mostra pode
/// imprimi-la sem filtro.
/// </summary>
public class RepositorySourceException(string message, Exception? inner = null) : Exception(message, inner);
