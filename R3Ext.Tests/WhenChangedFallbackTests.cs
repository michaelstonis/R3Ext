using System;
using System.Collections.Generic;
using System.ComponentModel;
using R3;
using Xunit;

namespace R3Ext.Tests;

[Collection("FrameProvider")]
public class WhenChangedFallbackTests(FrameProviderFixture fp)
{
    internal sealed class PlainHost
    {
        public string Name { get; set; } = string.Empty;

        public PlainPerson Person { get; set; } = new PlainPerson();
    }

    internal sealed class PlainPerson
    {
        public string Given { get; set; } = string.Empty;
    }

    internal sealed class NotifyPerson : INotifyPropertyChanged
    {
        private string _given = string.Empty;

        public string Given
        {
            get => _given;
            set
            {
                if (_given == value)
                {
                    return;
                }

                _given = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Given)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    internal sealed class NotifyHost : INotifyPropertyChanged
    {
        private NotifyPerson _person = new();

        public NotifyPerson Person
        {
            get => _person;
            set
            {
                if (ReferenceEquals(_person, value))
                {
                    return;
                }

                _person = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Person)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    [Fact]
    public void Fallback_On_Single_NonNotify_Property_Emits_On_Value_Changes()
    {
        var host = new PlainHost { Name = "A" };
        var values = new List<string>();
        using var d = host.WhenChanged(h => h.Name).Subscribe(values.Add);
        fp.Advance();
        host.Name = "B";
        fp.Advance();
        host.Name = "C";
        fp.Advance();
        Assert.Equal(new[] { "A", "B", "C" }, values);
    }

    [Fact]
    public void Fallback_On_MultiSegment_Chain_With_NonNotify_Intermediate_Emits_On_Leaf_And_Replacement()
    {
        var host = new PlainHost { Person = new PlainPerson { Given = "X" } };
        var values = new List<string>();
        using var d = host.WhenChanged(h => h.Person.Given).Subscribe(values.Add);
        fp.Advance();
        host.Person.Given = "Y"; // mutate leaf
        fp.Advance();
        host.Person = new PlainPerson { Given = "Z" }; // replace intermediate object
        fp.Advance();
        Assert.Equal(new[] { "X", "Y", "Z" }, values);
    }

    [Fact]
    public void INPC_Chain_Emits_On_Leaf_Property_Changes()
    {
        var host = new NotifyHost { Person = new NotifyPerson { Given = "K" } };
        var values = new List<string>();
        using var d = host.WhenChanged(h => h.Person.Given).Subscribe(values.Add);
        host.Person.Given = "L";
        host.Person.Given = "M";
        Assert.Equal(new[] { "K", "L", "M" }, values);
    }
}
