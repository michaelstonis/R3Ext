// Copyright (c) 2024 Michael Stonis. All rights reserved.
// Licensed under the MIT License.

using Avalonia.Controls;
using R3;
using R3Ext.Activation;
using R3Ext.Avalonia;
using R3Ext.SampleApp.ViewModels;

namespace R3Ext.Avalonia.SampleApp.Views;

/// <summary>
/// A second demo view to show navigation and activation lifecycle with R3Ext bindings.
/// </summary>
public partial class TimerDemoView : UserControl, IViewFor<TimerDemoViewModel>
{
    public TimerDemoView()
    {
        InitializeComponent();

        ViewModel = new TimerDemoViewModel();
        DataContext = ViewModel;

        // WhenActivated for view lifecycle with R3Ext bindings
        this.WhenActivated((ref DisposableBag disposables) =>
        {
            // R3Ext bindings
            this.BindOneWay(TimerLabel, v => v.ViewModel!.ElapsedDisplay.CurrentValue, l => l.Text)
                .AddTo(ref disposables);

            this.BindOneWay(StatusLabel, v => v.ViewModel!.Status.CurrentValue, l => l.Text)
                .AddTo(ref disposables);

            // Direct observable subscription
            ViewModel!.IsRunning
                .Subscribe(running => RunningLabel.Text = running.ToString())
                .AddTo(ref disposables);
        });
    }

    /// <summary>
    /// Gets or sets the ViewModel for this view.
    /// </summary>
    public TimerDemoViewModel? ViewModel { get; set; }

    /// <summary>
    /// Gets a value indicating whether to auto-activate the ViewModel.
    /// </summary>
    public bool AutoActivateViewModel => true;
}
