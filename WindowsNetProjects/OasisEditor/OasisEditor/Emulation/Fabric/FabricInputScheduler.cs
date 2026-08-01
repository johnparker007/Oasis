namespace OasisEditor;

/// <summary>Schedules digital input changes around Fabric advances.</summary>
internal sealed class FabricInputScheduler
{
    private readonly Dictionary<int, InputState> _states = [];

    public void Request(int index, bool active)
    {
        if (!_states.TryGetValue(index, out var state))
            state = new InputState();
        state.Desired = active;
        if (active && !state.Applied)
            state.AssertionPending = true;
        _states[index] = state;
    }

    public void ApplyBeforeAdvance(IFabricMachineSession session, long pumpSequence, Action<string>? logger)
    {
        foreach (var pair in _states.ToArray())
        {
            var index = pair.Key;
            var state = pair.Value;
            if (state.AssertionPending && !state.Applied)
            {
                Submit(session, index, true, pumpSequence, false, logger);
                state.Applied = true;
                state.AssertionPending = false;
                state.AdvancedSinceAssertion = false;
            }
            else if (state.Applied && !state.Desired && state.AdvancedSinceAssertion)
            {
                Submit(session, index, false, pumpSequence, true, logger);
                state.Applied = false;
            }

            if (!state.Applied && !state.Desired && !state.AssertionPending)
                _states.Remove(index);
            else
                _states[index] = state;
        }
    }

    public void MarkAdvanced()
    {
        foreach (var index in _states.Keys.ToArray())
        {
            var state = _states[index];
            if (state.Applied)
                state.AdvancedSinceAssertion = true;
            _states[index] = state;
        }
    }

    public void ReleaseAll(IFabricMachineSession? session)
    {
        if (session is not null)
            foreach (var pair in _states.Where(pair => pair.Value.Applied))
                session.SubmitInput(CreateInput(pair.Key, false));
        _states.Clear();
    }

    public void Clear() => _states.Clear();

    private static void Submit(IFabricMachineSession session, int index, bool active, long pumpSequence,
        bool advancedSinceAssertion, Action<string>? logger)
    {
        session.SubmitInput(CreateInput(index, active));
        logger?.Invoke($"[Fabric Input] switch={index} state={(active ? "pressed" : "released")} pump={pumpSequence} advanceSincePress={advancedSinceAssertion.ToString().ToLowerInvariant()}");
    }

    private static FabricInput CreateInput(int index, bool active) => new($"oasis.switch.{index}", index, active);

    private struct InputState
    {
        public bool Desired;
        public bool Applied;
        public bool AssertionPending;
        public bool AdvancedSinceAssertion;
    }
}
