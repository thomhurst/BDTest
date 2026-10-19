---
title: Named step text
description: Migrate positional StepText placeholders and display overrides to parameter names.
---

# Named step text

The next major version replaces `string.Format` step templates with named placeholders. Match the template name to the step method's parameter name, regardless of its position:

**Before:**

```csharp title="Illustrative fragment"
[StepText("I transfer {0} from {1}", "1:my account")]
public void Transfer(decimal amount, string account) { }
```

**After:**

```csharp title="Illustrative fragment"
[StepText("I transfer {amount} from {account}", "account=my account")]
public void Transfer(decimal amount, string account) { }
```

- Rewrite `{0}` / `{1}` to `{amount}` / `{account}` using the method's parameter names. Named formats such as `{amount:F2}` use invariant culture. Composite alignment fields are no longer interpreted; use a custom converter when you need padding.
- Rewrite `"n:text"` to `"parameterName=text"`, including entries in override arrays. `StepTextParameterOverrides` is removed; `ParameterOverrides` exposes an immutable snapshot of the named override strings.
- Keep existing escaped braces (`{{` and `}}`). Literal JSON and malformed or unmatched braces no longer throw. Override values containing braces remain exactly as written and are never interpreted as another template.
- Remove placeholders and overrides for `CancellationToken`. Tokens are always excluded, including ordinary delegates that pass a token explicitly. `WithArgs` may omit token readers or supply the full parameter list; token readers are not evaluated.
- Keep custom converters. They take precedence over format specifiers. Repeated placeholders read the argument once; different formats can render that value differently.

The source generator and analyzer diagnose invalid templates with `BDT002`, including positional migration cases and unknown parameter names. Without compiler validation, invalid placeholders remain literal and invalid overrides are ignored, with a non-fatal diagnostic once per step attempt. Invalid format strings render an error marker and a diagnostic. Update report-text snapshots accordingly.

## Argument capture and fallback

Install `BDTest.SourceGenerator` in each consumer project. Named templates do not expand the set of [supported generated argument readers](../advanced/source-generator.md). When a method is known but a captured local, factory expression or other unsupported argument cannot be read automatically, the source expression remains the fallback. Use `.WithArgs(...)` in method parameter order to restore named template rendering. Without method metadata, `WithArgs` values append to the source expression instead.

Construction, skipped/unreached steps and reporting never read arguments. Supported templates retain named placeholders, including their formats, until execution. Each executed attempt reads current values once, after hooks and skip checks; retries preserve this behavior. Template diagnostics are available for skipped and unreached steps too, and report refresh does not duplicate them.

See [step text](../writing-tests/step-text.md) for complete formatting and override behavior.
