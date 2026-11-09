# 07-aws-api-gateway

Contrato fictício no formato exportado do AWS API Gateway (jornada de fundos de investimento), com as extensões que costumam aparecer: `x-amazon-apigateway-integration` (`http_proxy` com VPC Link e `mock` no CORS), `x-amazon-apigateway-request-validator(s)`, `x-amazon-apigateway-gateway-responses`, `x-amazon-apigateway-binary-media-types` e o autorizador em `securitySchemes` (`x-amazon-apigateway-authtype` e `x-amazon-apigateway-authorizer`). Os `${stageVariables...}` são variáveis de estágio do gateway e ficam dentro de valores de texto.

- 5 endpoints: `GET /fundos`, `OPTIONS /fundos` (CORS), `GET /fundos/{codigoFundo}`, `POST /fundos/simulacoes` e `POST /fundos/ordens`. Nenhum aviso.
- O `OPTIONS` tem `content: {}` e nenhuma segurança: não gera campo nenhum nem quebra a leitura.
- Segurança dos demais: `autorizador-token (apikey)`; é só o nome e o tipo, nunca o `authorizerUri` nem o ARN.
- Campos da simulação: `cliente.contas[].numero` obrigatório, `valor` numérico obrigatório, `contaOrigem` opcional; resposta com `valorImposto`.
- Segredos plantados (`SEGREDO-PAPEL-DO-GATEWAY`, `SEGREDO-AUTORIZADOR`, o ID de conta `123456789012` e o host `exemplo.com`) estão só em extensões `x-*` e nunca podem aparecer no resultado.
