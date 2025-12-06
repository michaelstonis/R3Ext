using Avalonia.Controls;
using R3Ext.Avalonia.SampleApp.ViewModels;

namespace R3Ext.Avalonia.SampleApp.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
    }
}
