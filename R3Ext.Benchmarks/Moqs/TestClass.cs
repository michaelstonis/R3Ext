using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using ReactiveUI;

namespace R3Ext.Benchmarks.Moqs
{
    // Minimal test class implementing INotifyPropertyChanged and IViewFor for ReactiveUI bindings
    public class TestClass : INotifyPropertyChanged, IViewFor<TestClass>
    {
        private TestClass _child = null!;
        private int _value;

        public readonly int Height;

        public TestClass(int height = 1)
        {
            if (height < 1)
            {
                height = 1;
            }

            Height = height;
            if (height > 1)
            {
                Child = new TestClass(height - 1);
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public TestClass Child
        {
            get => _child;
            set => RaiseAndSetIfChanged(ref _child, value);
        }

        public int Value
        {
            get => _value;
            set => RaiseAndSetIfChanged(ref _value, value);
        }

        // Mutate at a given level from the root: if bottom, increment value; otherwise replace child chain
        public void Mutate(int depth = 0)
        {
            if (depth >= Height)
            {
                throw new ArgumentOutOfRangeException(nameof(depth));
            }

            int h = Height;
            TestClass? current = this;
            while (--h > depth)
            {
                current = current?.Child;
            }

            if (h < 1 && current is not null)
            {
                current.Value++;
                return;
            }

            if (current is not null)
            {
                current.Child = new TestClass(h);
            }
        }


        // Helper to get Value at specified depth (1-based for value at bottom)
        public int ValueAtDepth(int depth)
        {
            var node = this;
            int h = depth;
            while (--h > 0 && node.Child is not null)
            {
                node = node.Child;
            }
            return node.Value;
        }

        // ReactiveUI IViewFor implementation
        object? IViewFor.ViewModel
        {
            get => ViewModel;
            set => ViewModel = (TestClass?)value;
        }

        public TestClass? ViewModel { get; set; }

        protected void RaiseAndSetIfChanged<T>(ref T fieldValue, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(fieldValue, value))
            {
                return;
            }
            fieldValue = value;
            OnPropertyChanged(propertyName);
        }

        protected virtual void OnPropertyChanged(string? propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}