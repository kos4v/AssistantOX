# MCP error policy для Agent Service

| MCP code | Поведение агента |
|---|---|
| `invalid_input` | исправить аргументы или задать уточнение |
| `authentication_required` | запросить повторную авторизацию, без retry write |
| `forbidden` | сообщить об отсутствии доступа без раскрытия ресурса |
| `resource_not_found` | запросить другой ID/имя |
| `domain_conflict` | показать конфликт и предложить изменить параметры |
| `validation_failed` | показать validation issues; execute не вызывать |
| `confirmation_invalid` | повторить prepare |
| `confirmation_replayed` | не повторять write вслепую |
| `idempotency_conflict` | остановить операцию и сообщить о конфликте |
| `upstream_unavailable` | контролируемая ошибка; write не повторять |
| `unknown_result` | явно показать неизвестный результат; не утверждать success |

Внутренние stack traces, SQL, JWT и product DB details никогда не передаются
пользователю или модели.
