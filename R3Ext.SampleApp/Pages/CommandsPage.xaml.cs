using System.Collections.ObjectModel;
using System.Threading.Tasks;
using R3;
using R3Ext;

namespace R3Ext.SampleApp;

/// <summary>
/// Demonstrates RxCommand features including sync/async execution,
/// parameterization, error handling, and command composition.
/// </summary>
public sealed partial class CommandsPage : ContentPage, IDisposable
{
    private readonly Subject<bool> _canEcho = new();
    private readonly ObservableCollection<string> _log = new();
    private DisposableBag _disposables;
    private CancellationTokenSource? _loadDataCts;
    private int _counter = 0;

    // Commands
    private RxCommand<Unit, Unit> _incrementCommand = null!;
    private RxCommand<Unit, Unit> _loadDataCommand = null!;
    private RxCommand<string, string> _echoCommand = null!;
    private RxCommand<Unit, Unit> _errorCommand = null!;
    private RxCommand<Unit, Unit[]> _saveAllCommand = null!;

    public CommandsPage()
    {
        InitializeComponent();
        SetupCommands();
        LogCollectionView.ItemsSource = _log;
    }

    private void SetupCommands()
    {
        // Simple synchronous command
        _incrementCommand = RxCommand.Create(() =>
        {
            _counter++;
            CounterLabel.Text = $"Count: {_counter}";
            Log($"Counter incremented to {_counter}");
        });

        // Async command with IsExecuting tracking
        _loadDataCommand = RxCommand.CreateFromTask(async ct =>
        {
            Log("Loading data started...");
            await Task.Delay(3000, ct);
            Log("Data loaded successfully!");
            LoadStatusLabel.Text = $"Data loaded at {DateTime.Now:HH:mm:ss}";
        });

        _loadDataCommand.IsExecuting.Subscribe(isExecuting =>
        {
            LoadingIndicator.IsRunning = isExecuting;
            LoadingIndicator.IsVisible = isExecuting;
            LoadDataButton.IsEnabled = !isExecuting;
            CancelButton.IsEnabled = isExecuting;

            if (isExecuting)
            {
                LoadStatusLabel.Text = "Loading...";
            }
        }).AddTo(ref _disposables);

        // Parameterized command with CanExecute
        _echoCommand = RxCommand<string, string>.Create(
            message =>
            {
                Log($"Echoing: {message}");
                EchoLabel.Text = $"Echo: {message}";
                EchoLabel.TextColor = Colors.Green;
                EchoLabel.FontAttributes = FontAttributes.Bold;
                return message;
            },
            canExecute: _canEcho.AsObservable());

        // Error handling command
        _errorCommand = RxCommand.Create(() =>
        {
            Log("Throwing exception...");
            throw new InvalidOperationException("This is a test error!");
        });

        _errorCommand.ThrownExceptions.Subscribe(ex =>
        {
            Log($"Error caught: {ex.Message}");
            ErrorLabel.Text = $"❌ {ex.Message}";
            ErrorLabel.TextColor = Colors.Red;
        }).AddTo(ref _disposables);

        // Combined command - simulates saving multiple things
        _saveAllCommand = RxCommand<Unit, Unit[]>.CreateFromTask(async (_, ct) =>
        {
            Log("Saving all documents...");
            await Task.Delay(500, ct);
            Log("Document 1 saved");
            await Task.Delay(500, ct);
            Log("Document 2 saved");
            await Task.Delay(500, ct);
            Log("Document 3 saved");
            return new Unit[] { Unit.Default, Unit.Default, Unit.Default };
        });

        _saveAllCommand.IsExecuting.Subscribe(isExecuting =>
        {
            SaveAllButton.IsEnabled = !isExecuting;
            CompositionLabel.Text = isExecuting ? "Saving..." : "Ready";
        }).AddTo(ref _disposables);

        _saveAllCommand.AsObservable().Subscribe(_ =>
        {
            Log("All documents saved successfully!");
            CompositionLabel.Text = "✅ All saved";
            CompositionLabel.TextColor = Colors.Green;
        }).AddTo(ref _disposables);
    }

    private async void OnIncrementClicked(object? sender, EventArgs e)
    {
        await _incrementCommand.Execute().WaitAsync();
    }

    private async void OnLoadDataClicked(object? sender, EventArgs e)
    {
        // create a CTS so the running command can be cancelled
        _loadDataCts?.Dispose();
        _loadDataCts = new CancellationTokenSource();

        try
        {
            await _loadDataCommand.Execute().FirstAsync(_loadDataCts.Token);
        }
        catch (OperationCanceledException)
        {
            Log("Load cancelled");
        }
        finally
        {
            _loadDataCts?.Dispose();
            _loadDataCts = null;
        }
    }

    private void OnCancelClicked(object? sender, EventArgs e)
    {
        if (_loadDataCts != null && !_loadDataCts.IsCancellationRequested)
        {
            _loadDataCts.Cancel();
            Log("Cancel requested — signalling running command to cancel");
        }
        else
        {
            Log("No cancellable operation is running");
        }
    }

    private void OnMessageTextChanged(object? sender, TextChangedEventArgs e)
    {
        var hasText = !string.IsNullOrWhiteSpace(e.NewTextValue);
        _canEcho.OnNext(hasText);
        EchoButton.IsEnabled = hasText;
    }

    private async void OnEchoClicked(object? sender, EventArgs e)
    {
        var message = MessageEntry.Text;
        if (!string.IsNullOrWhiteSpace(message))
        {
            await _echoCommand.Execute(message).WaitAsync();
        }
    }

    private async void OnThrowErrorClicked(object? sender, EventArgs e)
    {
        await _errorCommand.Execute().WaitAsync();
    }

    private void OnClearErrorClicked(object? sender, EventArgs e)
    {
        ErrorLabel.Text = "No errors";
        ErrorLabel.TextColor = Colors.Green;
    }

    private async void OnSaveAllClicked(object? sender, EventArgs e)
    {
        await _saveAllCommand.Execute().WaitAsync();
    }

    private void OnClearLogClicked(object? sender, EventArgs e)
    {
        _log.Clear();
    }

    private void Log(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        _log.Insert(0, $"[{timestamp}] {message}");

        while (_log.Count > 50)
        {
            _log.RemoveAt(_log.Count - 1);
        }
    }

    public void Dispose()
    {
        _disposables.Dispose();
        _canEcho.Dispose();
        _incrementCommand?.Dispose();
        _loadDataCommand?.Dispose();
        _loadDataCts?.Dispose();
        _echoCommand?.Dispose();
        _errorCommand?.Dispose();
        _saveAllCommand?.Dispose();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        Dispose();
    }
}
