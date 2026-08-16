using LocalScreenRecorder.Core.Models;

namespace LocalScreenRecorder.Core.Services;

public sealed class RecordingStateMachine
{
    private static readonly IReadOnlyDictionary<RecordingState, RecordingState[]> AllowedTransitions =
        new Dictionary<RecordingState, RecordingState[]>
        {
            [RecordingState.Ready] = [RecordingState.Countdown, RecordingState.Starting, RecordingState.Error],
            [RecordingState.Countdown] = [RecordingState.Ready, RecordingState.Starting, RecordingState.Error],
            [RecordingState.Starting] = [RecordingState.Recording, RecordingState.Stopping, RecordingState.Ready, RecordingState.Error],
            [RecordingState.Recording] = [RecordingState.Paused, RecordingState.Stopping, RecordingState.Finalizing, RecordingState.Error],
            [RecordingState.Paused] = [RecordingState.Recording, RecordingState.Stopping, RecordingState.Finalizing, RecordingState.Error],
            [RecordingState.Stopping] = [RecordingState.Finalizing, RecordingState.Saved, RecordingState.Error],
            [RecordingState.Finalizing] = [RecordingState.Saved, RecordingState.Error],
            [RecordingState.Saved] = [RecordingState.Countdown, RecordingState.Starting, RecordingState.Error],
            [RecordingState.Error] = [RecordingState.Ready, RecordingState.Countdown, RecordingState.Starting, RecordingState.Saved]
        };

    private readonly object _sync = new();
    private RecordingState _state;

    public RecordingStateMachine(RecordingState initialState = RecordingState.Ready) => _state = initialState;

    public RecordingState State
    {
        get { lock (_sync) return _state; }
    }

    public bool CanTransitionTo(RecordingState next)
    {
        lock (_sync)
        {
            return next == _state || AllowedTransitions[_state].Contains(next);
        }
    }

    public bool TryTransitionTo(RecordingState next)
    {
        lock (_sync)
        {
            if (next == _state) return false;
            if (!AllowedTransitions[_state].Contains(next)) return false;
            _state = next;
            return true;
        }
    }

    public void TransitionTo(RecordingState next)
    {
        lock (_sync)
        {
            if (next == _state) return;
            if (!AllowedTransitions[_state].Contains(next))
            {
                throw new InvalidOperationException($"Recording cannot transition from {_state} to {next}.");
            }
            _state = next;
        }
    }
}
