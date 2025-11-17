using System;
using Microsoft.Maui.Controls;
using R3Ext.SampleApp.ViewModels;

namespace R3Ext.SampleApp;

public partial class DeepBindingPage : ContentPage
{
    private readonly SampleViewModel _vm = new();

    public DeepBindingPage()
    {
        InitializeComponent();

        this.BindOneWay(DeepNameLabel, v => v._vm.Deep.A.B.C.D.Leaf.Name, l => l.Text);
        this.BindOneWay(MixedNameLabel, v => v._vm.Mixed.NonNotify.Child!.Name, l => l.Text);
    }

    private void OnMutate(object sender, EventArgs e)
    {
        _vm.Deep.A.B.C.D.Leaf.Name = DateTime.Now.ToLongTimeString();
    }
}
