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
