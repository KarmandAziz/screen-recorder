using LocalScreenRecorder.Core.Models;
using LocalScreenRecorder.Core.Services;

namespace LocalScreenRecorder.Tests;

public sealed class RecordingStateMachineTests
{
    [Fact]
    public void NormalLifecycle_AllowsPauseResumeAndSafeFinalization()
    {
        var machine = new RecordingStateMachine();

        machine.TransitionTo(RecordingState.Starting);
        machine.TransitionTo(RecordingState.Recording);
        machine.TransitionTo(RecordingState.Paused);
        machine.TransitionTo(RecordingState.Recording);
        machine.TransitionTo(RecordingState.Stopping);
        machine.TransitionTo(RecordingState.Finalizing);
        machine.TransitionTo(RecordingState.Saved);

        Assert.Equal(RecordingState.Saved, machine.State);
        Assert.True(machine.CanTransitionTo(RecordingState.Starting));
    }

    [Fact]
    public void InvalidTransition_IsRejectedWithoutChangingState()
    {
        var machine = new RecordingStateMachine();

        Assert.False(machine.TryTransitionTo(RecordingState.Paused));
        Assert.Equal(RecordingState.Ready, machine.State);
        Assert.Throws<InvalidOperationException>(() => machine.TransitionTo(RecordingState.Finalizing));
    }

    [Fact]
    public void LateSuccessfulFinalization_CanRecoverFromError()
    {
        var machine = new RecordingStateMachine(RecordingState.Finalizing);

        machine.TransitionTo(RecordingState.Error);
        machine.TransitionTo(RecordingState.Saved);

        Assert.Equal(RecordingState.Saved, machine.State);
    }
}
