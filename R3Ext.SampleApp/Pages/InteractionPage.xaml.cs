using System;
using Microsoft.Maui.Controls;
using R3;
using R3Ext;
using R3Ext.SampleApp.ViewModels;

namespace R3Ext.SampleApp
{
    public partial class InteractionPage : ContentPage
    {
        private readonly InteractionViewModel _vm = new();
        private DisposableBag _disposables;

        public InteractionPage()
        {
            InitializeComponent();
            BindingContext = _vm;

            _vm.ConfirmDelete.RegisterHandler(async interaction =>
            {
                bool result = await DisplayAlert("Confirm", interaction.Input, "Delete", "Cancel");
                interaction.SetOutput(result);
            }).AddTo(ref _disposables);

            DeleteButton.Clicked +=
            async (_, __) =>
            {
                var fileName = FileEntry.Text ?? "report.pdf";
                await _vm.DeleteFileCommand.Execute(fileName).WaitAsync();
                StatusLabel.Text = _vm.LastAction.Value;
            };
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _disposables.Dispose();
        }
    }
}
