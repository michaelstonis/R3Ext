using System;
using System.Globalization;
using Microsoft.Maui.Controls;
using R3;
using R3Ext;
using R3Ext.SampleApp.ViewModels;

namespace R3Ext.SampleApp;

public partial class DeepBindingPage : ContentPage
{
    private readonly SampleViewModel _vm = new();
    private DisposableBag _bindings;

    private int _deepRootUpdates;
    private int _deepAUpdates;
    private int _deepBUpdates;
    private int _deepCUpdates;
    private int _deepDUpdates;
    private int _deepLeafUpdates;
    private int _deepLeafNameUpdates;

    private int _mixedRootUpdates;
    private int _mixedParentUpdates;
    private int _mixedChildUpdates;
    private int _mixedChildNameUpdates;

    public DeepBindingPage()
    {
        InitializeComponent();

        SetupBindings();
    }

    private void SetupBindings()
    {
        _vm.WhenChanged(v => v.Deep)
            .Subscribe(root => UpdateInstanceLabel(DeepRootLabel, ref _deepRootUpdates, root))
            .AddTo(ref _bindings);

        _vm.WhenChanged(v => v.Deep.A)
            .Subscribe(a => UpdateInstanceLabel(DeepALabel, ref _deepAUpdates, a))
            .AddTo(ref _bindings);

        _vm.WhenChanged(v => v.Deep.A.B)
            .Subscribe(b => UpdateInstanceLabel(DeepBLabel, ref _deepBUpdates, b))
            .AddTo(ref _bindings);

        _vm.WhenChanged(v => v.Deep.A.B.C)
            .Subscribe(c => UpdateInstanceLabel(DeepCLabel, ref _deepCUpdates, c))
            .AddTo(ref _bindings);

        _vm.WhenChanged(v => v.Deep.A.B.C.D)
            .Subscribe(d => UpdateInstanceLabel(DeepDLabel, ref _deepDUpdates, d))
            .AddTo(ref _bindings);

        _vm.WhenChanged(v => v.Deep.A.B.C.D.Leaf)
            .Subscribe(leaf => UpdateInstanceLabel(DeepLeafLabel, ref _deepLeafUpdates, leaf))
            .AddTo(ref _bindings);

        this.BindTwoWay(DeepLeafEntry, v => v._vm.Deep.A.B.C.D.Leaf.Name, e => e.Text)
            .AddTo(ref _bindings);

        this.BindOneWay(DeepLeafMirrorLabel, v => v._vm.Deep.A.B.C.D.Leaf.Name, l => l.Text, name => $"Deep Leaf Mirror: {name ?? "(empty)"}")
            .AddTo(ref _bindings);

        _vm.WhenChanged(v => v.Deep.A.B.C.D.Leaf.Name)
            .Subscribe(name => UpdateValueLabel(DeepLeafNameLabel, ref _deepLeafNameUpdates, name))
            .AddTo(ref _bindings);

        _vm.WhenChanged(v => v.Mixed)
            .Subscribe(root => UpdateInstanceLabel(MixedRootLabel, ref _mixedRootUpdates, root))
            .AddTo(ref _bindings);

        _vm.WhenChanged(v => v.Mixed.NonNotify)
            .Subscribe(nonNotify => UpdateInstanceLabel(MixedParentLabel, ref _mixedParentUpdates, nonNotify))
            .AddTo(ref _bindings);

        _vm.WhenChanged(v => v.Mixed.NonNotify.Child)
            .Subscribe(child =>
            {
                UpdateInstanceLabel(MixedChildLabel, ref _mixedChildUpdates, child);
                UpdateValueLabel(MixedChildNameLabel, ref _mixedChildNameUpdates, child?.Name);
            })
            .AddTo(ref _bindings);

        this.BindTwoWay(MixedChildEntry, v => v._vm.Mixed.NonNotify.Child.Name, e => e.Text)
            .AddTo(ref _bindings);

        this.BindOneWay(MixedChildMirrorLabel, v => v._vm.Mixed.NonNotify.Child.Name, l => l.Text, name => $"Mixed Child Mirror: {name ?? "(empty)"}")
            .AddTo(ref _bindings);
    }

    private static string FormatTimestamp() => DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);

    private static string DescribeInstance(object? value)
        => value is null ? "(null)" : $"{value.GetType().Name} #{value.GetHashCode():X8}";

    private void UpdateInstanceLabel<T>(Label label, ref int counter, T instance)
    {
        counter++;
        label.Text = $"{counter}: {DescribeInstance(instance)} @ {FormatTimestamp()}";
    }

    private void UpdateValueLabel(Label label, ref int counter, string? value)
    {
        counter++;
        var formatted = string.IsNullOrEmpty(value) ? "(empty)" : value;
        label.Text = $"{counter}: {formatted} @ {FormatTimestamp()}";
    }

    private void OnMutate(object sender, EventArgs e)
    {
        var stamp = FormatTimestamp();

        _vm.Deep = new SampleViewModel.DeepRoot
        {
            A = new SampleViewModel.DeepA
            {
                B = new SampleViewModel.DeepB
                {
                    C = new SampleViewModel.DeepC
                    {
                        D = new SampleViewModel.DeepD
                        {
                            Leaf = new SampleViewModel.DeepLeaf { Name = $"Leaf {stamp}" }
                        }
                    }
                }
            }
        };

        _vm.Mixed = new SampleViewModel.MixedRoot
        {
            NonNotify = new SampleViewModel.NonNotifyParent
            {
                Child = new Person { Name = $"Mixed {stamp}" }
            }
        };
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _bindings.Dispose();
    }
}
