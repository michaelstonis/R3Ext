using System;
using System.Collections.Generic;
using System.ComponentModel;
using R3;
using Xunit;

namespace R3Ext.Tests;

[Collection("FrameProvider")]
public class WhenObservedTests(FrameProviderFixture fp)
{
    // Test class with an observable property
    internal sealed class DocumentWithObservable : INotifyPropertyChanged, IDisposable
    {
        private readonly Subject<bool> _isSaved = new();
        private string _title = string.Empty;

        public Observable<bool> IsSaved => _isSaved.AsObservable();

        public string Title
        {
            get => _title;
            set
            {
                if (_title == value)
                {
                    return;
                }

                _title = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Title)));
            }
        }

        public void TriggerSaved(bool saved)
        {
            _isSaved.OnNext(saved);
        }

        public void Dispose()
        {
            _isSaved.Dispose();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    internal sealed class ViewModelWithDoc : INotifyPropertyChanged, IDisposable
    {
        private DocumentWithObservable _document = new();

        public DocumentWithObservable Document
        {
            get => _document;
            set
            {
                if (ReferenceEquals(_document, value))
                {
                    return;
                }

                _document = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Document)));
            }
        }

        public void Dispose()
        {
            _document?.Dispose();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    internal sealed class PlainContainerWithObservable
    {
        public DocumentWithObservable Document { get; set; } = new();
    }

    [Fact]
    public void WhenObserved_Single_Property_Emits_Values_From_Observable()
    {
        var doc = new DocumentWithObservable();
        var values = new List<bool>();

        using var d = doc.WhenObserved(x => x.IsSaved).Subscribe(values.Add);

        doc.TriggerSaved(false);
        doc.TriggerSaved(true);
        doc.TriggerSaved(false);

        Assert.Equal(new[] { false, true, false }, values);
    }

    [Fact]
    public void WhenObserved_Switches_When_Observable_Property_Changes()
    {
        var vm = new ViewModelWithDoc();
        var doc1 = new DocumentWithObservable();
        var doc2 = new DocumentWithObservable();
        
        vm.Document = doc1;
        
        var values = new List<bool>();
        using var d = vm.WhenObserved(x => x.Document.IsSaved).Subscribe(values.Add);
        
        // Emit from first document
        doc1.TriggerSaved(true);
        
        // Switch to second document
        vm.Document = doc2;
        
        // Emit from second document
        doc2.TriggerSaved(false);
        
        // Emit from first document (should not be received)
        doc1.TriggerSaved(true);
        
        Assert.Equal(new[] { true, false }, values);
    }

    [Fact]
    public void WhenObserved_Handles_Null_Intermediate_Gracefully()
    {
        var vm = new ViewModelWithDoc();
        var doc = new DocumentWithObservable();
        
        vm.Document = doc;
        
        var values = new List<bool>();
        using var d = vm.WhenObserved(x => x.Document.IsSaved).Subscribe(values.Add);
        
        doc.TriggerSaved(true);
        
        // Set document to null - subscription should handle gracefully
        vm.Document = null!;
        
        // Should have only received the first value
        Assert.Single(values);
        Assert.True(values[0]);
    }

    [Fact]
    public void WhenObserved_Resubscribes_When_Intermediate_Changes()
    {
        var vm = new ViewModelWithDoc();
        var doc1 = new DocumentWithObservable();
        var doc2 = new DocumentWithObservable();
        var doc3 = new DocumentWithObservable();
        
        vm.Document = doc1;
        
        var values = new List<bool>();
        using var d = vm.WhenObserved(x => x.Document.IsSaved).Subscribe(values.Add);
        
        doc1.TriggerSaved(true);
        
        vm.Document = doc2;
        doc2.TriggerSaved(false);
        
        vm.Document = doc3;
        doc3.TriggerSaved(true);
        
        // Old documents should not emit
        doc1.TriggerSaved(false);
        doc2.TriggerSaved(true);
        
        Assert.Equal(new[] { true, false, true }, values);
    }

    [Fact]
    public void WhenObserved_With_Non_INPC_Root_Still_Works()
    {
        var container = new PlainContainerWithObservable();
        var values = new List<bool>();
        
        using var d = container.WhenObserved(x => x.Document.IsSaved).Subscribe(values.Add);
        
        // Since root doesn't implement INPC, it will use EveryValueChanged
        // This tests the fallback mechanism
        fp.Advance();
        
        container.Document.TriggerSaved(true);
        container.Document.TriggerSaved(false);
        
        Assert.Equal(new[] { true, false }, values);
    }

    [Fact]
    public void WhenObserved_Disposes_Inner_Subscription_On_Outer_Dispose()
    {
        var doc = new DocumentWithObservable();
        var values = new List<bool>();
        
        var subscription = doc.WhenObserved(x => x.IsSaved).Subscribe(values.Add);
        
        doc.TriggerSaved(true);
        
        subscription.Dispose();
        
        // After disposal, should not receive values
        doc.TriggerSaved(false);
        
        Assert.Single(values);
        Assert.True(values[0]);
    }

    [Fact]
    public void WhenObserved_Handles_Property_That_Returns_Different_Observable_Instances()
    {
        var doc = new ObservableReturningDoc();
        var values = new List<string>();
        
        using var d = doc.WhenObserved(x => x.CurrentStream).Subscribe(values.Add);
        
        // First stream
        doc.EmitToCurrentStream("A");
        
        // Switch to new stream
        doc.SwitchToNewStream();
        doc.EmitToCurrentStream("B");
        
        Assert.Equal(new[] { "A", "B" }, values);
    }

    internal sealed class ObservableReturningDoc : INotifyPropertyChanged, IDisposable
    {
        private Subject<string> _currentStream = new();
        private Observable<string> _currentStreamObservable;

        public ObservableReturningDoc()
        {
            _currentStreamObservable = _currentStream.AsObservable();
        }

        public Observable<string> CurrentStream
        {
            get => _currentStreamObservable;
            private set
            {
                if (ReferenceEquals(_currentStreamObservable, value))
                {
                    return;
                }

                _currentStreamObservable = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentStream)));
            }
        }

        public void EmitToCurrentStream(string value)
        {
            _currentStream.OnNext(value);
        }

        public void SwitchToNewStream()
        {
            _currentStream = new Subject<string>();
            CurrentStream = _currentStream.AsObservable();
        }

        public void Dispose()
        {
            _currentStream?.Dispose();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    [Fact]
    public void WhenObserved_Does_Not_Emit_When_Observable_Is_Null()
    {
        var doc = new NullableObservableDoc();
        var values = new List<int>();
        
        // Start with null observable
        doc.SetObservable(null);
        
        using var d = doc.WhenObserved(x => x.ValueStream!).Subscribe(values.Add);
        
        // Should not crash, should not emit
        fp.Advance();
        
        // Now set a real observable
        var subject = new Subject<int>();
        doc.SetObservable(subject.AsObservable());
        
        subject.OnNext(42);
        
        Assert.Single(values);
        Assert.Equal(42, values[0]);
    }

    internal sealed class NullableObservableDoc : INotifyPropertyChanged
    {
        private Observable<int>? _valueStream;

        public Observable<int>? ValueStream
        {
            get => _valueStream;
            private set
            {
                if (ReferenceEquals(_valueStream, value))
                {
                    return;
                }

                _valueStream = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ValueStream)));
            }
        }

        public void SetObservable(Observable<int>? obs)
        {
            ValueStream = obs;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    [Fact]
    public void WhenObserved_With_Deep_Property_Chain()
    {
        var root = new RootWithMiddle();
        var middle = new MiddleWithDoc();
        var doc = new DocumentWithObservable();
        
        root.Middle = middle;
        middle.Document = doc;
        
        var values = new List<bool>();
        using var d = root.WhenObserved(x => x.Middle.Document.IsSaved).Subscribe(values.Add);
        
        doc.TriggerSaved(true);
        
        // Replace middle layer
        var newMiddle = new MiddleWithDoc();
        var newDoc = new DocumentWithObservable();
        newMiddle.Document = newDoc;
        
        root.Middle = newMiddle;
        newDoc.TriggerSaved(false);
        
        // Old doc should not emit
        doc.TriggerSaved(true);
        
        Assert.Equal(new[] { true, false }, values);
    }

    internal sealed class RootWithMiddle : INotifyPropertyChanged, IDisposable
    {
        private MiddleWithDoc _middle = new();

        public MiddleWithDoc Middle
        {
            get => _middle;
            set
            {
                if (ReferenceEquals(_middle, value))
                {
                    return;
                }

                _middle = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Middle)));
            }
        }

        public void Dispose()
        {
            _middle?.Dispose();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    internal sealed class MiddleWithDoc : INotifyPropertyChanged, IDisposable
    {
        private DocumentWithObservable _document = new();

        public DocumentWithObservable Document
        {
            get => _document;
            set
            {
                if (ReferenceEquals(_document, value))
                {
                    return;
                }

                _document = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Document)));
            }
        }

        public void Dispose()
        {
            _document?.Dispose();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
