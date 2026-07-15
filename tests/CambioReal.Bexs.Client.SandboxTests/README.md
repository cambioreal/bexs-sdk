# CambioReal.Bexs.Client.SandboxTests

Testes de integração sandbox real, opt-in — nunca incluído em `Bexs.slnx`, nunca rodado por padrão
em CI (goal-loop §2.5).

## Rodar

```bash
set -a
eval "$(pass show cambio-real-v2/bexs/demo-env | sed -n \
  's/^CLIENT_ID=/BEXS_SANDBOX_CLIENT_ID=/p;s/^CLIENT_SECRET=/BEXS_SANDBOX_CLIENT_SECRET=/p')"
set +a
dotnet test tests/CambioReal.Bexs.Client.SandboxTests/CambioReal.Bexs.Client.SandboxTests.csproj
unset BEXS_SANDBOX_CLIENT_ID BEXS_SANDBOX_CLIENT_SECRET
```

Sem as duas variáveis de ambiente, os testes falham explicitamente — não há skip silencioso.

O E2E de escrita (create→get→cancel de um QR não pago, não financeiro com cleanup) exige opt-in
adicional: `BEXS_SANDBOX_ALLOW_WRITE=1`.

## Última execução ao vivo — 2026-07-15 (com `BEXS_SANDBOX_ALLOW_WRITE=1`)

```
Passed MerchantGetWithFictitiousIdReturns404 [956 ms]
  GET merchants/{fictício}: HTTP 404 — forma de erro sem campo code tolerada.
Passed ExchangeRateReadsLive [528 ms]
  GET exchange-rate: 200 OK, quotation_time=2026-07-15T17:06:09Z, rate parseável.
Passed PaymentGetWithFictitiousIdReturnsAuthenticatedDomain404 [421 ms]
  GET payments/{fictício}: HTTP 404, code=7 — acesso real ao recurso confirmado.
Passed PaymentCreateGetCancelRoundTripsLive [845 ms]
  POST payments: HTTP 422, code=42 — bloqueio de provisionamento sandbox conhecido
  (merchants locked em compliance / fluxo default indisponível).
Passed MerchantsListAndDetailsReadLive [683 ms]
  GET merchants: 200 OK, 10 merchant(s). GET merchants/{id}: 200 OK, locked=True.
Passed AuthenticatesLiveAgainstSandbox [436 ms]
  payin-package-sandbox: token issued, length=1726 (masked).

Passed: 6, Failed: 0, Total: 6
```

Confirma, através do SDK real (não `curl` manual): autenticação e **toda a superfície de leitura**
funcionam ao vivo (exchange-rate, payments details/404 de domínio, merchants list/get). A ESCRITA
(payment create/cancel) está sob bloqueio externo de provisionamento — todos os 10 merchants da
conta sandbox `locked` em compliance (`403`/code `6` em cada um) e fluxo default `422`/code `42` —
ver `docs/providers/bexs/discovery.md` §10. `PaymentCreateGetCancelRoundTripsLive` é um sensor:
nunca falha pelo bloqueio conhecido em si; quando a Bexs aprovar um merchant, ele passa a executar
o round-trip completo (create→get→cancel com cleanup) e falhará se o fluxo regredir.
