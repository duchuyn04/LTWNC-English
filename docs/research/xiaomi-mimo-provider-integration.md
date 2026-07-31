# Xiaomi MiMo provider integration

Research date: 2026-07-31. Scope: first-party Xiaomi MiMo documentation only.

## Conclusion

For a custom ASP.NET application backend, use the **pay-as-you-go MiMo API** with the account's `sk-...` key and region-matched Base URL. Do **not** use a Token Plan `tp-...` key: Token Plan is technically exposed through OpenAI- and Anthropic-compatible APIs, but Xiaomi's current Token Plan policy says its quota may be used only in programming tools and explicitly prohibits API-call usage by automated scripts and custom application backends. Violations may cause suspension or key blocking.

Source: [Token Plan subscription instructions, "Package Usage"](https://mimo.mi.com/docs/en-US/tokenplan/Token%20Plan/subscription)

## Base URLs and credential pairing

### Pay-as-you-go

| Protocol | Published Base URL example | Key |
|---|---|---|
| OpenAI-compatible | `https://api.xiaomimimo.com/v1` | `sk-...` |
| Anthropic-compatible | `https://api.xiaomimimo.com/anthropic` | `sk-...` |

Xiaomi labels these URLs as examples. Its account FAQ says domestic and overseas accounts receive different region-specific Base URLs and keys, that the URL/key pairs are not interoperable, and that usage is accounted separately. The public English documentation does **not** enumerate separate pay-as-you-go China, Singapore, or Europe hostnames. Therefore, use the pay-as-you-go Base URL shown in the authenticated console for that account and do not infer a regional hostname from the Token Plan naming scheme.

Sources: [First API Call](https://mimo.mi.com/docs/en-US/quick-start/summary/first-api-call), [Account and Authentication FAQ](https://mimo.mi.com/docs/en-US/quick-start/faq/account)

### Token Plan

These are documented, but are **not permitted for the custom backend use case**:

| Region/cluster | OpenAI-compatible Base URL | Anthropic-compatible Base URL |
|---|---|---|
| China | `https://token-plan-cn.xiaomimimo.com/v1` | `https://token-plan-cn.xiaomimimo.com/anthropic` |
| Singapore | `https://token-plan-sgp.xiaomimimo.com/v1` | `https://token-plan-sgp.xiaomimimo.com/anthropic` |
| Europe (Amsterdam) | `https://token-plan-ams.xiaomimimo.com/v1` | `https://token-plan-ams.xiaomimimo.com/anthropic` |

Token Plan keys use `tp-...`. The plan-management console's displayed Base URL is authoritative; the cluster URL and key must be used together. A Token Plan key and pay-as-you-go Base URL/key cannot be mixed. Xiaomi documents mixed plan/pay-as-you-go credentials as a cause of HTTP 401.

Sources: [Token Plan Quick Access](https://mimo.mi.com/docs/en-US/tokenplan/Token%20Plan/quick-access), [Token Plan subscription instructions](https://mimo.mi.com/docs/en-US/tokenplan/Token%20Plan/subscription), [Error Codes](https://mimo.mi.com/docs/en-US/api/guidance/error-codes)

## OpenAI Chat Completions contract

- Method and path: `POST /chat/completions` relative to an OpenAI-compatible Base URL. Pay-as-you-go full URL: `https://api.xiaomimimo.com/v1/chat/completions`.
- Authentication: choose either `api-key: $MIMO_API_KEY` or `Authorization: Bearer $MIMO_API_KEY`.
- Required content header: `Content-Type: application/json`.
- Required body fields: `model` and `messages`.
- Documented text-generation model IDs: `mimo-v2.5-pro` and `mimo-v2.5`. Use the exact lowercase ID `mimo-v2.5-pro`.
- Documented top-level request fields are `messages`, `model`, `frequency_penalty`, `max_completion_tokens`, `presence_penalty`, `response_format`, `stop`, `stream`, `thinking`, `temperature`, `tool_choice`, `tools`, and `top_p`.
- Message roles include `developer`, `system`, `user`, `assistant`, and `tool`. Preserve returned `reasoning_content` in subsequent assistant messages during multi-turn tool calling in thinking mode.
- No Xiaomi-specific region header, API-version header, tenant header, or query parameter is documented for Chat Completions.

### Token limit field

For **OpenAI Chat Completions**, the documented field is `max_completion_tokens`, not `max_tokens`. It includes visible output and reasoning tokens, accepts `1..131072`, defaults to `131072` for `mimo-v2.5-pro`, and defaults to `32768` for `mimo-v2.5`. Xiaomi's examples consistently use `max_completion_tokens`.

`max_tokens` belongs to Xiaomi's **Anthropic Messages compatibility** request. Xiaomi's OpenAI Responses compatibility instead uses `max_output_tokens`. An ASP.NET Chat Completions DTO should therefore serialize `max_completion_tokens` exactly and should not substitute `max_tokens`.

### Other relevant behavior

- `thinking.type` is `enabled` or `disabled`; it defaults to `enabled` for both text models. In thinking mode, supplied `temperature` and `top_p` are overridden to `1.0` and `0.95`.
- `temperature` otherwise accepts `0..1.5`; `top_p` accepts `0.01..1.0`. Xiaomi recommends changing one, not both.
- `frequency_penalty` and `presence_penalty` default to `0` and accept `-2.0..2.0`.
- `stop` may be a string, an array of up to four sequences, or `null`.
- `tool_choice` currently documents only `auto`; other values are removed by the backend and behave as `auto`.

Sources: [OpenAI Chat Completions API reference](https://mimo.mi.com/docs/en-US/api/chat/openai-api), [First API Call](https://mimo.mi.com/docs/en-US/quick-start/summary/first-api-call), [Model Hyperparameters](https://mimo.mi.com/docs/en-US/api/guidance/model-hyperparameters), [API Integration FAQ](https://mimo.mi.com/docs/en-US/quick-start/faq/api-integration)

## Streaming and structured output

- `stream` defaults to `false`. With `true`, the server returns incremental server-sent events; generated text is in `choices[].delta.content`, with reasoning and tool-call deltas in their corresponding delta fields.
- `mimo-v2.5-pro` supports streaming and structured output.
- JSON mode is enabled with `response_format: {"type":"json_object"}`. The prompt must explicitly request JSON and define the expected fields/types.
- JSON mode guarantees syntactically valid JSON only, not conformance to a supplied JSON Schema. Validate the result in the backend.
- In streaming JSON mode, concatenate the entire `delta.content` sequence before parsing. A low `max_completion_tokens` can truncate the response into incomplete JSON.

Sources: [Structured Outputs](https://mimo.mi.com/docs/en-US/quick-start/usage-guide/text-generation/structured-output), [Models](https://mimo.mi.com/docs/en-US/quick-start/summary/model), [OpenAI Chat Completions API reference](https://mimo.mi.com/docs/en-US/api/chat/openai-api)

## Documented HTTP 404 meaning

Xiaomi's error table defines `404 - Not Found` as: **"The requested endpoint or model does not support image input capability"** and recommends verifying that the selected endpoint/model supports image input. The same table assigns a nonexistent model or malformed/incorrect fields to HTTP 400, mixed or invalid credentials to HTTP 401, unavailable region/risk control to HTTP 403, and exhausted Token Plan quota or excessive request rate to HTTP 429.

The official error table does not document any additional MiMo-specific 404 cause. In particular, it does not say that a wrong plan/key pairing produces 404. A generic proxy may still return its own 404 for an invalid URL, but that is not a Xiaomi-documented API error meaning and should not be presented as one.

Source: [Error Codes](https://mimo.mi.com/docs/en-US/api/guidance/error-codes)

## Reconciling the apparent Token Plan contradiction

The generic First API Call page says Token Plan users can call the same examples after replacing the Base URL and key. Token Plan Quick Access also includes a direct `curl` call as an optional credential-verification method. These establish **technical protocol compatibility**, not permission to operate a custom backend.

The more specific Token Plan subscription page governs package use: it limits quota to programming tools and expressly prohibits custom application backends and automated scripts in clearly non-coding scenarios. Thus there is no safe policy basis for using Token Plan from this ASP.NET backend. The direct-call example is consistent with verification and coding-tool integration; it does not override the explicit usage restriction. Use pay-as-you-go for production backend calls.

Sources: [First API Call](https://mimo.mi.com/docs/en-US/quick-start/summary/first-api-call), [Token Plan Quick Access](https://mimo.mi.com/docs/en-US/tokenplan/Token%20Plan/quick-access), [Token Plan subscription instructions](https://mimo.mi.com/docs/en-US/tokenplan/Token%20Plan/subscription)
