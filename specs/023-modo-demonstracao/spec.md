# Spec 023: Modo demonstração

**Status**: Implementada (2026-11-15), confirmada pelo usuário na tela
**Criada em**: 2026-11-15
**Origem**: pedido do usuário (2026-11-15). O CheckApps vai passar a usar dados reais, mas precisa continuar demonstrável a qualquer momento e **sem conexão** com repositório nenhum. Hoje uma demonstração exige rodar seed, scan e análise na mão, fazer a triagem pela tela e garantir que todos os comandos apontem para o mesmo banco; um erro de variável grava dado de exemplo no banco real. Desenho registrado no `backlog.md` em 2026-11-15.

> Esta spec não muda nenhuma funcionalidade já entregue. As specs 021 (contratos) e 022 (jornadas) aparecem aqui só como **dados de exemplo** que a base de demonstração traz prontos.

## Escopo e fronteira

**Faz parte**

1. Um **modo** escolhido na subida (`CheckApps:Mode` = `demo` | `real`), que vale igual para a API e para as três ferramentas de linha de comando.
2. No modo demo, a configuração passa a apontar para um **banco separado** e para as **fontes de demonstração**, sem rede e sem token.
3. Um comando que **monta a base de demonstração completa** do zero, sem rede.
4. Um **aviso na tela** quando a aplicação está em modo demo, e a indicação do modo e do banco na saída das ferramentas.

**Não faz parte**

- Trocar de modo com a aplicação rodando; mais de dois modos; banco de demonstração versionado no Git.
- Autenticação e dados reais (a configuração real continua local).
- Qualquer mudança no modo real: sem `CheckApps:Mode`, tudo funciona como antes.

## User Scenarios & Testing *(obrigatório)*

### User Story 1 — Montar a demonstração com um comando (Prioridade: P1)

Como pessoa que vai apresentar o CheckApps, rodo um comando e tenho uma base completa de demonstração, sem depender de rede nem de token.

**Por que esta prioridade**: sem a base pronta, a demonstração depende de passos manuais e de acesso a repositórios que podem não estar disponíveis.

**Teste independente**: rodar o comando numa máquina com token configurado e conferir o banco, sem subir a API.

**Cenários de aceite**:

1. **Dado** qualquer configuração local, **Quando** rodo `dotnet run --project tools/CheckApps.Seed -- --demo`, **Então** o banco de demonstração é recriado do zero com squads, apps, histórico de qualidade, repositórios descobertos, um app com contrato analisado em dois commits, uma árvore de jornadas com passos e impacto, e uma checagem com status variados.
2. **Dado** um token do GitHub configurado, **Quando** monto a demonstração, **Então** nenhuma chamada de rede é feita: as fontes são as de demonstração.
3. **Dado** o banco real com dados, **Quando** monto a demonstração, **Então** o banco real não é tocado.
4. **Dado** o comando rodado duas vezes, **Então** o resultado é o mesmo.
5. **Dado** um banco de demonstração configurado com um nome que é o do banco real, **Então** o comando se recusa a apagá-lo, com mensagem.

### User Story 2 — Subir a aplicação em modo demo (Prioridade: P1)

Como pessoa que vai apresentar, subo a API e a tela em modo demo e vejo claramente que estou numa demonstração.

**Teste independente**: subir com `CheckApps__Mode=demo` e abrir a tela.

**Cenários de aceite**:

1. **Dado** `CheckApps__Mode=demo`, **Quando** a API sobe, **Então** ela usa o banco de demonstração e as fontes de demonstração, mesmo que haja token e banco real configurados.
2. **Dado** o modo demo, **Quando** abro a tela, **Então** uma faixa fixa diz "Modo demonstração: dados de exemplo, sem conexão com repositórios reais".
3. **Dado** o modo demo, **Quando** uso "Buscar no GitHub" na tela Repositórios, **Então** a busca funciona com a fonte de demonstração.
4. **Dado** nenhum modo configurado, **Então** não há faixa, o banco é o real e tudo funciona como antes.
5. **Dado** um valor de modo inválido (por exemplo, `teste`), **Então** a aplicação e as ferramentas recusam subir, com mensagem que diz os valores aceitos.

### User Story 3 — Saber onde as ferramentas estão gravando (Prioridade: P2)

Como pessoa que roda seed, scan e análise, vejo na primeira linha da saída o modo e o banco, para nunca gravar no lugar errado.

**Cenários de aceite**:

1. **Dado** qualquer modo, **Quando** rodo uma das três ferramentas, **Então** a primeira linha diz o modo e o caminho do banco.

### Edge Cases

- **Argumento `--source github` em modo demo**: a fonte continua a de demonstração; o modo vence.
- **Banco de demonstração com outro nome** (`CheckApps:DemoDatabasePath`): respeitado, desde que não seja o nome do banco real.
- **API antiga rodando com o modo errado**: a faixa (ou a falta dela) mostra o modo de verdade.

## Requirements *(obrigatório)*

### Functional Requirements

- **FR-001**: O modo DEVE ser lido de `CheckApps:Mode` (`demo` | `real`, padrão `real`, sem diferença de caixa) por um único helper usado pela API e pelas três ferramentas. Outro valor DEVE impedir a subida, com mensagem.
- **FR-002**: No modo demo, a configuração DEVE ganhar uma camada final que vence as demais: `Sqlite:DatabasePath` = `CheckApps:DemoDatabasePath` (padrão `demo.db`), `Scan:Source` = `fake`, `Scan:NameTemplate` = `fake-{sigla}-` e `Scan:Enabled` = `true`.
- **FR-003**: `CheckApps.Seed --demo` DEVE ligar o modo demo, apagar e recriar o arquivo do banco de demonstração e montar a base descrita na US1 só com os serviços que já existem, sem SQL novo e sem rede.
- **FR-004**: O seed NÃO DEVE apagar um arquivo cujo nome seja o do banco real (`checkapps.db`), nem fora do modo demo.
- **FR-005**: As três ferramentas DEVEM imprimir na primeira linha o modo e o caminho do banco.
- **FR-006**: `GET /api/info` DEVE devolver o modo e o **nome** do arquivo do banco (sem o caminho).
- **FR-007**: A tela DEVE mostrar a faixa de demonstração só no modo demo, sem peso relevante no pacote inicial.
- **FR-008**: Sem `CheckApps:Mode`, o comportamento DEVE ser idêntico ao anterior (os testes existentes continuam verdes sem alteração).

### Key Entities

Não há dado novo persistido. O modo é configuração; a base de demonstração usa as entidades existentes, com origem `fake` (descoberta e análise) e `MANUAL` (jornadas e checagem), como qualquer outro dado.

## Success Criteria *(obrigatório)*

- **SC-001**: Em até 1 minuto, a partir de um clone novo, uma pessoa tem uma demonstração completa rodando sem rede.
- **SC-002**: Montar a demonstração nunca altera o banco real.
- **SC-003**: Ao abrir a tela, ninguém confunde dado de exemplo com dado real.

## Assumptions

- O banco de demonstração é descartável: cada `--demo` recria do zero, e o que for cadastrado durante uma demonstração se perde na próxima.
- As fontes de demonstração já existentes (`FakeRepositorySource` e `FakeRepositoryContentSource`) bastam para a base.

## Design notes *(opcional)*

- **Por que perfil na subida, e não chave na tela**: trocar o banco com o processo rodando misturaria dados e criaria estado num sistema que hoje não tem nenhum. Reiniciar a API com outro modo é simples e explícito. Decisão em `adr-013-modo-demonstracao.md`.
- **Por que `Scan:Enabled = true` no modo demo**: com a fonte de demonstração, o scan pela tela não fala com nada, e mostrar a busca faz parte da demonstração.
