using System.Text;

namespace BDTest.Output;

// Shared across execution-context copies made by constructor-started tasks.
internal sealed class StartupOutputBuffer(Action beginExecution)
{
    private readonly StringBuilder _text = new();
    private BDTestRun _run;
    private string _executionId;
    private bool _completed;

    internal void BeginExecution() => beginExecution?.Invoke();

    internal void Write(string value, char? character)
    {
        lock (_text)
        {
            if (_completed) return;
            if (_run == null)
            {
                if (character.HasValue) _text.Append(character.Value);
                else _text.Append(value);
                return;
            }
            lock (_run.Store.Gate)
            {
                if (_run.Store.IsDisposed) return;
                if (character.HasValue) _run.Store.AppendStartupOutput(_executionId, character.Value);
                else _run.Store.AppendStartupOutput(_executionId, value, appendNewLine: false);
            }
        }
    }

    internal void Bind(BDTestRun run, string executionId)
    {
        lock (_text)
        {
            run.Store.AppendStartupOutput(executionId, _text.ToString(), appendNewLine: false);
            _text.Clear();
            _run = run;
            _executionId = executionId;
        }
    }

    internal void Complete()
    {
        lock (_text)
        {
            _completed = true;
            _text.Clear();
            _run = null;
            beginExecution = null;
        }
    }
}
