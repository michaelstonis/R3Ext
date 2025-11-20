using CommunityToolkit.Mvvm.ComponentModel;

namespace R3Ext.SampleApp.ViewModels;

public class Person : ObservableObject
{
    private string _name = string.Empty;

    public string Name
    {
        get => _name;
        set => this.SetProperty(ref _name, value);
    }
}

public class PersonWithAge : ObservableObject
{
    private string _name = string.Empty;
    private int _age;

    public string Name
    {
        get => _name;
        set => this.SetProperty(ref _name, value);
    }

    public int Age
    {
        get => _age;
        set => this.SetProperty(ref _age, value);
    }
}
