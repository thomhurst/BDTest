---
title: Retry hook errors
description: Inspect failures from retry setup, teardown and OnBeforeRetry hooks.
---

# Retry hook errors

If a `[BDTestRetrySetUp]`, `[BDTestRetryTearDown]`, or `OnBeforeRetry()` hook throws,
BDTest stops retrying and throws
`BDTest.Exceptions.ErrorOccurredDuringRetryActionException`.

This exception derives from `BDTestException`. Its constructor still accepts the
original hook exception as its only argument. The message describes the retry
action failure and includes the hook's message. `InnerException` holds that same
hook exception, and `ToString()` includes its message and stack trace.

Use `InnerException` to inspect the hook failure. Use `GetBaseException()` to reach
the innermost cause when the hook exception has its own inner exception. Scenario
outline reports retain the hook exception's message and stack trace, including its
nested causes.
