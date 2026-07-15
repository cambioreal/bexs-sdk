# bexs-sdk

Cliente .NET tipado (`CambioReal.Bexs.Client`) para a **Bexs (Ebury Bank) Payin Package API** —
pagamentos PIX com câmbio embutido (consumidor paga BRL; liquidação em moeda estrangeira),
cotações informativas e cadastro de submerchants. Mesmo padrão do `cambioreal/kira-sdk`,
`cambioreal/ripple-sdk` e `cambioreal/bs2-sdk`: SDK modela a API nativa do provider, zero
dependência de `CambioReal.Contracts`, nenhum conhecimento de HTTP de plataforma.

```csharp
services.AddBexsClient(options =>
{
    options.Environment = BexsEnvironment.Sandbox;   // default deliberado
    options.ClientId = configuration["Bexs:ClientId"]!;
    options.ClientSecret = configuration["Bexs:ClientSecret"]!;
});

var client = provider.GetRequiredService<BexsClient>();

var rate = await client.ExchangeRates.GetAsync();                  // GET exchange-rate?from=BRL&to=USD
var payment = await client.Payments.CreateAsync(request);          // POST payments → QR síncrono
var details = await client.Payments.GetAsync(payment.Id!);         // GET payments/{id} — fonte de verdade
var reverted = await client.Payments.CancelAsync(payment.Id!);     // POST payments/{id}/cancel
var merchants = await client.Merchants.ListAsync();                // GET merchants
```

## Por que este pacote existe

O legado (`cerebro`, PHP) integra a Bexs dentro do monólito (`app/Libraries/EnvioBr/Bexs/*`).
Este SDK extrai o contrato confirmado do provider para um serviço standalone testável de ponta a
ponta contra o sandbox real, consumido pelo `bexs-gateway` (que traduz para o contrato canônico
`Envelope<T>`/`ProblemDetail` da plataforma).

## Bexs → Ebury: qual API é esta

A Bexs foi absorvida pela Ebury. O portal público atual (`developers.ebury.com.br`) documenta
produtos NOVOS (WebPayments/DigitalFX, hosts `api-sandbox-*.bexs.com.br`) onde **a credencial
deste projeto não vale** (`invalid_client`, sondado ao vivo). O produto que este SDK modela é o
**payin package** original — hosts `auth.bexs.com.br` + `{sandbox,apis}.bexs.com.br` — que segue
vivo e acessível, mas sem documentação pública vigente. Fonte de verdade do contrato: legado
somente leitura + sondas vivas registradas em [`docs/providers/bexs/discovery.md`](docs/providers/bexs/discovery.md).

## O que está confirmado vs. inferido

- **Confirmado ao vivo (2026-07-15, SDK real contra sandbox):** auth (`POST /v1/token`, corpo
  JSON com `audience`, `expires_in=3600`), `GET exchange-rate` (rate **string**),
  `GET payments/{id}` (404 de domínio `{"code":"7"}` para id fictício), `GET merchants` (listagem
  com dados reais) e `GET merchants/{id}`.
- **Confirmado pelo legado (payload/casing):** `POST payments` (shape completo do request e da
  resposta com QR síncrono), `POST payments/{id}/cancel` (sem corpo), `POST merchants`,
  vocabulário de status (`WAITING_CONSUMER`/`CONFIRMED`/`TRANSFERENCE`/`CANCELED`/…).
- **Inferido/aberto:** shape da resposta de sucesso do cancel (lido defensivamente — corpo vazio
  ⇒ `null`); semântica de `GET payments` sem id (respondeu `200`/`null` ao vivo — não exposto).

## Bloqueio de provisionamento de ESCRITA (externo ao código)

Toda a **leitura** funciona ao vivo. A **escrita** (payment create/cancel) está bloqueada do lado
da Bexs desde 2026-07-15: os 10 merchants da conta sandbox estão `locked` em compliance — create
com `merchant_id` responde `403 {"code":"6","message":"Merchant blocked"}` (confirmado
individualmente nos 10) e o fluxo default sem `merchant_id` responde
`422 {"code":"42","message":"Invalid event flow on state machine"}`. O teste sensor
`PaymentCreateGetCancelRoundTripsLive` (SandboxTests) reporta o estado atual e executará o
round-trip completo (create→get→cancel com cleanup) automaticamente quando a Bexs
aprovar/destravar um merchant. Refund de pagamento CONFIRMADO permanece financeiro e fora de
escopo sem autorização explícita.

## Modelo de domínio

- `Payments` — `CreateAsync` (QR síncrono), `GetAsync` (única fonte de verdade de status
  confirmada; o produto não tem webhook validado — ver discovery.md §7), `CancelAsync`
  (reversão de não pago / refund de pago, mesmo endpoint).
- `ExchangeRates` — `GetAsync(from, to)`; `Rate` é string com precisão preservada, cotação
  informativa (não vinculante).
- `Merchants` — `CreateAsync` (entra em análise de compliance; sem endpoint de remoção),
  `GetAsync`, `ListAsync`.
- Statuses/enums são `string` + classes de constantes (`BexsPaymentStatuses`,
  `BexsConsumerTypes`) — conjunto aberto, sem conversor global de enum.
- Auth: `IBexsTokenProvider` singleton com cache derivado do `expires_in` real, single-flight e
  1 retry em 401 via `DelegatingHandler` (o legado cacheava 50min fixos e não fazia retry).

## Secrets

Credenciais SÓ via `pass cambio-real-v2/bexs/demo-env` → variáveis de ambiente/secret store.
Nunca em `appsettings.json`, código ou fixtures. Nenhum teste imprime token, secret, CPF ou
payload integral. (As credenciais demo hardcoded em `cerebro/config/bexs.php` são um achado de
inventário do legado — não replicar.)

## Testes

- `tests/CambioReal.Bexs.Client.Tests` — unit/contrato (39 testes): serialização snake_case,
  token provider (cache/skew/single-flight/invalidação), recursos, mapeamento de erro nas duas
  formas observadas, retry 401, cancellation.
- `tests/CambioReal.Bexs.Client.SandboxTests` — integração sandbox real, opt-in, FORA da
  solution (nunca roda em CI por acidente). Ver o [README](tests/CambioReal.Bexs.Client.SandboxTests/README.md)
  com a última execução ao vivo.

## Origem

Contrato extraído de `cerebro` (somente leitura): `app/Libraries/EnvioBr/Bexs/{AbstractRequest,
PixRequest,ExchangeRateRequest,MerchantsRequest,PaymentNotification}.php`, `config/bexs.php`,
`config/bexs-mock.php`, `EmpresaApiController::postCreateBexsMerchant`. Discovery completo com
matriz de cobertura e evidências: [`docs/providers/bexs/discovery.md`](docs/providers/bexs/discovery.md).
