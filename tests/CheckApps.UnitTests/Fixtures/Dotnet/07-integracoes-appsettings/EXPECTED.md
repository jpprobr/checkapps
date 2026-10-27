# 07 — Integrações em `appsettings` (spec 014)

Padrão informado pelo usuário em 2026-10-27: uma seção com o nome da interface Refit (`INomeServiceCliente`) contendo `BaseUrl`, com `{env}` no host; as rotas ficam na interface. **Fixture inventada** (`exemplo.com`, segredos fictícios `SEGREDO-PLANTADO-N`); ajustar quando houver um modelo real do time.

Arquivos lidos: `appsettings.json`, `appsettings.Development.json`, `appsettings.Staging.json`, `appsettings.Uat.json`, `appsettings.Production.json`. Os nomes de ambiente **não seguem convenção** entre os repositórios (decisão de 2026-10-27): `Dev`/`Hom`/`Prod`, `Development`/`Homolog`/`Production`, e `Staging` como homologação; por isso há `Staging` (→ `hom`) e `Uat` (fora da tabela de apelidos, sem categoria). O `.cs` está aqui só como referência para a spec 015 e **não é lido**.

O `appsettings.json` tem comentário `//` e vírgula sobrando (o .NET aceita): o parser precisa tolerar.

## Integrações esperadas

| Arquivo (ambiente) | Seção (`reference`) | Chave | Host / modelo | Caminho base | Estado | Confiança |
|---|---|---|---|---|---|---|
| `appsettings.json` (—) | `IPagamentosServiceCliente` | `…:BaseUrl` | `empresa.{env}.exemplo.com` | `/pagamentos` | parametrizada | média |
| `appsettings.json` (—) | `IClientesServiceCliente` | `…:BaseUrl` | `empresa.{env}.exemplo.com` | `/clientes` | parametrizada | média |
| `appsettings.json` (—) | `INotificacoesServiceCliente` | `…:BaseUrl` | `notificacoes.exemplo.com` | `/v1` (sem usuário, sem senha, sem `?key=`, sem barra final) | resolvida | alta |
| `appsettings.json` (—) | `IFaturasServiceCliente` | `…:BaseUrl` | — (texto cru `${FATURAS_URL}`) | — | não resolvida | baixa |
| `appsettings.json` (—) | `IEstoqueServiceCliente` | `…:BaseUrl` | — (vazio) | — | não resolvida | baixa |
| `appsettings.Development.json` (`Development` → `dev`) | `IPagamentosServiceCliente` | `…:BaseUrl` | `empresa.dev.exemplo.com` | `/pagamentos` | resolvida | alta |
| `appsettings.Staging.json` (`Staging` → `hom`) | `IPagamentosServiceCliente` | `…:BaseUrl` | `empresa.hom.exemplo.com` | `/pagamentos` | resolvida | alta |
| `appsettings.Uat.json` (`Uat` → sem categoria) | `IPagamentosServiceCliente` | `…:BaseUrl` | `empresa.uat.exemplo.com` | `/pagamentos` | resolvida | alta |
| `appsettings.Production.json` (`Production` → `prod`) | `IPagamentosServiceCliente` | `…:BaseUrl` | `empresa.exemplo.com` | `/pagamentos` | resolvida | alta |
| `appsettings.Production.json` (`Production` → `prod`) | `IFaturasServiceCliente` | `…:BaseUrl` | `faturas.exemplo.com` (porta `8443`) | `/api` | resolvida | alta |

**10 integrações.** A de `IPagamentosServiceCliente` aparece 5 vezes (base, Development, Staging, Uat, Production); a lista de hosts as agrupa pela **seção**, não por palpite sobre `{env}`.

## Não geram integração

| O que | Por quê |
|---|---|
| `ConnectionStrings:Checkout` | connection string: nunca lida nem gravada |
| `IClientesServiceCliente:ApiKey` | chave de nome de segredo: descartada |
| `IAuditoriaServiceCliente:BaseUrl` (`localhost:5001`) | `localhost` ignorado (conta 1) |
| `appsettings.Development.json` → `IClientesServiceCliente:BaseUrl` (`localhost:5002`) | `localhost` ignorado (conta 2) |
| `FilaMensagens:Endpoint` (`amqp://`) | chave da lista, mas o valor não é HTTP (conta como ignorada) |
| `Documentacao:LinkAjuda` (`https://ajuda.exemplo.com/...`) | o nome da chave (`LinkAjuda`) não está na lista (`BaseUrl`, `Url`, `Endpoint`, `Host`): links de ajuda e documentação não são integração |
| `Logging`, `Documentacao:Titulo` | não são URL |

Regra: só entra a chave cujo **último segmento** está em `Analysis:IntegrationKeyHints` e cujo valor é URL `http(s)` (com ou sem `{env}`), ou está vazio/com placeholder (não resolvida).

## Segredos plantados (nenhum pode aparecer no banco, na saída nem em mensagem)

`SEGREDO-PLANTADO-1` (connection string), `-2` (`ApiKey`), `-3` (usuário/senha na URL), `-4` (`?key=` na URL), além do usuário `usuario`.

## Análise

Situação `ok`. Avisos esperados, sem valores: 2 URLs locais ignoradas; 1 valor não HTTP ignorado (`amqp://`); 2 integrações não resolvidas (valor vazio ou com placeholder `${…}`). Frameworks e pacotes (`net8.0`, `Refit.HttpClientFactory 7.2.1`) continuam sendo extraídos como na spec 013.
