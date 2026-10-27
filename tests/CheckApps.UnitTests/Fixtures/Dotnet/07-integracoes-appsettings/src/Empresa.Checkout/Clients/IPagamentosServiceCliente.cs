using Refit;

namespace Empresa.Checkout.Clients;

// Referência para a spec 015 (leitura de código): a seção "IPagamentosServiceCliente" do
// appsettings tem o mesmo nome desta interface. A spec 014 NÃO lê arquivos .cs.
public interface IPagamentosServiceCliente
{
    [Get("/cobrancas/{id}")]
    Task<Cobranca> ObterAsync(string id);

    [Post("/cobrancas")]
    Task<Cobranca> CriarAsync([Body] Cobranca cobranca);
}

public record Cobranca(string Id, decimal Valor);
