import {test} from 'node:test';
import assert from 'node:assert/strict';
import {extractSamples} from './check-samples.mjs';

test('extracts the displayed C# verbatim with its Markdown line', () => {
  const markdown = '# Example\r\n\r\n```csharp sample="Readme/Test.cs" title="Compiled example"\r\nclass Test { }\r\n```\r\n';
  assert.deepEqual(extractSamples(markdown, 'README.md'), [
    {sample: 'Readme/Test.cs', code: 'class Test { }\r\n', source: 'README.md', line: 4},
  ]);
});

test('requires explicit classification instead of silently dropping new examples', () => {
  assert.throws(() => extractSamples('```csharp\nMissingApi();\n```', 'new.md'), /classify C#/);
  assert.deepEqual(extractSamples('```csharp title="Illustrative fragment"\n...\n```', 'fragment.md'), []);
});

test('rejects paths outside a sample project', () => {
  assert.throws(() => extractSamples('```csharp sample="../Test.cs"\nclass Test {}\n```', 'bad.md'), /invalid sample path/);
});

test('rejects an unterminated C# fence instead of silently omitting it', () => {
  assert.throws(() => extractSamples('```csharp sample="Readme/Test.cs"\nclass Test {}', 'bad.md'), /unterminated/);
});

for (const opening of ['~~~csharp', '  ```csharp', '````csharp', '``` csharp']) {
  test(`rejects unsupported C# fence syntax: ${opening}`, () => {
    const closing = opening.trimStart().startsWith('~') ? '~~~' : '````';
    const markdown = `# Example\n\n${opening} sample="Readme/Test.cs"\nclass Test {}\n${closing}\n`;
    assert.throws(() => extractSamples(markdown, 'alternate.md'), /alternate\.md:3: unsupported C# fence/);
  });
}
