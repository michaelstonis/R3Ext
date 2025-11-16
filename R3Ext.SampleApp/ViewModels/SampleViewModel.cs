using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace R3Ext.SampleApp.ViewModels;

public class Person : INotifyPropertyChanged
{
    private string _name = string.Empty;
    public string Name
    {
        get => _name;
        set { if (_name != value) { _name = value; OnPropertyChanged(); } }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? member = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(member));
}

public class SampleViewModel : INotifyPropertyChanged
{
    private int _counter;
    private Person _person = new();
    private string _editableName = string.Empty;
    private string _status = string.Empty;

    public int Counter
    {
        get => _counter;
        set { if (_counter != value) { _counter = value; OnPropertyChanged(); } }
    }

    public Person Person
    {
        get => _person;
        set { if (!ReferenceEquals(_person, value)) { _person = value; OnPropertyChanged(); } }
    }

    public string EditableName
    {
        get => _editableName;
        set
        {
            if (_editableName != value)
            {
                _editableName = value;
                // keep Person.Name in sync for demonstration
                if (Person.Name != value) Person.Name = value;
                OnPropertyChanged();
            }
        }
    }

    public string Status
    {
        get => _status;
        private set { if (_status != value) { _status = value; OnPropertyChanged(); } }
    }

    public void Increment()
    {
        Counter++;
        Person.Name = "Name " + Counter;
        EditableName = Person.Name;
        Status = Counter switch
        {
            < 5 => "Low",
            < 10 => "Medium",
            _ => "High"
        };
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? member = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(member));
}