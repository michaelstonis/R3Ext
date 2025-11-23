using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace R3Ext.SampleApp.ViewModels;

public class PersonWithId : ObservableObject
{
    private int _id;
    private string _name = string.Empty;
    private int _age;
    private string _city = string.Empty;

    public int Id
    {
        get => _id;
        set => this.SetProperty(ref _id, value);
    }

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

    public string City
    {
        get => _city;
        set => this.SetProperty(ref _city, value);
    }
}

public class PersonWithHobbies : ObservableObject
{
    private string _name = string.Empty;
    private int _age;
    private string _city = string.Empty;
    private List<string> _hobbies = new();

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

    public string City
    {
        get => _city;
        set => this.SetProperty(ref _city, value);
    }

    public List<string> Hobbies
    {
        get => _hobbies;
        set => this.SetProperty(ref _hobbies, value);
    }
}
