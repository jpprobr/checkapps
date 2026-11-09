# 01-oas3-yaml

OpenAPI 3.0 em YAML, dentro de `contracts/` (nome de arquivo sem padrão: é a pasta que o torna candidato).

- 3 endpoints. `200:` sem aspas vira o status `200`; `version: 1.10` sem aspas continua `1.10`.
- O `GET` e o `POST` herdam a segurança do documento (`oauth (oauth2)`); o `DELETE` declara `security: []` e não tem nenhuma.
- Campos achatados: `cliente.contas[].numero` (obrigatório), `cliente.apelidos` com tipo `array<string>`.
