# ADR-005: Remoção é hard delete, mas bloqueada quando há dependentes; inativação é o caminho normal

**Status**: Aceita · **Data**: 2026-09-16

## Contexto

Ao adicionar cadastro manual de Squads e Apps (`specs/002-squad-app-management/`), foi preciso decidir o que "remover" significa. Alternativas consideradas: hard delete sempre (destrutivo, perde histórico), só inativação (seguro, mas não limpa cadastros feitos por engano), ou hard delete condicional.

## Decisão

- **Inativar** (`status = "inactive"`) é a operação normal para tirar algo de circulação. É reversível e não apaga nada. Listagens ocultam inativos por padrão, com toggle para mostrar.
- **Remover** é hard delete, permitido **apenas** quando não há dependentes com valor histórico:
  - App: só se não tiver nenhum `QualitySnapshot`. Se tiver, `409 Conflict` com mensagem orientando a inativar.
  - Squad: só se não tiver nenhum App vinculado (ativo ou inativo). Senão, 409.
- Ao remover um App, seus filhos sem valor histórico próprio (`Repository`, `SonarProject`, `AppLink`) são apagados em cascata, sem confirmação individual.
- A guarda vive nos serviços de aplicação (`AppService.DeleteAsync`, `SquadService.DeleteAsync`), lançando `DomainConflictException`. Os repositórios não protegem nada.

## Consequências

- Um App que já recebeu um snapshot não pode mais ser removido pela API — nem por engano. Limpeza de dados de teste em ambiente local exige reset do DynamoDB Local (reseed), o que é aceitável.
- Serviços que criam snapshots (futuro sync real com Sonar) tornam apps "permanentes" na prática. É intencional: histórico de qualidade é o ativo central do produto.
- Não há soft delete com `deletedAt`; `status` é a única marca. Se um dia for necessário distinguir "inativo" de "arquivado", será um valor novo de `status`, não um campo novo.

## Nota (2026-10-12, spec 003)

A guarda do App ganhou uma segunda condição: **um App que está em qualquer checagem (aberta ou fechada) não pode ser removido** — participação em checagem também é histórico. `AppService.DeleteAsync` verifica as duas condições e responde 409 orientando a inativar.

Diferente do resto desta decisão, aqui há uma **segunda camada no banco**: a FK `check_items.app_id` não cascateia, então o SQLite recusa a remoção mesmo se o serviço falhar em checar. O princípio "os repositórios não protegem nada" continua valendo para a regra de negócio (a mensagem e o 409 vêm do serviço); a FK é rede de segurança, não a regra.

Checagem segue a mesma lógica: só é removida sem itens; com itens, é fechada (spec 003, FR-012).
