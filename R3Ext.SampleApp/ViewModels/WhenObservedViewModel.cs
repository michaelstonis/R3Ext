#pragma warning disable SA1413
#pragma warning disable SA1516
#pragma warning disable SA1629
#pragma warning disable CS8602
#pragma warning disable CS8618
#pragma warning disable CS9264

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using R3;

namespace R3Ext.SampleApp.ViewModels;

/// <summary>
/// Demonstrates WhenObserved operator - similar to ReactiveUI's WhenAnyObservable.
/// WhenObserved observes properties that return IObservable and automatically switches between them.
///
/// NOTE: Currently using WhenChanged + Select + Switch as a workaround due to a source generator bug.
/// The WhenObserved source generator incorrectly registers Observable&lt;T&gt; instead of T as the return type.
/// </summary>
public sealed class WhenObservedViewModel : ObservableObject, IDisposable
{
    private Document _currentDocument = new();
    private DataStream _currentStream = new();
    private string _observedDocumentStatus = "Not Subscribed";
    private string _observedStreamValue = "Not Subscribed";
    private string _observedNestedValue = "Not Subscribed";
    private ObservableCollection<string> _eventLog = new();
    private DisposableBag _disposables;
    private Container _container = new();

    public WhenObservedViewModel()
    {
        // Example 1: Observe a simple observable property
        // Manual implementation of WhenObserved due to source generator bug
        this.WhenChanged(x => x.CurrentDocument)
            .Select(doc => doc.IsSaved)
            .Switch()
            .Subscribe(isSaved => ObservedDocumentStatus = $"Document is {(isSaved ? "saved" : "NOT saved")}")
            .AddTo(ref _disposables);

        // Example 2: Observe a data stream observable
        this.WhenChanged(x => x.CurrentStream)
            .Select(stream => stream.DataObservable)
            .Switch()
            .Subscribe(value => ObservedStreamValue = $"Stream value: {value}")
            .AddTo(ref _disposables);

        // Example 3: Observe nested property with automatic switching
        this.WhenChanged(x => x.NestedContainer.Document)
            .Select(doc => doc.IsSaved)
            .Switch()
            .Subscribe(isSaved => ObservedNestedValue = $"Nested document {(isSaved ? "saved" : "NOT saved")}")
            .AddTo(ref _disposables);

        // Log all document status changes
        this.WhenChanged(x => x.CurrentDocument)
            .Select(doc => doc.IsSaved)
            .Switch()
            .Subscribe(isSaved => LogEvent($"Document saved state changed: {isSaved}"))
            .AddTo(ref _disposables);
    }

    public Document CurrentDocument
    {
        get => _currentDocument;
        set
        {
            if (SetProperty(ref _currentDocument, value))
            {
                LogEvent($"Switched to new document: {value.Name}");
            }
        }
    }

    public DataStream CurrentStream
    {
        get => _currentStream;
        set
        {
            if (SetProperty(ref _currentStream, value))
            {
                LogEvent($"Switched to new stream: {value.Name}");
            }
        }
    }

    public Container NestedContainer
    {
        get => _container;
        set => SetProperty(ref _container, value);
    }

    public string ObservedDocumentStatus
    {
        get => _observedDocumentStatus;
        private set => SetProperty(ref _observedDocumentStatus, value);
    }

    public string ObservedStreamValue
    {
        get => _observedStreamValue;
        private set => SetProperty(ref _observedStreamValue, value);
    }

    public string ObservedNestedValue
    {
        get => _observedNestedValue;
        private set => SetProperty(ref _observedNestedValue, value);
    }

    public ObservableCollection<string> EventLog
    {
        get => _eventLog;
        private set => SetProperty(ref _eventLog, value);
    }

    public void TriggerDocumentSave()
    {
        CurrentDocument.TriggerSave(true);
        LogEvent("Triggered document save");
    }

    public void TriggerDocumentEdit()
    {
        CurrentDocument.TriggerSave(false);
        LogEvent("Triggered document edit (unsaved)");
    }

    public void SwitchToNewDocument()
    {
        var newDoc = new Document
        {
            Name = $"Document {DateTime.Now:HH:mm:ss}"
        };
        CurrentDocument = newDoc;

        // Immediately trigger a save on the new document
        Task.Delay(100).ContinueWith(_ => newDoc.TriggerSave(true));
    }

    public void EmitStreamValue()
    {
        var random = new Random();
        CurrentStream.Emit(random.Next(1, 100));
        LogEvent($"Emitted random value to stream");
    }

    public void SwitchToNewStream()
    {
        CurrentStream = new DataStream
        {
            Name = $"Stream {DateTime.Now:HH:mm:ss}"
        };
    }

    public void SwitchNestedDocument()
    {
        NestedContainer.Document = new Document
        {
            Name = $"Nested Doc {DateTime.Now:HH:mm:ss}"
        };

        // Trigger save after switch
        Task.Delay(100).ContinueWith(_ => NestedContainer.Document.TriggerSave(true));
    }

    public void ClearLog()
    {
        EventLog.Clear();
    }

    private void LogEvent(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        EventLog.Insert(0, $"[{timestamp}] {message}");

        // Keep log at reasonable size
        while (EventLog.Count > 20)
        {
            EventLog.RemoveAt(EventLog.Count - 1);
        }
    }

    public void Dispose()
    {
        _disposables.Dispose();
        _currentDocument?.Dispose();
        _currentStream?.Dispose();
        _container?.Dispose();
    }

    /// <summary>
    /// Document with an observable for save state changes.
    /// </summary>
    public sealed class Document : ObservableObject, IDisposable
    {
        private readonly ReactiveProperty<bool> _isSaved = new(false);
        private string _name = "Untitled";

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public Observable<bool> IsSaved => _isSaved;

        public void TriggerSave(bool saved)
        {
            _isSaved.Value = saved;
        }

        public void Dispose()
        {
            _isSaved?.Dispose();
        }
    }

    /// <summary>
    /// Data stream that emits integer values.
    /// </summary>
    public sealed class DataStream : IDisposable
    {
        private readonly ReactiveProperty<int> _data = new(0);
        private string _name = "Default Stream";

        public string Name
        {
            get => _name;
            set => _name = value;
        }

        public Observable<int> DataObservable => _data;

        public void Emit(int value)
        {
            _data.Value = value;
        }

        public void Dispose()
        {
            _data?.Dispose();
        }
    }

    /// <summary>
    /// Container for nested document example.
    /// </summary>
    public sealed class Container : ObservableObject, IDisposable
    {
        private Document _document = new();

        public Document Document
        {
            get => _document;
            set => SetProperty(ref _document, value);
        }

        public void Dispose()
        {
            _document?.Dispose();
        }
    }
}
