# Leitor de GitHub — configurar e rodar

O scan descobre os repositórios de uma ou mais **siglas** e os coloca numa caixa de entrada (menu **Descobertos**). Quem decide o destino de cada um é a pessoa: criar um app, vincular a um app existente, marcar como de outra squad ou ignorar. O scan **nunca cria App**.

Decisões e motivos: `specs/005-leitor-github-fase-1/spec.md` e `.agents/architecture/decisions/adr-010-leitor-github.md`.

Os valores de exemplo abaixo são fictícios (`{prefixo}`, `AB1`, `AB1-0001`). Nome da organização, token e convenção de nome ficam **só em configuração local** — nunca em arquivo versionado.

## 1. Sem GitHub: a fonte `fake`

Para ver o fluxo inteiro sem rede nem token:

```bash
dotnet run --project tools/CheckApps.GitHubScan -- --sigla ab1,cd2 --source fake
```

Grava 8 repositórios fictícios com os casos difíceis (aplicação, infra, contrato, arquivado, sigla-app de outra sigla…). Depois, abra **Descobertos** no menu. Sem `Scan:NameTemplate` configurado, a fonte `fake` usa `fake-{sigla}-`.

## 2. Token do GitHub

Use um **fine-grained personal access token** (não o clássico):

- **Resource owner**: a organização que será lida.
- **Repository access**: só os repositórios necessários (ou todos da organização de teste).
- **Permissions → Repository**: `Metadata: Read-only` e `Contents: Read-only`. Nada de escrita.
- **Expiration**: curta (30 a 90 dias).
- Se a organização exigir aprovação de tokens, quem administra a organização aprova em *Settings → Personal access tokens*.

Um token pessoal é amarrado a uma pessoa e expira — serve para laboratório. Para uso compartilhado, o alvo é um GitHub App (ADR-010).

## 3. Configuração local

```bash
dotnet user-secrets set "GitHub:Organization" "<organização>" --project tools/CheckApps.GitHubScan
dotnet user-secrets set "GitHub:Token" "<token>" --project tools/CheckApps.GitHubScan
dotnet user-secrets set "Scan:NameTemplate" "<prefixo>-{sigla}-" --project tools/CheckApps.GitHubScan
```

Opcionais (os padrões estão entre parênteses): `Scan:SiglaPattern` (`^[a-z0-9]{3}$`), `Scan:MaxResults` (`50`), `GitHub:ConfigFilePath` (`config.yaml`), `GitHub:SiglaAppKey` (`siglaApp`).

Para a **API** mostrar sugestões de papel e nome na caixa de entrada, ela precisa da mesma convenção de nome (não precisa do token):

```bash
dotnet user-secrets set "Scan:NameTemplate" "<prefixo>-{sigla}-" --project src/CheckApps.Api
```

Alternativa por variável de ambiente: `GitHub__Token`, `GitHub__Organization`, `Scan__NameTemplate`.

A ferramenta **recusa rodar sem token** e **nunca o imprime**, nem parcialmente.

## 4. Rodar

Primeiro em simulação — lê e mostra, **não grava dados**:

```bash
dotnet run --project tools/CheckApps.GitHubScan -- --sigla ab1 --dry-run
```

Depois, gravando:

```bash
dotnet run --project tools/CheckApps.GitHubScan -- --sigla ab1,cd2
```

Rodar de novo é seguro: atualiza o que mudou e **não desfaz nenhuma decisão de triagem**. Códigos de saída: `0` ok, `1` erro inesperado, `2` uso ou configuração, `3` falha ao ler a fonte (token, permissão, limite de chamadas, limite de resultados).

## 5. Organização de teste

Para exercitar o scan de verdade, uma organização gratuita com repositórios no padrão `{prefixo}-{sigla}-{segmento}-{nome}` basta (repositórios podem ser vazios, exceto pelo arquivo de configuração). Usando o prefixo `lab`:

| Repositório | Conteúdo | O que exercita |
|---|---|---|
| `lab-ab1-app-pagamentos` | `config.yaml` com `siglaApp: AB1-0001` | aplicação típica, confiança alta |
| `lab-ab1-app-bff-pagamentos` | `config.yaml` com `siglaApp: AB1-0002` | nome com mais de uma palavra |
| `lab-ab1-infra-fila` | sem `config.yaml` | infra: sem sigla-app |
| `lab-ab1-contrato-api-pagamentos` | sem `config.yaml` | papel `contrato` |
| `lab-ab1-app-legado` | sem `config.yaml`, **arquivado** | arquivado; nome diz app mas sem sigla-app (confiança baixa) |
| `lab-cd2-app-faturas` | `config.yaml` com `siglaApp: CD2-0002` | outra sigla |
| `playground` | qualquer | fora da convenção: **nunca** deve aparecer |
| `lab-ab1-restrito` | qualquer | exclua-o do acesso do token: acesso parcial, como no ambiente real |

Conteúdo de `config.yaml`:

```yaml
siglaApp: AB1-0001
```

Esperado para `--sigla ab1`: 5 repositórios (o restrito não aparece); `app-legado` com `[arquivado]`; `infra-fila` e `contrato-api-pagamentos` sem sigla-app.

## 6. Problemas comuns

| Sintoma | Causa provável |
|---|---|
| `O GitHub recusou o token` | token inválido ou expirado — gere outro |
| `O GitHub negou o acesso` | token sem `Metadata` e `Contents` (leitura) na organização, ou a política da organização não aceita tokens fine-grained |
| `O limite de chamadas do GitHub foi esgotado` | espere o horário informado na mensagem |
| `A organização '…' não foi encontrada` | nome errado, ou o token não tem acesso a ela |
| `encontraria N repositórios, acima do limite` | escopo amplo demais — use uma sigla mais específica (o limite é `Scan:MaxResults`) |
| Repositório criado há pouco não aparece | a busca do GitHub demora a indexar; rode de novo depois |
| `Sigla fora do formato esperado` | informe a sigla (ex.: `ab1`), não um trecho de nome |
| Aviso de YAML | o arquivo existe mas não é um YAML válido, ou a chave não tem valor simples; o repositório entra sem sigla-app |
