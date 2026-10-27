# Leitor de GitHub — configurar e rodar

O scan descobre os repositórios de uma ou mais **siglas** e os coloca numa caixa de entrada (menu **Repositórios**, filtro **Novos**). Quem decide o destino de cada um é a pessoa: criar um app, vincular a um app existente, marcar como de outra squad ou ignorar. O scan **nunca cria App**.

Decisões e motivos: `specs/005-leitor-github-fase-1/spec.md` e `.agents/architecture/decisions/adr-010-leitor-github.md`.

Os valores de exemplo abaixo são fictícios (`{prefixo}`, `AB1`, `AB1-0001`). Nome da organização, token e convenção de nome ficam **só em configuração local** — nunca em arquivo versionado.

## 1. Sem GitHub: a fonte `fake`

Para ver o fluxo inteiro sem rede nem token:

```bash
dotnet run --project tools/CheckApps.GitHubScan -- --sigla ab1,cd2 --source fake
```

Grava 8 repositórios fictícios com os casos difíceis (aplicação, infra, contrato, arquivado, sigla-app de outra sigla…). Depois, abra **Repositórios** no menu. Sem `Scan:NameTemplate` configurado, a fonte `fake` usa `fake-{sigla}-`.

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

Opcionais (os padrões estão entre parênteses): `Scan:SiglaPattern` (`^[a-z0-9]{3}$`), `Scan:MaxResults` (`50`), `Scan:Source` (`github`; `fake` para demonstração), `GitHub:ConfigFilePath` (`config.yaml`), `GitHub:SiglaAppKey` (`siglaApp`).

Para a **API** mostrar sugestões de papel e nome na caixa de entrada, ela precisa da mesma convenção de nome (para só isso, não precisa do token; o scan pela tela, seção 5, precisa também do token e da organização):

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

## 5. Escanear pela tela

O mesmo scan pode ser disparado em **Repositórios → Buscar no GitHub**: informe as siglas (`ab1, cd2`) e use *Pré-visualizar* (mostra o que seria gravado, **sem gravar nada**) ou *Escanear*. A ferramenta de linha de comando continua funcionando; os dois executam o mesmo código.

A tela vem **desligada**, e a API precisa da credencial. Os user-secrets da API são **separados** dos da ferramenta de linha de comando:

```bash
dotnet user-secrets set "Scan:Enabled" "true" --project src/CheckApps.Api
dotnet user-secrets set "GitHub:Organization" "<organização>" --project src/CheckApps.Api
dotnet user-secrets set "GitHub:Token" "<token>" --project src/CheckApps.Api
dotnet user-secrets set "Scan:NameTemplate" '<prefixo>-{sigla}-' --project src/CheckApps.Api
```

**Reinicie a API depois de mudar qualquer um**: a configuração é lida na subida. Se algo faltar, o bloco da tela diz **o que** configurar, sem mostrar valores. Para ver o fluxo sem rede nem token, use `Scan:Source = fake` junto com `Scan:Enabled = true`.

**Segurança**: sem autenticação, quem abre o sistema pode disparar scans com o token configurado. Por isso o endpoint vem desligado, só um scan roda por vez, e o escopo continua limitado por siglas e pelo limite de resultados. Use assim apenas em ambiente de uso individual; **antes de qualquer uso compartilhado, o endpoint precisa de autenticação** (ADR-010).

### "Já no catálogo"

Se um repositório já virou app — ou se foi cadastrado à mão com a **mesma URL** (sem diferença de caixa nem barra final; spec 012) —, ele **não aparece em "Novos"** (esse filtro só mostra o que ainda precisa de decisão), mas **continua na tela**: nos filtros **No catálogo** e **Todos**, com a etiqueta e o link do app e sem botões de triagem. Ele também aparece **no resultado do scan** com a marca **já no catálogo**. Para ele **voltar para "Novos"** e refazer a triagem, remova o app (só é possível se o app ainda não tiver histórico de qualidade nem estiver em checagem; senão, inative-o).

A tela lista os repositórios que **passaram pelo scan** e, nos filtros **Todos** e **No catálogo**, também os já cadastrados nos apps (à mão ou pelo seed), cada um com o atalho **Gerenciar** para a edição do app (spec 010).

## 6. Organização de teste

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

## 6b. Analisar o conteúdo dos repositórios (spec 013)

A **descoberta** acima acha repositórios e os leva à triagem. A **análise** lê os arquivos de repositórios **já catalogados** (os que viraram app ou foram vinculados) e grava o que um *scanner* extrai: hoje, o scanner .NET lê a versão do framework e os pacotes diretos de `*.csproj` (formato SDK e legado, com `packages.config`), `Directory.Packages.props` e `Directory.Build.props`. Não grava código nem conteúdo de arquivo, só fatos, com a confiança em texto.

```bash
# sem rede nem token (os repositórios do scan fake, depois de vinculados a apps):
dotnet run --project tools/CheckApps.Analyze -- --sigla ab1,cd2 --source fake

# GitHub de verdade: simulação primeiro, depois gravando
dotnet run --project tools/CheckApps.Analyze -- --sigla ab1 --dry-run
dotnet run --project tools/CheckApps.Analyze -- --sigla ab1
```

- **Mesmo token da descoberta**: a ferramenta usa o mesmo armazenamento de segredos do `CheckApps.GitHubScan` (mesmo `UserSecretsId`), então `GitHub:Token` já configurado serve. Só o token é necessário: o repositório é lido pelo id, não pela organização.
- **Idempotente por commit**: rodar de novo sobre um commit já analisado não grava nada ("sem alteração"); um commit novo gera uma análise nova e a anterior fica como histórico. Falha de um repositório (sem acesso, vazio, limite de chamadas) não interrompe os outros e fica registrada; ela não esconde a última análise boa. Para reanalisar o mesmo commit depois de uma correção no scanner, ainda não há opção: só um commit novo gera análise.
- **Limites**: `Analysis:MaxProjectFiles` (padrão 200) e `Analysis:MaxFileBytes` (padrão 512 KB); ao exceder, a análise fica **parcial** com aviso.
- **Pacote interno**: `Analysis:InternalPackagePrefixes` (lista; o nome da empresa no início do namespace, ex.: `Empresa.`). **Configuração local, nunca em arquivo versionado nem em exemplo de documentação**: `dotnet user-secrets set "Analysis:InternalPackagePrefixes:0" "<prefixo>" --project src/CheckApps.Api` (a marcação é calculada pela **API** ao ler). Sem prefixo, nada é marcado.
- **Onde ver**: seção **Dependências** no detalhe do app e a tela **Dependências** do menu ("Quem usa o pacote" e ".NET por squad"). A API só lê; não há como disparar a análise pela tela.
- **O que não cobre ainda**: dependências transitivas, outras tecnologias, integrações entre apps (`appsettings`, Refit), `ProjectReference`. Códigos de saída iguais aos da descoberta; `3` também quando algum repositório falha.

## 7. Problemas comuns

| Sintoma | Causa provável |
|---|---|
| `O GitHub recusou o token` | token inválido ou expirado — gere outro |
| `O GitHub negou o acesso` | token sem `Metadata` e `Contents` (leitura) na organização, ou a política da organização não aceita tokens fine-grained |
| `O limite de chamadas do GitHub foi esgotado` | espere o horário informado na mensagem |
| `A organização '…' não foi encontrada` | nome errado, ou o token não tem acesso a ela |
| `encontraria N repositórios, acima do limite` | escopo amplo demais — use uma sigla mais específica (o limite é `Scan:MaxResults`) |
| Repositório criado há pouco não aparece | a busca do GitHub demora a indexar; rode de novo depois |
| `Sigla fora do formato esperado` | informe a sigla (ex.: `ab1`), não um trecho de nome |
| Escaneei, o repositório aparece como "inalterado" e a tabela está vazia em "Novos" | ele já virou app — veja o filtro **No catálogo** e a marca "já no catálogo" no resultado; para triar de novo, remova o app |
| "O scan pela API está desligado" | falta `Scan:Enabled = true` nos user-secrets **da API** (e reiniciar a API) |
| "O scan não está pronto" na tela | falta `GitHub:Token`, `GitHub:Organization` ou `Scan:NameTemplate` nos user-secrets **da API**; a mensagem diz qual |
| "Já há um scan em andamento" | outro scan está rodando; espere terminar |
| "Não foi possível falar com o GitHub" (502) | rede indisponível ou `GitHub:ApiBaseUrl` errada |
| "Nenhum repositório catalogado nessa sigla" na análise | os repositórios precisam estar no catálogo (virar app ou vincular a um app pela triagem) e ter a sigla preenchida; a análise não lê o que ainda é só "descoberto" |
| "O repositório não foi encontrado no GitHub, ou o token não tem acesso a ele" | o token não enxerga aquele repositório (acesso parcial é normal); os demais seguem |
| Análise "parcial" | há aviso na saída e na tela: arquivo de projeto inválido, versão por propriedade não resolvida, limite de arquivos ou de tamanho, ou árvore truncada pelo GitHub |
| Aviso de YAML | o arquivo existe mas não é um YAML válido, ou a chave não tem valor simples; o repositório entra sem sigla-app |
