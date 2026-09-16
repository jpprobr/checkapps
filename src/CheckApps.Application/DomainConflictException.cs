namespace CheckApps.Application;

/// <summary>
/// Violação de uma regra de negócio que impede uma operação (ex.: remover uma
/// Squad que ainda tem Apps vinculados). Controllers traduzem isso para 409 Conflict.
/// </summary>
public class DomainConflictException(string message) : Exception(message);
