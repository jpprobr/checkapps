# ADR-001: Stack — .NET 10, Angular 20, AWS DynamoDB

**Status**: Aceita; a escolha de persistência foi revista pelo ADR-009 (2026-10-04) · **Data**: 2026-09-13

## Contexto

O projeto evolui uma POC Python/Streamlit para uma aplicação web. Além de entregar valor, o MVP é declaradamente um **laboratório** de: modelagem de domínio, DynamoDB com design access-pattern-first, .NET 10, desenvolvimento assistido por IA, testes automatizados e arquitetura evolutiva. A stack foi escolhida por esses objetivos de aprendizado tanto quanto pelo produto.

## Decisão

- **Backend**: .NET 10 / C# / ASP.NET Core Web API.
- **Frontend**: Angular 20 (standalone components). Angular era a preferência declarada; não surgiu justificativa técnica forte para React. A versão 20 (não a 22, mais recente) é imposta pelo Node 22.17 da máquina de desenvolvimento — a CLI 22 exige Node ≥ 22.22.
- **Persistência**: AWS DynamoDB, em conta pessoal/dev para validação; DynamoDB Local (Docker) no dia a dia.
- **Dados de Sonar**: mockados atrás de `ISonarProvider`; integração real é passo futuro.

## Consequências

- O time (hoje uma pessoa + agentes) precisa dominar single-table design — custo assumido conscientemente como aprendizado.
- Angular 20 fica "preso" até o Node ser atualizado; `ng update` para 22 exige atualizar o Node antes.
- DynamoDB Local em `-inMemory` significa reseed a cada reinício — aceito em troca de custo zero e sem credenciais AWS no dia a dia.
