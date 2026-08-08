# ADR-0002: Gemma 4 вызывается через OpenAI-compatible vLLM

## Статус

Принято на этапе 0.

## Решение

Agent Service использует внутренний OpenAI-compatible endpoint vLLM как единственную
границу вызова модели. В production фиксируются model revision, tokenizer/template,
generation parameters и timeout policy.

## Причины

- закрытый GPU-контур без доступа к продуктовой БД;
- замена модели без изменения orchestration contracts;
- возможность использовать fake OpenAI-compatible server в тестах;
- измеряемые latency, concurrency и GPU limits.

Модель не получает JWT, system secrets, raw logs или скрытое policy state.
