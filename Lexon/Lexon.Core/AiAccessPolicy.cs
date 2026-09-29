namespace Lexon.Core;

/// <summary>
/// Runtime gates for when text may leave the machine. Changes take effect
/// immediately; callers should not wait for a restart.
/// </summary>
public sealed class AiAccessPolicy
{
    private sealed class State
    {
        public required bool LocalOnly { get; init; }
        public required bool SuggestionsWhileTyping { get; init; }
        public required bool RewriteOnRequest { get; init; }
        public required bool PrefetchOnSelection { get; init; }
        public required int Version { get; init; }
    }

    private State _state = new()
    {
        LocalOnly = false,
        SuggestionsWhileTyping = false,
        RewriteOnRequest = true,
        PrefetchOnSelection = false,
        Version = 0
    };

    public bool LocalOnly => Volatile.Read(ref _state).LocalOnly;
    public bool SuggestionsWhileTyping => Volatile.Read(ref _state).SuggestionsWhileTyping;
    public bool RewriteOnRequest => Volatile.Read(ref _state).RewriteOnRequest;
    public bool PrefetchOnSelection => Volatile.Read(ref _state).PrefetchOnSelection;
    public int Version => Volatile.Read(ref _state).Version;

    public event Action? Changed;

    public bool AllowsTypingSuggestions
    {
        get
        {
            var state = Volatile.Read(ref _state);
            return !state.LocalOnly && state.SuggestionsWhileTyping;
        }
    }

    public bool AllowsRewrite
    {
        get
        {
            var state = Volatile.Read(ref _state);
            return !state.LocalOnly && state.RewriteOnRequest;
        }
    }

    public bool AllowsPrefetch
    {
        get
        {
            var state = Volatile.Read(ref _state);
            return !state.LocalOnly && state.RewriteOnRequest && state.PrefetchOnSelection;
        }
    }

    /// <summary>
    /// Returns true when any flag changed. Unchanged calls do not bump Version
    /// or fire Changed, so unrelated settings persists do not cancel in-flight AI.
    /// </summary>
    public bool Update(bool localOnly, bool suggestionsWhileTyping, bool rewriteOnRequest, bool prefetchOnSelection)
    {
        var previous = Volatile.Read(ref _state);
        if (previous.LocalOnly == localOnly
            && previous.SuggestionsWhileTyping == suggestionsWhileTyping
            && previous.RewriteOnRequest == rewriteOnRequest
            && previous.PrefetchOnSelection == prefetchOnSelection)
        {
            return false;
        }

        Volatile.Write(ref _state, new State
        {
            LocalOnly = localOnly,
            SuggestionsWhileTyping = suggestionsWhileTyping,
            RewriteOnRequest = rewriteOnRequest,
            PrefetchOnSelection = prefetchOnSelection,
            Version = previous.Version + 1
        });
        Changed?.Invoke();
        return true;
    }
}
