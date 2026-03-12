namespace AutoDiagnosis.App.Services;

public sealed class ScanControl
{
    private readonly object _lock = new();
    private TaskCompletionSource<bool> _resumeSignal = CreateCompletedSignal();

    public bool IsPaused { get; private set; }

    public void Pause()
    {
        lock (_lock)
        {
            if (IsPaused)
            {
                return;
            }

            IsPaused = true;
            _resumeSignal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    public void Resume()
    {
        lock (_lock)
        {
            if (!IsPaused)
            {
                return;
            }

            IsPaused = false;
            _resumeSignal.TrySetResult(true);
            _resumeSignal = CreateCompletedSignal();
        }
    }

    public async Task WaitIfPausedAsync(CancellationToken cancellationToken)
    {
        Task waitTask;
        lock (_lock)
        {
            waitTask = _resumeSignal.Task;
        }

        await waitTask.WaitAsync(cancellationToken);
    }

    public void Reset()
    {
        Resume();
    }

    private static TaskCompletionSource<bool> CreateCompletedSignal()
    {
        var signal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        signal.SetResult(true);
        return signal;
    }
}
