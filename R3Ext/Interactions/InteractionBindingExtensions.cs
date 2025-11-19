using System.Linq.Expressions;
using R3;

namespace R3Ext;

public static class InteractionBindingExtensions
{
    public static IDisposable BindInteraction<TViewModel, TInput, TOutput>(
        this TViewModel viewModel,
        Expression<Func<TViewModel, Interaction<TInput, TOutput>?>> interactionProperty,
        Func<IInteractionContext<TInput, TOutput>, Task> handler,
        [System.Runtime.CompilerServices.CallerArgumentExpression("interactionProperty")]
        string? propertyExpressionPath = null)
    {
        if (viewModel is null)
        {
            throw new ArgumentNullException(nameof(viewModel));
        }

        if (interactionProperty is null)
        {
            throw new ArgumentNullException(nameof(interactionProperty));
        }

        if (handler is null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        Interaction<TInput, TOutput>? currentInstance = null;
        IDisposable? currentRegistration = null;

        IDisposable subscription = viewModel.WhenChanged(interactionProperty, propertyExpressionPath)
            .Subscribe(newInstance =>
            {
                if (!ReferenceEquals(newInstance, currentInstance))
                {
                    currentRegistration?.Dispose();
                    currentInstance = newInstance;
                    currentRegistration = newInstance is null ? null : newInstance.RegisterHandler(handler);
                }
            });

        return Disposable.Create(() =>
        {
            subscription.Dispose();
            currentRegistration?.Dispose();
        });
    }

    public static IDisposable BindInteraction<TViewModel, TInput, TOutput>(
        this TViewModel viewModel,
        Expression<Func<TViewModel, Interaction<TInput, TOutput>?>> interactionProperty,
        Action<IInteractionContext<TInput, TOutput>> handler,
        [System.Runtime.CompilerServices.CallerArgumentExpression("interactionProperty")]
        string? propertyExpressionPath = null)
    {
        if (handler is null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        return viewModel.BindInteraction(interactionProperty, ctx =>
        {
            handler(ctx);
            return Task.CompletedTask;
        }, propertyExpressionPath);
    }

    public static IDisposable BindInteraction<TViewModel, TInput, TOutput, TDontCare>(
        this TViewModel viewModel,
        Expression<Func<TViewModel, Interaction<TInput, TOutput>?>> interactionProperty,
        Func<IInteractionContext<TInput, TOutput>, Observable<TDontCare>> handler,
        [System.Runtime.CompilerServices.CallerArgumentExpression("interactionProperty")]
        string? propertyExpressionPath = null)
    {
        if (viewModel is null)
        {
            throw new ArgumentNullException(nameof(viewModel));
        }

        if (interactionProperty is null)
        {
            throw new ArgumentNullException(nameof(interactionProperty));
        }

        if (handler is null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        Interaction<TInput, TOutput>? currentInstance = null;
        IDisposable? currentRegistration = null;

        IDisposable subscription = viewModel.WhenChanged(interactionProperty, propertyExpressionPath)
            .Subscribe(newInstance =>
            {
                if (!ReferenceEquals(newInstance, currentInstance))
                {
                    currentRegistration?.Dispose();
                    currentInstance = newInstance;
                    currentRegistration = newInstance is null ? null : newInstance.RegisterHandler(handler);
                }
            });

        return Disposable.Create(() =>
        {
            subscription.Dispose();
            currentRegistration?.Dispose();
        });
    }
}
