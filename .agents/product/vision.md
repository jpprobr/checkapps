# CheckApps — Visão do produto

## Origem

O CheckApps nasceu de uma POC local em Python + Streamlit que consultava o SonarQube, exibia alguns indicadores de qualidade por aplicação e permitia exportar para Excel. A POC provou interesse real: **dois QAs e um coordenador** no contexto corporativo de origem queriam acompanhar os indicadores de qualidade das aplicações — e, principalmente, a **evolução histórica** deles. O maior gap da POC era justamente esse: ela mostrava só o estado atual, sem histórico.

O MVP atual (ver `specs/001-mvp-core/`) é a evolução dessa POC para uma aplicação web simples, visual e útil, atacando exatamente essa dor.

## O que o CheckApps quer ser (longo prazo)

Uma **camada contextual e AI-friendly** que ajuda times de engenharia a compreender e operar um ecossistema de aplicações distribuídas, conectando informações que hoje ficam espalhadas:

- Squads / times
- Aplicações
- Repositórios
- Jornadas (em árvore, com nível em texto livre; produto e operação entram como classificação opcional, não como entidades)
- Dependências
- Integrações
- Indicadores técnicos
- Arquitetura
- SDD / especificações
- Harness / CI/CD
- Documentação
- Agentes e RAGs

```
              CHECKAPPS
                  |
        Application Context
                  |
      ┌───────────┼───────────┐
      ▼           ▼           ▼
 Architecture  Operations   Product
      │           │           │
      └───────────┼───────────┘
                  ▼
             Context Layer
                  |
          ┌───────┼────────┐
          ▼       ▼        ▼
         SDD    Harness    AI
                         Agents
                           RAG
```

## O que o CheckApps NÃO quer ser

- **Não** é mais uma ferramenta corporativa de governança.
- **Não** substitui sistemas existentes (Sonar, Jira, Harness, catálogos corporativos).

A intenção é funcionar como **camada de contexto, integração e inteligência sobre as ferramentas existentes**, com baixo atrito de adoção, APIs/conectores/webhooks e boa experiência de uso. Tudo isso é direção, não escopo imediato.

## Perguntas que o produto existe para responder

Hoje (MVP), um coordenador ou QA abre a aplicação e responde rapidamente:

1. Quais apps minha squad possui?
2. Como está a qualidade atual deles?
3. Quais estão melhores / piores?
4. Qual é a evolução histórica?
5. Quais indicadores estão causando a mudança?
6. Quais aplicações merecem atenção?

Ainda **não** responde (estágios futuros): por que uma arquitetura é ruim, quais jornadas serão impactadas, qual operação de negócio será afetada, como gerar uma SDD, como executar um agente, como interagir com o Harness.

## Princípios que guiam o produto

**Regra de ouro: construir o menor produto que prove o maior valor.** Evitar infraestrutura para problemas que ainda não existem.

**AI-friendly é preparação, não implementação.** O modelo de dados preserva o que uma camada de IA futura vai precisar, sem construir a camada agora:

- **Identity** — todo objeto tem identidade estável (o `Id` interno de um App não é o nome do repositório, nem depende de identificadores externos).
- **Relationships** — relações entre objetos são explícitas (Squad → App → Repository / SonarProject / Link).
- **Provenance** — a origem dos dados é registrada (`source = SONAR_MOCK`, futuramente `SONAR`).
- **Temporalidade** — dados históricos têm timestamp claro e são imutáveis (`QualitySnapshot`).
- **Context** — o modelo permite, no futuro, relacionar um App a Product, Operation, Dependency, Documentation, SDD, Harness — sem criar essas entidades hoje.

### AI-friendly não é ter IA dentro (refinado em 2026-10-12)

Ser AI-friendly é ter **dados que qualquer IA consiga usar bem** — e as propriedades que tornam dados bons para uma LLM são as mesmas que os tornam bons para pessoas e integrações. Por isso a preparação acontece de forma orgânica, com uma regra: **cada preparação precisa ter valor hoje, sem IA**.

| Preparação | Valor hoje, sem IA | O que destrava depois | Situação |
|---|---|---|---|
| **Identidade estável e legível** | URLs, links entre telas | referências que uma LLM consegue citar | existe (slugs) |
| **Proveniência** (+ confiança quando inferido) | confiança no dado, auditoria | a IA cita a fonte e distingue confirmado de inferido — o principal antídoto contra alucinação | parcial; estender a todo dado novo |
| **Temporalidade** | evolução no tempo, a tese do MVP | perguntas como "o que mudou desde o mês passado" | existe nos snapshots; estender às dependências |
| **Relações como dados** (ligações com origem e confiança) | mapa de aplicações | grafo que a LLM navega: "o que depende de X" | criar com a primeira ligação app→app |
| **API completa e documentada** | frontend, integrações | ferramentas para agentes (ver abaixo) | OpenAPI exposto; manter completo |
| **Vocabulário do domínio explícito** | alinhar termos entre squads ("modal", "carrinho", "solução") | camada semântica para a LLM interpretar termos | existe: `.agents/product/glossary.md` (a revisar) |
| **Campos de texto** (descrição, README resumido, observações) | leitura humana | busca semântica (RAG) | quando surgirem naturalmente |

**O primeiro passo de IA provavelmente não é IA dentro do CheckApps.** O time já usa Claude, Copilot e Devin. O caminho mais curto para gerar valor com IA é o CheckApps virar **fonte de contexto para essas ferramentas**, via um servidor MCP (o protocolo que permite a uma LLM chamar ferramentas externas): o CheckApps expõe consultas determinísticas — "apps da squad X", "quem usa a biblioteca Y", "o que está pendente para o app Z" — e o tech lead pergunta no próprio assistente: *"quais apps da minha squad ainda estão no .NET 8 e qual a qualidade deles?"*. Nenhuma LLM dentro do CheckApps, nenhum custo de modelo, nenhuma questão nova de governança além da própria API — e é a visão de "camada de contexto" implementada com o protocolo feito para isso.

**Infraestrutura de IA vem do banco que já está no plano.** No Postgres, grafo é uma tabela de ligações com consultas recursivas, e busca semântica é a extensão `pgvector`. Não há necessidade prevista de banco vetorial nem de banco de grafo separados.

**O que fica fora até haver demanda real**: embeddings, banco vetorial separado, banco de grafo, framework de agentes, e ontologia genérica de "tudo se relaciona com tudo" desenhada antes da primeira relação concreta.

**Como isso se mantém orgânico**: o template de spec pergunta, em cada feature, se os dados novos têm identidade estável, relações explícitas, proveniência e data. O CheckApps fica AI-friendly spec a spec, guiado por demanda — não por uma "fundação de IA" construída antecipadamente.

**Métrica ≠ avaliação.** Uma métrica é um dado observado (`coverage = 72.4`). Um Quality Level (`B`) é uma classificação derivada de regras organizacionais, que evoluem. As duas camadas são separadas de propósito, e cada snapshot guarda qual versão da regra o classificou.

## Público

- **Primário hoje**: QAs e coordenadores de squad que precisam ver "como estão meus apps" e "estão melhorando ou piorando".
- **Futuro**: times de engenharia em geral (arquitetura, operações, produto) usando o CheckApps como camada de contexto — e agentes de IA consumindo esse contexto.
