---
title: Story text
description: Format story roles, goals, and benefits with StoryAttribute.
---

# Story text

Apply `[Story]` to a test class to describe its role, goal, and benefit:

```csharp title="Illustrative fragment"
using BDTest.Attributes;
using BDTest.Test;

[Story(AsA = "engineer", IWant = "to run scenarios", SoThat = "reports explain their purpose")]
public class ScenarioTests : TUnitBDTestBase<object>
{
}
```

`StoryAttribute.GetStoryText()` returns three lines and a trailing newline:

```text
As an engineer
I want to run scenarios
So that reports explain their purpose
```

Formatting never changes `AsA`, `AsAn`, `IWant`, or `SoThat`. Repeated calls on the
same attribute return the same text while its property values remain unchanged.

## Role articles

Supply exactly one of `AsA` or `AsAn`:

- A complete `As a ...` or `As an ...` phrase is preserved, including its casing.
- An explicit article is preserved: `AsA = "a user"` becomes `As a user`, and
  `AsA = "an engineer"` becomes `As an engineer`.
- Otherwise, `AsA` uses `an` before a vowel letter (`a`, `e`, `i`, `o`, `u`), ignoring
  case, and `a` before other letters. Roles starting with `user` use `a`, so
  `AsA = "user"` becomes `As a user`.
- `AsAn` explicitly selects `an` for a role without an article:
  `AsAn = "owl"` becomes `As an owl`.

Article selection is a spelling rule, not a pronunciation dictionary. Supply an
explicit article or a complete phrase for other pronunciation exceptions, such as
`AsA = "a university student"` or `AsAn = "hourly worker"`.

## Prefixes and validation

`I want` and `So that` are added only when absent. All prefix checks ignore case
and require a whole word: the prefix must end the value or be followed by a
character other than a letter, digit, or underscore. For example, `I wanted ...`
becomes `I want I wanted ...`; `As another user` becomes `As an As another user`.

Null or empty roles, goals, or benefits still throw
`MissingStoryTextArgumentException`. Supplying both `AsA` and `AsAn` still throws
`DuplicateStoryTextArgumentException`.
