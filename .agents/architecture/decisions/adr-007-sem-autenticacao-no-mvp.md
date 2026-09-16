# ADR-007: Sem autenticação nem autorização neste estágio

**Status**: Aceita · **Data**: 2026-09-16 · **Revisar quando**: o projeto sair do estágio de laboratório ou for exposto além da máquina local

## Contexto

O MVP começou só com leitura. Ao adicionar cadastro (criar/editar/inativar/remover), surgiu a pergunta se as operações de escrita precisariam de algum controle de acesso.

## Decisão

Nenhuma autenticação ou autorização por enquanto. Qualquer pessoa com a URL da API ou do frontend pode ler e escrever. Ações destrutivas pedem confirmação na UI, mas isso é UX, não segurança.

Justificativa: o projeto roda em máquina local contra DynamoDB Local, usado por uma pessoa. Adicionar auth agora seria construir infraestrutura para um problema que ainda não existe — exatamente o que a regra de ouro do produto pede para evitar.

## Consequências

- **Este ADR é uma dívida declarada**, não uma decisão definitiva. Antes de qualquer deploy compartilhado (mesmo interno), precisa ser substituído por um ADR de autenticação.
- Quando chegar a hora, a decisão provavelmente será "integrar com o IdP corporativo" — e a modelagem de quem pode fazer o quê (por squad? por papel?) é uma spec nova, não um detalhe de implementação.
- Não há campo de auditoria (`createdBy`, `updatedBy`) nas entidades. Adicionar depois é compatível com o modelo atual.
