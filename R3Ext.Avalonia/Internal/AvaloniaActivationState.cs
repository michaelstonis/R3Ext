// Copyright (c) 2024 Michael Stonis. All rights reserved.
// Licensed under the MIT License.

using Avalonia;
using Avalonia.VisualTree;
using R3;
using R3Ext.Activation;

namespace R3Ext.Avalonia.Internal;

/// <summary>
/// Manages activation state for an Avalonia visual.
/// </summary>
internal sealed class AvaloniaActivationState
{
    private readonly Visual _visual;
    private readonly ActivationBlock _activationBlock;
    private readonly Func<ViewModelActivator?>? _getActivator;
    private DisposableBag _currentBag;
    private bool _isActivated;

    public AvaloniaActivationState(
        Visual visual,
        ActivationBlock activationBlock,
        Func<ViewModelActivator?>? getActivator = null)
    {
        _visual = visual;
        _activationBlock = activationBlock;
        _getActivator = getActivator;
    }

    public IDisposable Start()
    {
        // Subscribe to visual tree events
        _visual.AttachedToVisualTree += OnAttached;
        _visual.DetachedFromVisualTree += OnDetached;

        // Check initial state
        if (_visual.IsAttachedToVisualTree())
        {
            Activate();
        }

        return Disposable.Create(Cleanup);
    }

    private void OnAttached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        Activate();
    }

    private void OnDetached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        Deactivate();
    }

    private void Activate()
    {
        if (_isActivated)
        {
            return;
        }

        _isActivated = true;

        // Activate ViewModel if available
        _getActivator?.Invoke()?.Activate();

        // Run activation block
        _currentBag = default;
        _activationBlock(ref _currentBag);
    }

    private void Deactivate()
    {
        if (!_isActivated)
        {
            return;
        }

        _isActivated = false;

        // Dispose activation resources
        _currentBag.Dispose();

        // Deactivate ViewModel if available
        _getActivator?.Invoke()?.Deactivate();
    }

    private void Cleanup()
    {
        _visual.AttachedToVisualTree -= OnAttached;
        _visual.DetachedFromVisualTree -= OnDetached;

        Deactivate();
    }
}

/// <summary>
/// Manages attachment state for an Avalonia visual.
/// </summary>
internal sealed class AvaloniaAttachmentState
{
    private readonly Visual _visual;
    private readonly ActivationBlock _activationBlock;
    private readonly Func<ViewModelAttacher?>? _getAttacher;
    private DisposableBag _currentBag;
    private bool _isAttached;

    public AvaloniaAttachmentState(
        Visual visual,
        ActivationBlock activationBlock,
        Func<ViewModelAttacher?>? getAttacher = null)
    {
        _visual = visual;
        _activationBlock = activationBlock;
        _getAttacher = getAttacher;
    }

    public IDisposable Start()
    {
        // Subscribe to visual tree events
        _visual.AttachedToVisualTree += OnAttached;
        _visual.DetachedFromVisualTree += OnDetached;

        // Check initial state
        if (_visual.IsAttachedToVisualTree())
        {
            Attach();
        }

        return Disposable.Create(Cleanup);
    }

    private void OnAttached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        Attach();
    }

    private void OnDetached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        Detach();
    }

    private void Attach()
    {
        if (_isAttached)
        {
            return;
        }

        _isAttached = true;

        // Attach ViewModel if available
        _getAttacher?.Invoke()?.Attach();

        // Run activation block
        _currentBag = default;
        _activationBlock(ref _currentBag);
    }

    private void Detach()
    {
        if (!_isAttached)
        {
            return;
        }

        _isAttached = false;

        // Dispose activation resources
        _currentBag.Dispose();

        // Detach ViewModel if available
        _getAttacher?.Invoke()?.Detach();
    }

    private void Cleanup()
    {
        _visual.AttachedToVisualTree -= OnAttached;
        _visual.DetachedFromVisualTree -= OnDetached;

        Detach();
    }
}
