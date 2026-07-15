# Tests

Cross-cutting test suites:

```
integration/   Cross-service integration tests (Testcontainers, real Service Bus emulator)
e2e/           End-to-end tests (Playwright against deployed environments)
load/          Load tests validating performance targets (validation <50ms, API P99 <150ms)
```

Unit tests live alongside each app/package.
