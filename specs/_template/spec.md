# Spec NNN: [Nome da feature]

**Status**: Rascunho | Em análise | Aprovada | Em implementação | Implementada
**Criada em**: AAAA-MM-DD
**Origem**: [pedido do usuário, item do backlog, ADR, incidente — de onde veio a demanda]

> Formato compatível com o template de spec do Spec-Kit (github/spec-kit). Preencha as quatro seções obrigatórias antes de qualquer código. Marque ambiguidades com `[NEEDS CLARIFICATION: pergunta]` em vez de assumir — elas viram perguntas para o usuário antes da aprovação.

## User Scenarios & Testing *(obrigatório)*

### User Story 1 — [Título curto] (Prioridade: P1)

[Quem faz o quê, para quê, em linguagem simples. Uma jornada, não uma lista de features.]

**Por que esta prioridade**: [O que se perde se isso não existir; por que é P1 e não P2.]

**Teste independente**: [Como esta história pode ser demonstrada sozinha, sem as outras. Se não puder, provavelmente não é uma história.]

**Cenários de aceite**:

1. **Dado** [estado inicial], **Quando** [ação], **Então** [resultado observável].
2. **Dado** …, **Quando** …, **Então** ….

### User Story 2 — [Título] (Prioridade: P2)

…

### Edge Cases

- O que acontece quando [condição de borda]?
- Como o sistema se comporta se [erro / dado inválido / concorrência]?

## Requirements *(obrigatório)*

### Functional Requirements

- **FR-001**: O sistema DEVE [capacidade específica e testável].
- **FR-002**: O sistema DEVE [...].
- **FR-003**: [NEEDS CLARIFICATION: pergunta objetiva quando o brief não define algo — ex.: "o que acontece com X quando Y?"]

### Key Entities

- **[Entidade]**: o que representa, atributos relevantes para esta spec, relação com outras entidades. Não repita o modelo de dados inteiro — aponte para `.agents/architecture/data-model/` quando for o caso.

## Success Criteria *(obrigatório)*

Mensuráveis e agnósticos de tecnologia — "um QA consegue X em Y segundos", não "o endpoint responde em 200ms".

- **SC-001**: [resultado observável pelo usuário ou pelo negócio, com número quando possível].
- **SC-002**: [...].

## Assumptions

- [Premissa sobre usuários, ambiente, escopo ou dependências que a spec toma como dada.]
- [Decisão tomada por falta de definição, explicitada para poder ser contestada.]

## Design notes *(opcional)*

Decisões técnicas, trade-offs, alternativas descartadas e gotchas descobertos na implementação que não cabem nas seções acima. Se crescer demais, vira `plan.md` ao lado. Decisões estruturais viram ADR em `.agents/architecture/decisions/`, não ficam só aqui.
