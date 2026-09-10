using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public sealed class ScriptedTextGenerationService : ITextGenerationService
{
    private readonly Func<string, string?, string> _script;
    public List<string> Inputs { get; } = [];
    public int CallCount => Inputs.Count;

    public ScriptedTextGenerationService(Func<string, string?, string> script) => _script = script;

    public Task<YandexAiResult> RespondAsync(string input, string? instructions, CancellationToken cancellationToken)
    {
        Inputs.Add(input);
        return Task.FromResult(new YandexAiResult("test-request", "test-model", _script(input, instructions), null));
    }
}
