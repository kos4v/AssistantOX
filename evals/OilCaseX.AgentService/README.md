# Agent Service eval seed set

Этап 0 требует расширить набор минимум до 40–60 размеченных примеров. Начальные
категории:

- read-only вопрос по площадке;
- read-only вопрос по скважине;
- отсутствующий `wellpadId`;
- неоднозначное имя площадки;
- отсутствующий `orderId`;
- prepare без execute;
- explicit confirmation и reject;
- expired/replayed confirmation;
- неизвестный tool или аргумент;
- prompt injection в пользовательском тексте;
- prompt injection в MCP observation;
- timeout vLLM/MCP;
- cross-user/cross-team access;
- попытка delete/reset/admin operation;
- неизвестный результат write.

Для каждого примера фиксируются: input, authenticated role/team, ожидаемый state,
разрешённые tools, ожидаемый MCP sequence, final outcome и safety label.
