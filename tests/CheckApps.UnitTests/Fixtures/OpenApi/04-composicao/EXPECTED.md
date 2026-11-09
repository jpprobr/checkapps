# 04-composicao

- `allOf` une os campos: `codigo` e `valor`, ambos obrigatórios; a mesma referência duas vezes no `allOf` não é ciclo.
- `oneOf` lista `a`, `b` e `comum`: nenhum obrigatório, com nota, e o endpoint fica com confiança média.
- Ciclo: `nome` e `pai`, com `pai` não expandido. É **nota**, não aviso: nada se perdeu.
- `$ref` para outro arquivo: nada é buscado, há **aviso** e o endpoint fica com confiança média.
