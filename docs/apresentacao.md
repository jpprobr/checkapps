# CheckApps em 10 minutos

> Uma conversa rápida com coordenadores e tech leads: o que já existe, para onde vai e o que a gente ganha com isso. Situação em novembro de 2026.

## Por que isso existe

Começou pequeno: uma POC em Python que lia o Sonar, porque QA e coordenação queriam ver a qualidade dos apps e, principalmente, **se ela estava melhorando ou piorando**. A POC mostrava só o retrato do dia.

Daí a dor foi crescendo. A informação de cada app está espalhada: repositórios, Sonar, `appsettings`, contratos OpenAPI, planilhas, wiki. Ninguém tem a foto inteira. E a IA acelerou a escrita de código, mas **não acelerou a validação**: contrato que muda sem aviso, massa de teste que deixou de servir, impacto descoberto em homologação (às vezes depois).

O CheckApps quer ser o lugar onde essa foto se junta, **lida dos próprios repositórios**, e não digitada à mão.

## O que já dá para ver hoje

| A pergunta do dia a dia | Onde o CheckApps responde |
|---|---|
| Quais apps a minha squad tem, e em quais repositórios? | Catálogo, com leitura do GitHub por sigla e triagem |
| A qualidade está melhorando ou piorando? | Qualidade e evolução no tempo (dados do Sonar ainda simulados) |
| Quem ainda está no .NET 8? Quem usa o pacote X? | Pacotes e versão do .NET, por squad |
| Quem chama quem? | Integrações lidas das configurações e o mapa de aplicações |
| O contrato mudou? Isso quebra alguém? | Contratos OpenAPI, com o que mudou entre dois commits, classificado em **quebra**, **atenção** e **segura** |
| Que fluxo de negócio sente essa mudança? | Jornadas ligadas aos endpoints, com o impacto calculado |
| Uma demanda que atinge vários apps (subir versão, por exemplo) | Checagens, app a app, no lugar da planilha |

### Um exemplo de 30 segundos

Alguém torna o campo `contaOrigem` obrigatório no contrato da simulação e faz o commit. Depois da análise, a jornada **"Compra de Fundos"** mostra **quebra** no passo "Simular", com o campo, o commit anterior e o atual. Ninguém precisou descobrir isso em homologação.

## Como foi construído (e por que isso importa)

- Construído com agentes de IA, mas **com método**: cada funcionalidade tem uma spec, as decisões viram ADR, as regras aprendidas com bug ficam registradas, e o repositório tem um `AGENTS.md` que serve de contexto para Claude, Copilot e Devin.
- **695 testes automatizados**, e nenhum depende de rede.
- **Nada é inventado**: todo dado lido de código ou contrato diz de onde veio (arquivo, commit, leitor) e com que confiança. O que o sistema não conseguiu ler aparece como "não sei", nunca como um palpite.

## E o tal "AI-friendly"?

Não é colocar um chat dentro do app. É ter **dados que qualquer IA consiga usar sem alucinar**: identidade estável, origem, confiança, histórico e relações explícitas.

- **Regra de ouro**: a IA nunca grava fato. No máximo sugere, e uma pessoa confirma.
- **Próximo passo natural**: um **MCP** somente leitura. Com ele, o assistente que o time já usa pergunta direto ao CheckApps: "quais jornadas essa mudança de contrato quebra?", "quem ainda está no .NET 8?". Combina com o movimento de usar MCPs para produtividade.
- **Depois**: IA dentro dos fluxos, sempre como sugestão. Por exemplo, sugerir casos de teste a partir de uma mudança de contrato, ou explicar uma quebra em linguagem simples.

## Próximos passos

1. **Rodar com dados reais** de uma squad piloto, mantendo um **modo demonstração** que funciona sem conexão.
2. **Massas e "ficha do endpoint"**: contrato atualizado, massa válida e payload pronto para copiar. É a maior dor de quem testa hoje.
3. **MCP somente leitura**, para os assistentes de IA usarem esse contexto.
4. **Um agente testador à parte**, que usa esse contexto para validar jornadas em homologação.
5. **Login e permissões** antes de abrir para mais gente.

## O que eu espero que a gente ganhe

- Menos tempo garimpando contrato, massa e "quem chama quem".
- Quebra de contrato vista no commit, e não em homologação.
- Visão de impacto para TL e coordenação decidirem com dado.
- Contexto pronto para a IA do time trabalhar melhor: análise de impacto, testes, onboarding.

## Sendo honesto sobre os limites

- É um protótipo: roda local, sem login, e os dados do Sonar ainda são simulados.
- Os exemplos de contrato e de configuração ainda são inventados e precisam de casos reais para calibrar.
- Jornadas e massas dependem de alguém cadastrar. Por isso o dono de cada jornada é uma squad.
- O contrato mostra o que foi **declarado**, que pode estar desatualizado em relação ao que a aplicação faz. Comparar os dois está no plano.

## O que eu preciso de vocês

- Uma squad piloto e 15 minutos de feedback depois de usar.
- Acesso de leitura aos repositórios e contratos (idealmente por um GitHub App, e não por token pessoal).
- Um contrato real e uma página de massas, anonimizados, para calibrar.
- E a pergunta mais importante: **que pergunta vocês gostariam que ele respondesse?**
