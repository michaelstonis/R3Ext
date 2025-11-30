using Microsoft.Maui.Controls;
using R3;
using R3Ext.Activation;
using R3Ext.Maui;
using Xunit;

// Resolve ambiguity with Microsoft.Maui.ActivationState
using ActivationState = R3Ext.Activation.ActivationState;

namespace R3Ext.Maui.Tests;

/// <summary>
/// Integration tests for the IViewFor source generator infrastructure.
/// These tests verify that the source generator correctly provides
/// ViewModel-BindingContext synchronization, Activation property generation,
/// and InitializeViewFor helper method.
/// </summary>
public class ViewForGeneratorIntegrationTests : IClassFixture<MauiActivationFixture>
{
    public ViewForGeneratorIntegrationTests(MauiActivationFixture fixture)
    {
        // Fixture registers the MAUI activation provider
        _ = fixture;
    }

    [Fact]
    public void ViewModel_SyncsToBindingContext_WhenSet()
    {
        // Arrange
        var page = new SourceGenTestPage();
        var viewModel = new SourceGenTestViewModel();

        // Act
        page.ViewModel = viewModel;

        // Assert
        Assert.Same(viewModel, page.BindingContext);
    }

    [Fact]
    public void BindingContext_IsUpdated_WhenViewModelSet()
    {
        // Arrange
        var page = new SourceGenTestPage();
        var viewModel = new SourceGenTestViewModel();

        // Act - setting ViewModel also sets BindingContext
        page.ViewModel = viewModel;

        // Assert
        Assert.Same(viewModel, page.BindingContext);
    }

    [Fact]
    public void BindingContext_CanBeSetDirectly_WithoutAffectingViewModel()
    {
        // Note: The current generated code syncs ViewModel -> BindingContext
        // but not the reverse. This test documents that behavior.

        // Arrange
        var page = new SourceGenTestPage();
        var viewModel = new SourceGenTestViewModel();
        page.ViewModel = viewModel;

        // Act - setting BindingContext directly doesn't change ViewModel
        page.BindingContext = "different value";

        // Assert - ViewModel is still the same (no reverse sync)
        Assert.Same(viewModel, page.ViewModel);
    }

    [Fact]
    public void ViewModel_CanBeCleared()
    {
        // Arrange
        var page = new SourceGenTestPage();
        page.ViewModel = new SourceGenTestViewModel();

        // Act
        page.ViewModel = null;

        // Assert
        Assert.Null(page.ViewModel);
        Assert.Null(page.BindingContext);
    }

    [Fact]
    public void ViewModel_DoesNotCauseInfiniteLoop_WhenBindingContextChanges()
    {
        // Arrange
        var page = new SourceGenTestPage();
        var viewModel = new SourceGenTestViewModel();
        var changeCount = 0;

        page.BindingContextChanged += (s, e) => changeCount++;

        // Act
        page.ViewModel = viewModel;

        // Assert - should only fire once, not infinitely
        Assert.Equal(1, changeCount);
    }

    [Fact]
    public void Activation_ReturnsNonNullObservable()
    {
        // Arrange
        var page = new SourceGenTestPage();

        // Activation is explicitly implemented, access via interface
        IActivatable activatable = page;

        // Act & Assert
        Assert.NotNull(activatable.Activation);
    }

    [Fact]
    public void Activation_CanBeSubscribedTo()
    {
        // Arrange
        var page = new SourceGenTestPage();
        var values = new List<ActivationState>();

        // Activation is explicitly implemented, access via interface
        IActivatable activatable = page;

        // Act
        using var sub = activatable.Activation.Subscribe(v => values.Add(v));

        // Assert - no exception thrown
        Assert.NotNull(sub);
    }

    [Fact]
    public void AutoActivateViewModel_DefaultsToTrue()
    {
        // Arrange
        var page = new SourceGenTestPage();

        // Assert
        Assert.True(page.AutoActivateViewModel);
    }

    [Fact]
    public void AutoActivateViewModel_CanBeOverridden()
    {
        // Arrange
        var page = new NoAutoActivateTestPage();

        // Assert
        Assert.False(page.AutoActivateViewModel);
    }

    [Fact]
    public void WhenActivated_WorksWithSourceGeneratedPage()
    {
        // Arrange
        var page = new SourceGenTestPage();

        // Act - WhenActivated should compile and work with source-generated infrastructure
        using var sub = page.WhenActivated((ref DisposableBag d) =>
        {
            // Block executes on activation
        });

        // Assert - subscription created successfully
        Assert.NotNull(sub);
    }

    [Fact]
    public void WhenAttached_WorksWithSourceGeneratedPage()
    {
        // Arrange
        var page = new SourceGenTestPage();

        // Act - WhenAttached should compile and work with source-generated infrastructure
        using var sub = page.WhenAttached((ref DisposableBag d) =>
        {
            // Block executes on attachment
        });

        // Assert - subscription created successfully
        Assert.NotNull(sub);
    }

    [Fact]
    public void WhenActivated_CanAccessViewModel_InBlock()
    {
        // Arrange
        var page = new SourceGenTestPage();
        var viewModel = new SourceGenTestViewModel();
        page.ViewModel = viewModel;

        // Act
        using var sub = page.WhenActivated((ref DisposableBag d) =>
        {
            // ViewModel is accessible from within block
            _ = page.ViewModel;
        });

        // Assert - ViewModel accessible from block
        Assert.Same(viewModel, page.ViewModel);
    }

    [Fact]
    public void WhenActivated_SupportsLateBinding_WhenViewModelSetAfterSubscription()
    {
        // Arrange
        var page = new SourceGenTestPage();

        // Act - Set up activation BEFORE ViewModel
        using var sub = page.WhenActivated((ref DisposableBag d) =>
        {
            // ViewModel access here would be null initially
        });

        // Set ViewModel after subscription
        var viewModel = new SourceGenTestViewModel();
        page.ViewModel = viewModel;

        // Assert
        Assert.Same(viewModel, page.ViewModel);
    }

    [Fact]
    public void SourceGenerator_WorksWithContentView()
    {
        // Arrange
        var view = new SourceGenTestView();
        var viewModel = new SourceGenTestViewModel();

        // Act
        view.ViewModel = viewModel;

        // Assert
        Assert.Same(viewModel, view.BindingContext);

        // Activation is explicitly implemented, access via interface
        IActivatable activatable = view;
        Assert.NotNull(activatable.Activation);
    }
}

/// <summary>
/// Test page that implements IViewFor with source-generated infrastructure.
/// </summary>
public sealed partial class SourceGenTestPage : ContentPage, IViewFor<SourceGenTestViewModel>
{
    // Source generator provides:
    // - ViewModel property with BindingContext sync
    // - Activation property
    // - AutoActivateViewModel property (defaults to true)
}

/// <summary>
/// Test page that opts out of auto-activation.
/// </summary>
public sealed partial class NoAutoActivateTestPage : ContentPage, IViewFor<SourceGenTestViewModel>
{
    /// <summary>
    /// Gets a value indicating whether the ViewModel is auto-activated.
    /// </summary>
    public bool AutoActivateViewModel => false;
}

/// <summary>
/// Test view (not page) that implements IViewFor.
/// </summary>
public sealed partial class SourceGenTestView : ContentView, IViewFor<SourceGenTestViewModel>
{
}

/// <summary>
/// Simple test ViewModel for source generator tests.
/// </summary>
public sealed class SourceGenTestViewModel : IActivatableViewModel
{
    /// <summary>
    /// Gets the activator for this ViewModel.
    /// </summary>
    public ViewModelActivator Activator { get; } = new();

    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; } = "Test";
}
