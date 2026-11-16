# Checklist — Spec 023: Modo demonstração

Critério de "concluído". A spec só muda para `Implementada` quando tudo abaixo estiver marcado. Cada item deve ser verificável por alguém que não escreveu o código.

**Status**: implementada e confirmada pelo usuário em 2026-11-15.

## Qualidade da spec (antes de codar)

- [x] Todas as user stories têm cenários de aceite em Dado/Quando/Então
- [x] Nenhum `[NEEDS CLARIFICATION]` restante
- [x] Cada FR é testável
- [x] Success Criteria são mensuráveis e não citam tecnologia
- [x] Decisões estruturais têm ADR em `.agents/architecture/decisions/` (ADR-013)
- [x] Dados novos são AI-friendly — não se aplica: não há dado novo persistido

## Implementação

- [x] Incremento 1: `CheckAppsMode` (leitura do modo, camada de sobreposição, descrição do banco) nos quatro pontos de entrada, com testes
- [x] Incremento 2: `DemoEnvironmentBuilder` e `Seed --demo` com a regra de segurança, com teste de integração
- [x] Incremento 3: `GET /api/info` e a faixa na tela
- [x] Incremento 4: `README.md` e skill `run-local`
- [x] Regras "conquistadas" registradas em `.agents/rules/`, se houver

## Verificação

- [x] `dotnet build` sem erros nem avisos
- [x] `dotnet test` verde (unitários + integração), sem alterar os testes existentes
- [x] `Seed --demo` com token configurado: sem rede, banco real intocado, resultado igual em duas execuções
- [x] `GET /api/info` em modo demo e em modo real
- [x] Tela em modo demo navegada (faixa, jornadas, contratos, mapa, checagem, busca no GitHub fake), sem erros no console
- [x] `README.md` e `AGENTS.md` atualizados
- [x] Confirmação do usuário na tela (2026-11-15; a faixa ganhou cor de texto fixa depois que o usuário apontou o contraste ruim no tema escuro)

## Encerramento

- [x] `Status` desta spec atualizado para `Implementada`
- [x] `.agents/project/STATE.md` reescrito
- [x] `.agents/product/backlog.md` atualizado
