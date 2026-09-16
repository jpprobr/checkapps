# ADR-006: Ids de Squad e App são gerados como slug do nome, com sufixo em colisão

**Status**: Aceita · **Data**: 2026-09-16

## Contexto

O domain model exige que o `Id` de um App seja um identificador interno estável — não o nome do repositório, não um id corporativo externo (esses ficam em `ExternalId`). Ao abrir cadastro manual, era preciso decidir se a pessoa digita o id ou o sistema gera.

## Decisão

- O id é **gerado** a partir do nome: `Slug.Generate` (lowercase, sem acentos, não-alfanumérico vira `-`, colapsa e trima). `"Squad Pagamentos"` → `squad-pagamentos`.
- Colisão é resolvida por tentativa atômica: `PutItem` com `attribute_not_exists(PK)`; se falhar, tenta `slug-2`, `slug-3`, … (`SlugIdGenerator`, até 50 tentativas). A atomicidade fica no DynamoDB — não há "checar se existe, depois gravar".
- O id **nunca muda** depois de criado, mesmo que o nome mude. Renomear "Checkout API" para "Checkout Service" mantém `checkout-api`.
- Ids de `AppLink` e `QualitySnapshot` são GUIDs (`Guid.NewGuid().ToString("n")`) — não têm "nome" natural para slugificar.

## Consequências

- URLs legíveis (`/apps/checkout-api`) sem expor ao usuário um detalhe técnico que ele teria que administrar.
- Um app renomeado pode ficar com id "desatualizado" em relação ao nome. É aceitável e intencional — o id é identidade, não rótulo.
- O sufixo numérico é best-effort; um número muito grande de colisões consecutivas falha após 50 tentativas. Irrelevante na escala do projeto.
