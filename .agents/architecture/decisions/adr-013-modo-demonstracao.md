# ADR-013: Modo demonstração como perfil escolhido na subida

**Status**: Aceita · **Data**: 2026-11-15

## Contexto

O CheckApps precisa de duas bases lado a lado: a real e uma de demonstração, que funciona sem conexão com repositório nenhum. A configuração hoje é montada separadamente por quatro pontos de entrada (a API e as ferramentas de seed, scan e análise), e cada um pode apontar para um banco e uma fonte diferentes. Alternativas consideradas:

- **Chave na tela que troca o banco em tempo de execução**: cria estado no processo, risco de misturar dados e de uma requisição ler um banco e gravar em outro.
- **Variáveis de ambiente soltas** (`Sqlite__DatabasePath`, `Scan__Source`…): funciona hoje, mas basta esquecer uma em um dos quatro pontos para gravar dado de exemplo no banco real.
- **Perfil único escolhido na subida**: uma chave, lida igual pelos quatro pontos, que aplica um conjunto fechado de valores.

## Decisão

- **`CheckApps:Mode` = `demo` | `real`** (padrão `real`). Valor inválido impede a subida.
- **No modo demo, uma camada de configuração entra por último** e vence `user-secrets`, variáveis e argumentos: banco de demonstração (`CheckApps:DemoDatabasePath`, padrão `demo.db`), fontes `fake` de descoberta e de análise, convenção de nome de demonstração e scan pela tela ligado (inofensivo com a fonte `fake`).
- **Um único helper** (`CheckAppsMode`) aplica a camada nos quatro pontos de entrada.
- **A base de demonstração é gerada por comando** (`CheckApps.Seed --demo`), descartável e reproduzível, e nunca versionada. O comando só apaga o arquivo do banco de demonstração; o nome do banco real é recusado.
- **O modo é visível**: as ferramentas imprimem o modo e o banco na primeira linha, e a tela mostra uma faixa no modo demo (`GET /api/info`).

## Consequências

- Trocar de modo é reiniciar a API com outra configuração. Não há troca de banco em tempo de execução.
- O modo real não muda em nada: sem a chave, a configuração é a mesma de antes.
- Um terceiro perfil (por exemplo, homologação compartilhada) entraria como outro valor da mesma chave, com a própria camada; não está previsto.
- Dados cadastrados durante uma demonstração se perdem no próximo `--demo`, de propósito.
