# Checklist — Spec 021: Contratos OpenAPI

Critério de "concluído". A spec só muda para `Implementada` quando tudo abaixo estiver marcado. Cada item deve ser verificável por alguém que não escreveu o código.

**Status**: implementada em 2026-11-05; lida no GitHub real e confirmada pelo usuário na tela em 2026-11-08.

## Qualidade da spec (antes de codar)

- [x] Todas as user stories têm cenários de aceite em Dado/Quando/Então
- [x] Nenhum `[NEEDS CLARIFICATION]` restante — todos viraram decisão registrada em Assumptions ou Design notes
- [x] Cada FR é testável (dá para escrever um teste que falha se ele não for atendido)
- [x] Success Criteria são mensuráveis e não citam tecnologia
- [x] Decisões estruturais têm ADR em `.agents/architecture/decisions/` (adendo da spec 021 no ADR-011)
- [x] Dados novos são AI-friendly: identidade estável, relações explícitas, proveniência e data definidas

## Implementação

- [x] Incremento 1: refatoração dos tipos de fato (processadores e escritores), mesmos 434 + 95 testes verdes, sem alterar asserção
- [x] Incremento 2: protótipo do parser e `OpenApiScanner`, com fixtures em `Fixtures/OpenApi/`
- [x] Incremento 3: tabelas, processador, escritor, contagem na ferramenta, fonte fake com revisão e `--fake-revision`
- [x] Incremento 4: `ContractComparer` e os dois endpoints de leitura
- [x] Incremento 5: seção **Contratos** no detalhe do app
- [x] Regras "conquistadas" durante a implementação registradas em `.agents/rules/`
- [x] Modelo de dados atualizado em `.agents/architecture/data-model/`
- [x] Access patterns novos registrados em `.agents/architecture/data-model/access-patterns.md`

## Verificação

- [x] `dotnet build` sem erros nem avisos
- [x] `dotnet test` verde (unitários + integração)
- [x] Endpoints novos exercitados via `curl`, incluindo o 404
- [x] Mesma análise repetida não grava nada; `--dry-run` nunca grava
- [x] Segredos plantados não aparecem no banco nem na saída
- [x] Fluxo completo navegado no browser em banco isolado, sem erros no console
- [x] Um repositório de contrato de demonstração analisado no GitHub real (2026-11-08: 5 endpoints no formato AWS API Gateway; depois de um commit, as 2 quebras e a mudança segura apareceram na tela)
- [x] `README.md` e `AGENTS.md` atualizados se algo mudou em como rodar/testar
- [x] Confirmação do usuário na tela (2026-11-08; ajustes visuais ficam para depois)

## Encerramento

- [x] `Status` desta spec atualizado para `Implementada`
- [x] `docs/leitor-github.md` com a seção dos contratos
- [x] `.agents/project/STATE.md` reescrito
- [x] `.agents/product/backlog.md` atualizado
