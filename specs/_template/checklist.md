# Checklist — Spec NNN: [Nome da feature]

Critério de "concluído". A spec só muda para `Implementada` quando tudo abaixo estiver marcado. Cada item deve ser verificável por alguém que não escreveu o código.

## Qualidade da spec (antes de codar)

- [ ] Todas as user stories têm cenários de aceite em Dado/Quando/Então
- [ ] Nenhum `[NEEDS CLARIFICATION]` restante — todos viraram decisão registrada em Assumptions ou Design notes
- [ ] Cada FR é testável (dá para escrever um teste que falha se ele não for atendido)
- [ ] Success Criteria são mensuráveis e não citam tecnologia
- [ ] Decisões estruturais têm ADR em `.agents/architecture/decisions/`
- [ ] Dados novos são AI-friendly: identidade estável, relações explícitas, proveniência (de onde veio; confiança, se inferido) e data definidas — ou "não se aplica" justificado (ver `.agents/product/vision.md`)

## Implementação

- [ ] [Incremento 1 — o que entrega e como se verifica]
- [ ] [Incremento 2 — …]
- [ ] Regras "conquistadas" durante a implementação registradas em `.agents/rules/`
- [ ] Modelo de dados atualizado em `.agents/architecture/data-model/` se houve mudança de chave/item
- [ ] Access patterns novos ou alterados registrados em `.agents/architecture/data-model/access-patterns.md`, com o fit de cada um

## Verificação

- [ ] `dotnet build` sem erros nem avisos
- [ ] `dotnet test` verde (unitários + integração)
- [ ] Endpoints novos exercitados via `curl`, incluindo os caminhos de erro (4xx)
- [ ] Fluxo completo navegado no browser (Playwright ou manual), sem erros no console
- [ ] `README.md` e `AGENTS.md` atualizados se algo mudou em como rodar/testar

## Encerramento

- [ ] `Status` desta spec atualizado para `Implementada`
- [ ] `.agents/project/STATE.md` reescrito
- [ ] `.agents/product/backlog.md` atualizado (item removido ou itens novos descobertos)
