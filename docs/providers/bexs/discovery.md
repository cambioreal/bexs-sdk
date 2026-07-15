# Bexs (Ebury Bank) — Discovery

Status: fase de descoberta concluída (2026-07-15). Sonda §0.8 do goal loop **verde** — auth +
leitura de recurso real confirmados contra o sandbox vivo (diferente do precedente BS2, onde só a
auth funcionava). SDK/gateway ainda não iniciados.
Provider order position: **2 of 9** (`GOAL-provider-standalone-sandbox-loop.md`); BS2 (1 of 9) em
stand-by por bloqueio externo.
Verified: 2026-07-15, contra `cambio-real-v2/bexs/demo-env` no sandbox vivo + legado `cerebro`
(read-only) + documentação oficial vigente (portal Ebury/DigitalFX).

## 1. Perfil no Provider Protocol

**`Sync`** (submit/status/cancel/refund), perfil **PIX payin puro** com conversão FX embutida
(consumidor paga BRL via PIX; liquidação em moeda estrangeira para o merchant). Confirmado contra
`provider-protocol/docs/PROVIDER-MAP.md` §linha Bexs ("Bexs | PIX payin | ... ✅ 200+JWT (path
correto é `/v1/token`, não `/oauth/token`)") e `RFC-provider-protocol.md`.
Não há fluxo quote→identity→transfer (não é `Async`); a cotação (`exchange-rate`) é leitura
informativa, não quote vinculante com id. Bexs não implementará `ISyncProviderAdapter` formalmente
nesta wave — mesma decisão de Kira/Ripple/BS2 (SDK modela a API nativa; gateway traduz para
`Envelope<T>`). Lacuna conhecida, tratada em `RFC-provider-protocol.md` §6.

## 2. Ambiente e conectividade

| | Sandbox (demo) | Produção |
|---|---|---|
| Auth URL | `https://auth.bexs.com.br/v1/` | `https://auth.bexs.com.br/v1/` (mesma) |
| Base URL | `https://sandbox.bexs.com.br/v1/` | `https://apis.bexs.com.br/v1/` |
| Audience | `payin-package-sandbox` | `https://forex.bexs.com.br` |
| Credencial | `pass cambio-real-v2/bexs/demo-env` (`CLIENT_ID`/`CLIENT_SECRET`/`AUDIENCE`) | não aprovisionada neste loop |
| mTLS / IP allowlist | Nenhum (outbound). | — |
| Webhook secret | **não existe** (`BEXS_WEBHOOK_SECRET` vazio no legado; nada no `pass`) | — |

Nenhuma credencial foi impressa, logada ou copiada para este arquivo — apenas nomes de entradas no
`pass`.

⚠️ **Achado de inventário (não corrigir no legado, que é read-only):** as credenciais demo estão
**hardcoded e versionadas** em `cerebro/config/bexs.php` (mesmo padrão dos achados S7/S8). A
entrada do `pass` contém exatamente os mesmos valores — ou seja, a credencial que temos é a demo
histórica do produto "payin package". O novo repo nunca versiona esses valores; runtime só via
env/`pass`.

## 3. Divergência legado ↔ documentação oficial vigente (§0.7 do goal)

**A Bexs foi absorvida pela Ebury** (Ebury Bank Brasil). Estado atual da documentação pública:

- `developers.bexs.com.br` → **301** para `developers.ebury.com.br` (seções GETTING STARTED,
  WEBHOOKS, ONBOARDING, EXCHANGE, BANKING, PAYMENTS; status page `status.eburybank.com.br`).
- As specs OpenAPI públicas em `digitalfx.bexs.com.br/specs/` documentam **dois produtos novos**:
  `swagger_webpayments.yaml` (WebPayments — remessas com merchants/buckets/checkout, paths
  `/v1/webpayments/*`, server `api-sandbox-digitalfx.bexs.com.br`) e `swagger_digitalfx.yaml`
  (Digital FX). Auth desses produtos: `POST https://api-sandbox-auth.bexs.com.br/v1/oauth/token`,
  **form-urlencoded** (`client_id`, `client_secret`, `grant_type`), sem `audience`.
- **A nossa credencial NÃO vale nos produtos novos**: sonda ao vivo em 2026-07-15 retornou
  `400 {"error":"invalid_client"}` no `oauth/token` novo (com e sem audience).
- **O produto que o legado usa ("payin package") continua vivo e acessível** nos hosts antigos
  (`auth.bexs.com.br` + `sandbox.bexs.com.br`), com contrato idêntico ao que o `cerebro` assume.
  Não há documentação pública vigente desse produto (o portal antigo foi substituído pelo da
  Ebury); a fonte de verdade do contrato é o legado + as sondas ao vivo deste documento.

**Decisão:** o escopo do SDK/gateway é o **payin package** (API antiga, viva, com credencial
válida). Migração para WebPayments/DigitalFX exigiria credencial nova + onboarding comercial
Ebury — registrado como risco/limite (§10), não como escopo. As specs baixadas dos produtos novos
ficam guardadas como referência em `docs/providers/bexs/upstream-specs/` para eventual migração.

## 4. Auth — OAuth2 client_credentials (estilo Auth0, corpo JSON)

- `POST {auth_url}token` → `POST https://auth.bexs.com.br/v1/token`, `Content-Type: application/json`.
- Corpo **JSON** (não form-urlencoded — diverge do produto novo da Ebury e do padrão RFC 6749):
  `{ "grant_type": "client_credentials", "client_id": ..., "client_secret": ..., "audience": ... }`.
  `audience` é obrigatória e discrimina o produto/ambiente (`payin-package-sandbox`).
- Resposta: `{ access_token, token_type: "Bearer", expires_in: 3600 }`.
- Aplicado como `Authorization: Bearer <access_token>`.
- Legado cacheia o token por 50 min (TTL real 60 min) com um TODO reconhecendo risco de token
  expirado em cache. O SDK novo: cache com margem de segurança derivada de `expires_in` (não
  hardcoded), single-flight, e no máximo 1 retry em 401 pós-token (padrão canônico do goal —
  melhoria deliberada sobre o legado, que aborta sem retry).

**Validado ao vivo 2026-07-15** (sonda não financeira, sandbox real):

| Sonda | Resultado |
|---|---|
| `POST /v1/token` (JSON, audience payin) | ✅ `200`, `token_type=Bearer`, `expires_in=3600` |
| `GET /v1/exchange-rate?from=BRL&to=USD` | ✅ `200`, `{quotation_time, quotes:[{symbol:"USD", rate:"23.4840"}]}` (rate como **string**; valor dummy de sandbox) |
| `GET /v1/payments/{id fictício}` | ✅ `404` autenticado, corpo de domínio `{"code":"7","message":"Payment not found"}` — **não** é 403: acesso real ao recurso confirmado |
| `GET /v1/merchants` (listagem, não usada pelo legado) | ✅ `200` com dados reais (merchant "CambioReal Inc", `locked: true`, id com sufixo `V1` sem prefixo `B-`) |
| `GET /v1/merchants/{id fictício}` | ✅ `404`, corpo `{"message":"7 - Merchant not found"}` (código embutido na message — forma de erro inconsistente com a de payments) |
| `GET /v1/payments` (listagem) | ⚠️ `200` com corpo `null` — endpoint aceito mas semântica desconhecida (sem params?); investigar na fase SDK, não prometer no gateway |
| `POST oauth/token` novo (Ebury) | 🔴 `400 invalid_client` — credencial não migrada para os produtos novos |

## 5. Payments — PIX payin

Fonte de verdade: `cerebro/app/Libraries/EnvioBr/Bexs/{AbstractRequest,PixRequest}.php` +
`config/bexs-mock.php` (fixtures de resposta) + sondas ao vivo. Casing upstream: **snake_case**
em request e response; enums em SCREAMING_SNAKE/UPPER.

### Create — `POST /v1/payments`

```
type: "PIX"                      // único type usado; legado já teve "nupay" (só fee config, sem request class) — fora de escopo
currency: "BRL"
soft_descriptor: string          // "CambioReal Inc" no legado
amount: number                   // BRL, decimal (float no legado)
correlation_id: string           // código da transação do cliente (ex.: BR05248213768) — idempotência de negócio
consumer:
  external_id: string
  email: string
  type: "NATURAL_PERSON" | "LEGAL_PERSON"
  full_name: string              // NATURAL_PERSON (legado aplica cleanString)
  national_id: string            // CPF — NATURAL_PERSON
  commercial_name: string        // LEGAL_PERSON
  document_number: string        // CNPJ — LEGAL_PERSON
merchant_id: string              // ENVIADO SÓ EM PRODUÇÃO no legado; sandbox usa merchant default da conta
```

Resposta (mock `payment.pix.success`, confirmada em produção pelo legado):

```
id: string                       // "B-..." nos mocks
status: "WAITING_CONSUMER"       // estado inicial
type: "PIX"
correlation_id: string           // ecoado
qr_code: string                  // payload EMV copia-e-cola (não é URL de imagem)
expiration_datetime: ISO-8601 UTC ("Z")
soft_descriptor: string
amount_info:
  gross_amount: number           // BRL
  foreign_gross_amount: number   // USD
  financial_tax: number          // IOF
  fee_amount: number
events: [ { date, amount, foreign_amount, type: "AUTHORIZATION", status: "PENDING" } ]
```

Nota: o legado **ignora `expiration_datetime` da resposta** e computa client-side `now()+15min`.
O SDK expõe o campo do provider como está; TTL sintético é decisão de consumidor, não do SDK.

### Details — `GET /v1/payments/{id}`

Resposta = payment completo (mock `payment.pix.details`): mesmos campos do create, com `status`
avançado (`CONFIRMED`) e `events[]` acumulando `AUTHORIZATION` → `CONFIRMATION` → `SETTLEMENT`
(cada um com `{date, amount, foreign_amount, type, status: SUCCESS|PENDING}`), e
`amount_info.net_amount`/`tax_amount` adicionais.

### Cancel/refund — `POST /v1/payments/{id}/cancel`

Corpo vazio (`{}`). Semântica dependente do estado (mesmo modelo que a Ebury manteve no produto
novo): pagamento não pago → reversão/cancelamento do QR (não financeiro); pagamento confirmado →
reembolso (financeiro). O legado usa o mesmo endpoint para ambos (`PixRequest::refund`).

### Status enum (payin) — mapeamento do legado

| Bexs `status` | Legado (`getStatusPago`) | PaymentStatus ISO 20022 (gateway) |
|---|---|---|
| `WAITING_CONSUMER` | PENDING | `Pending` |
| `CONFIRMED` | PAID | `AcceptedSettlementCompleted` |
| `TRANSFERENCE` | PAID (aceito em `PaymentNotification::check`) | `AcceptedSettlementInProcess` |
| `WAITING_CANCELATION` | (re-poll; transitório) | `Pending` (com reason) |
| `CANCELED` | FAILED | `Cancelled` |
| `DECLINED_BY_ISSUER` | FAILED | `Rejected` |
| `DECLINED_BY_BUSINESS_RULES` | FAILED | `Rejected` |
| (qualquer outro) | PENDING | `Pending` (fail-safe do legado) |

## 6. Exchange-rate e Merchants

### `GET /v1/exchange-rate?from=BRL&to=USD` (leitura, informativa)

Resposta: `{ quotation_time: ISO-8601Z, quotes: [ { symbol: "USD", rate: "23.4840" } ] }`.
**`rate` é string** (precisão decimal preservada) — o SDK NÃO converte para double; expõe
`string`/`decimal` com parse invariant-culture. Não é quote vinculante (sem id, sem TTL).

### Merchants (cadastro de submerchant p/ compliance)

- `POST /v1/merchants` — corpo (confirmado em `EmpresaApiController::postCreateBexsMerchant`):
  `{ document, company: { name, trading_name, website_url }, fiscal_address: { street(≤30),
  number, city, state, country: "USA", zip_code } }`. Resposta: merchant completo com `id`.
  Passa por análise de compliance (`locked`/status `ANALYSING` até aprovação).
- `GET /v1/merchants/{id}` — detalhes (campos extras: `locked`, `logo`, `company.state_registration_number`,
  `state_registered`, `foundation_date`, `bacen_name`).
- `GET /v1/merchants` — **listagem existe** (validada ao vivo; o legado não a usa). Retorna array.
- Não há DELETE/cleanup de merchant — criação real em sandbox fica fora dos testes E2E default
  (sem cleanup ⇒ viola §0.4 do goal); coberta por contrato/fixture.

## 7. Webhooks / notificações — contrato upstream NÃO confirmado

- O fluxo battle-tested do legado é **polling-first**: `PaymentNotification::check` SEMPRE
  re-consulta `GET /v1/payments/{id}` e valida valor antes de confirmar; o cron
  `cambioreal:compensation` processa a fila de notificações pendentes.
- Os adapters novos do legado (`BexsWebhookAuthenticator` — HMAC-SHA256 em `X-Bexs-Signature`,
  fail-closed; `BexsWebhookNormalizer` — payload `{id, status, amount}` com statuses
  `COMPLETED|PENDING|FAILED|...`) estão explicitamente marcados **⚠️ CONFIRM-BEFORE-PROD**:
  header, esquema de assinatura e payload **nunca foram validados contra a Bexs real**, e o
  secret nem existe (`pass` não tem entrada; env vazio ⇒ fail-closed rejeita tudo).
- Os statuses do normalizer (`COMPLETED`/`APPROVED`/`EXPIRED`...) **não batem** com o vocabulário
  confirmado da API (`CONFIRMED`/`WAITING_CONSUMER`/`CANCELED`...) — mais um indício de que o
  normalizer é especulativo.
- **Decisão explícita (registrada, §1.6 do goal):** o gateway Bexs desta iteração **não expõe
  endpoint de webhook inbound**. Consumidores usam `GET /v1/bexs/payments/{id}` (polling), que é
  o único mecanismo confirmado. Se/quando o contrato de webhook do payin package for obtido
  (contato Ebury), abre-se um incremento com o padrão BS2 (webhook = gatilho de re-poll, nunca
  fonte de verdade).

## 8. Erros

Formas observadas (ao vivo + mock + código defensivo do legado):

1. `{"code":"7","message":"Payment not found"}` — payments 404; `code` numérico **string**.
2. `{"message":"7 - Merchant not found"}` — merchants 404; sem campo `code`, código embutido na
   message. O SDK extrai `message` sempre e `code` quando presente; não assume schema único.
3. Mock de falha de negócio: `{"code":"6","message":"Merchant blocked"}` (mesma forma de 1).
4. Auth inválida (host novo): `{"error":"invalid_client","error_description":...}` — só no
   produto novo; no host antigo não observamos a forma de erro de auth (credencial válida).

Catálogo de códigos não existe publicamente; `code` conhecidos: `6` (merchant blocked), `7` (not
found). O gateway preserva HTTP status + `code` upstream dentro de `ProblemDetail` (extensões),
sem inventar catálogo.

## 9. Idempotência, retry, polling

- **Idempotência**: não há header `Idempotency-Key` documentado/observado. A idempotência de
  negócio vem de `correlation_id` (payment create). O SDK expõe `correlation_id` como
  first-class e NÃO envia header de idempotência especulativo (diverge do BS2, onde decidimos
  enviar best-effort — aqui o endpoint é do produto legado congelado; menor superfície de
  surpresa; registrado como decisão).
- **Retry**: nenhum no legado. SDK: 1 retry em 401 (refresh de token), sem retry automático de
  5xx (deixado ao consumidor/gateway); timeout explícito de 30s (paridade com legado),
  configurável.
- **Polling**: verdade de status = `GET /v1/payments/{id}`. Sem lote. Cadência de re-poll é do
  consumidor (o cron do legado usa fila própria); o gateway não faz polling em background.
- **`verify => false` no legado** (TLS não verificado!) — defeito do legado, **não replicar**;
  SDK usa validação TLS padrão. Sondas ao vivo com TLS válido confirmam que não é necessário.

## 10. Matriz de cobertura — todos os endpoints

| # | Endpoint upstream | Método | Recurso SDK | Endpoint gateway | Perfil PP | Efeito | Fixture/cleanup | Teste esperado | Status sandbox |
|---|---|---|---|---|---|---|---|---|---|
| 1 | `/v1/token` | POST | `BexsTokenProvider` | interno (não exposto) | Sync (auth) | read/auth | n/a | unit (mock) + integração sandbox opt-in | ✅ validado ao vivo 2026-07-15 |
| 2 | `/v1/exchange-rate?from&to` | GET | `ExchangeRatesResource.GetAsync` | `GET /v1/bexs/exchange-rates?from&to` | Sync (rate, leitura) | read | n/a | unit + contrato + sandbox opt-in | ✅ validado ao vivo (200, rate string) |
| 3 | `/v1/payments` | POST | `PaymentsResource.CreateAsync` | `POST /v1/bexs/payments` | Sync (submit) | non-financial-write (QR não pago; nada liquida sem pagador) | cleanup = #6 cancel (reversão de não pago); `correlation_id` único | unit + contrato; sandbox E2E create→details→cancel (caso permitido §0.4, com cleanup) | 🔴 **bloqueio externo confirmado 2026-07-15** (11 sondas): com `merchant_id` ⇒ `403 {"code":"6","message":"Merchant blocked"}` nos 10 merchants (todos `locked` em compliance); sem `merchant_id` ⇒ `422 {"code":"42","message":"Invalid event flow on state machine"}`. Provisionamento do lado da Bexs; sensor no SandboxTests reexecuta o round-trip quando liberado |
| 4 | `/v1/payments/{id}` | GET | `PaymentsResource.GetAsync` | `GET /v1/bexs/payments/{id}` | Sync (status) | read | id de #3; id fictício → 404 | unit + contrato + sandbox (404 fictício ✅; details de id real após #3) | ✅ 404 autenticado validado ao vivo |
| 5 | `/v1/payments` (listagem) | GET | não prometido (semântica desconhecida — corpo `null`) | não exposto | — | read | n/a | só sonda exploratória na fase SDK | ⚠️ 200/null — comportamento indefinido |
| 6 | `/v1/payments/{id}/cancel` (não pago) | POST | `PaymentsResource.CancelAsync` | `POST /v1/bexs/payments/{id}/cancel` | Sync (cancel) | non-financial-write (reversão de QR não pago; é o cleanup de #3) | aplica-se a #3 | unit + contrato + sandbox E2E (cleanup do caso #3) | 🔴 bloqueado transitivamente (depende de #3 criar; mesmo bloqueio externo) |
| 7 | `/v1/payments/{id}/cancel` (pago = refund) | POST | idem #6 (mesmo método) | idem #6 | Sync (refund) | **financial-write** | exige pagamento CONFIRMED (impossível sem pagador sandbox) | contrato/fixture; **bloqueado — sem autorização e sem meio de pagar o QR no sandbox** | 🔴 bloqueado (autorização pendente + sem simulador de pagamento) |
| 8 | `/v1/merchants` | POST | `MerchantsResource.CreateAsync` | `POST /v1/bexs/merchants` | Sync (cadastro) | non-financial-write **sem cleanup** (sem DELETE; entra em fila de compliance) | sem cleanup ⇒ não roda por default | unit + contrato/fixture; sandbox só com autorização específica | 🔴 não executar (sem cleanup; §0.4) |
| 9 | `/v1/merchants/{id}` | GET | `MerchantsResource.GetAsync` | `GET /v1/bexs/merchants/{id}` | Sync (status cadastro) | read | id real da listagem; fictício → 404 | unit + contrato + sandbox (ambos ✅ ao vivo) | ✅ validado ao vivo (404 fictício + detalhe via lista) |
| 10 | `/v1/merchants` (listagem) | GET | `MerchantsResource.ListAsync` | `GET /v1/bexs/merchants` | Sync (leitura agregada) | read | n/a | unit + contrato + sandbox opt-in | ✅ validado ao vivo (200 com merchant real) |
| 11 | webhook inbound | POST (inbound) | n/a | **não exposto nesta iteração** (decisão §7) | Sync (event) | — | — | — | ⚪ contrato upstream inexistente/não confirmado |

**Resumo do gate de leitura (§0.8):** dos 10 endpoints upstream reais, **5 exercitados ao vivo com
sucesso** (auth, exchange-rate, payments 404-autenticado, merchants list, merchants 404), **1
bloqueado por ser financeiro** (refund de pago — sem autorização e sem pagador sandbox), **1
não executável com segurança** (merchant create sem cleanup), **1 de semântica indefinida**
(listagem de payments).

**Atualização pós-SDK (2026-07-15, via SDK real):** os 2 endpoints antes classificados como
"elegíveis a E2E com cleanup" (payment create + cancel) estão sob **bloqueio externo de
provisionamento de escrita**: todos os 10 merchants da conta sandbox estão `locked` em compliance
(create com `merchant_id` ⇒ `403`/code `6` "Merchant blocked", testado individualmente nos 10) e o
fluxo default sem `merchant_id` responde `422`/code `42` "Invalid event flow on state machine".
Diferente do precedente BS2 (bloqueio total, até leitura), aqui o bloqueio é **parcial — só
escrita**: toda a superfície de leitura funciona ao vivo. Não resolvível por código; requer a Bexs
aprovar/destravar um merchant sandbox ou reabilitar o fluxo default. O teste sensor
(`PaymentCreateGetCancelRoundTripsLive`) reporta o estado e executará o round-trip completo com
cleanup automaticamente quando o provisionamento for liberado.

## 11. Lacunas, suposições e riscos

1. **Produto legado sem documentação pública vigente**: o payin package não aparece mais no
   portal Ebury; risco de descontinuação/sunset sem aviso público. Mitigação: contrato modelado a
   partir do legado + sondas vivas; specs dos produtos novos guardadas para migração futura.
   Monitorar `status.eburybank.com.br`.
2. **Credencial única (demo)**: não há credencial de produção no `pass`; produção exigirá
   onboarding/contato Ebury (a cargo do dono, fora do loop).
3. **Semântica de `GET /v1/payments` (lista)** desconhecida (200/null) — não prometida no SDK até
   sonda com params na fase 2.
4. **Webhook**: contrato upstream não confirmado (§7) — gateway sem endpoint inbound nesta
   iteração; polling é o único mecanismo confirmado.
5. **Refund de pagamento confirmado nunca exercitável em sandbox** sem um simulador de pagamento
   PIX (não descoberto até agora) — permanece contrato/fixture-only, com bloqueio registrado.
6. **`rate` string e valores dummy no sandbox** (BRL→USD "23.4840" é irreal) — testes E2E validam
   forma, não plausibilidade do valor.
7. **`merchant_id` só em produção** no create do legado — em sandbox o merchant default da conta é
   usado. O SDK expõe `merchant_id` opcional; gateway repassa; documentado para evitar surpresa
   na promoção a produção.
8. Suposição: cancel de QR não pago não tem efeito financeiro (semântica de reversão, consistente
   com o produto novo da Ebury e com o uso do legado). Se a Fase 2 observar comportamento
   diferente na E2E real, reclassificar #6 e parar antes do gateway.

## 12. Limites de responsabilidade SDK / gateway / plataforma

- **SDK (`bexs-sdk` / `CambioReal.Bexs.Client`)**: modela a API payin package nativa (payments,
  exchange-rate, merchants) com DTOs snake_case fiéis; auth encapsulada em
  `IBexsTokenProvider` + `DelegatingHandler` (cache por `expires_in`, single-flight, 1 retry em
  401); zero dependência de `CambioReal.Contracts`; sem normalização ISO 20022; sem HTTP de
  plataforma; nunca loga segredos/PII.
- **Gateway (`bexs-gateway`)**: Minimal API `/v1/bexs/*`; toda resposta `Envelope<T>`; erros
  upstream → `ProblemDetail` (RFC 9457 + `code`/`retryable`/`severity`), preservando HTTP status
  e `code` Bexs em extensões; correlation ID (`X-Correlation-Id`) propagado; mapeamento de status
  → vocabulário ISO 20022 (`PaymentStatus`, tabela §5) acontece AQUI, não no SDK; OpenAPI/Scalar;
  health/readiness sem credencial.
- **Plataforma (consumidor)**: decide roteamento/contabilização/cadência de polling; TTL sintético
  de expiração de QR (15min do legado) é política de consumidor, não do gateway.

## 13. Nenhuma contradição arquitetural encontrada

Bexs payin package se encaixa integralmente no padrão canônico `Sync` + SDK/gateway standalone
(Kira/Ripple/BS2). Não é necessário ADR de exceção. Decisões locais registradas: (a) webhook fora
de escopo até contrato confirmado (§7); (b) sem `Idempotency-Key` especulativo (§9); (c) escopo =
API legada viva, não os produtos novos Ebury (§3).
