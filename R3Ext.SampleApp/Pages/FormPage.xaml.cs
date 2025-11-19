using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using R3;

namespace R3Ext.SampleApp;

public partial class FormPage : ContentPage
{
    public sealed class FormViewModel : ObservableObject
    {
        private string _name = string.Empty;
        private string _email = string.Empty;
        private bool _acceptTerms;

        public string Name
        {
            get => _name;
            set => this.SetProperty(ref _name, value);
        }

        public string Email
        {
            get => _email;
            set => this.SetProperty(ref _email, value);
        }

        public bool AcceptTerms
        {
            get => _acceptTerms;
            set => this.SetProperty(ref _acceptTerms, value);
        }
    }

    private readonly FormViewModel _vm = new();
    private DisposableBag _bindings;

    public FormPage()
    {
        this.InitializeComponent();
        this.SetupBindings();
    }

    private void SetupBindings()
    {
        // Two-way input bindings
        _vm.BindTwoWay(NameEntry, v => v.Name, c => c.Text).AddTo(ref _bindings);
        _vm.BindTwoWay(EmailEntry, v => v.Email, c => c.Text).AddTo(ref _bindings);
        _vm.BindTwoWay(TermsSwitch, v => v.AcceptTerms, c => c.IsToggled).AddTo(ref _bindings);

        // Validation streams
        Observable<bool> nameValid = _vm.WhenChanged(v => v.Name)
            .Select(n => !string.IsNullOrWhiteSpace(n) && n.Trim().Length >= 2)
            .Share();

        Observable<bool> emailValid = _vm.WhenChanged(v => v.Email)
            .Select(IsValidEmail)
            .Share();

        var termsValid = _vm.WhenChanged(v => v.AcceptTerms)
            .Share();

        // Error labels
        nameValid.Subscribe(ok => NameError.Text = ok ? string.Empty : "Name must be at least 2 characters.").AddTo(ref _bindings);
        emailValid.Subscribe(ok => EmailError.Text = ok ? string.Empty : "Please enter a valid email.").AddTo(ref _bindings);

        // Enable submit when all valid
        new[] { nameValid, emailValid, termsValid, }.CombineLatestValuesAreAllTrue()
            .Subscribe(ok => SubmitBtn.IsEnabled = ok)
            .AddTo(ref _bindings);
    }

    private static bool IsValidEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        // very simple pattern for demo purposes
        return Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$");
    }

    private void OnSubmit(object? sender, EventArgs e)
    {
        SubmitStatus.Text = $"Submitted: {_vm.Name} <{_vm.Email}>";
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _bindings.Dispose();
    }
}
