using System.Linq.Expressions;
using R3;

namespace R3Ext;

public static partial class R3BindingExtensions
{
    public static partial IDisposable BindTwoWay<TFrom, TFromProperty, TTarget, TTargetProperty>(
        this TFrom fromObject,
        TTarget targetObject,
        Expression<Func<TFrom, TFromProperty>> fromProperty,
        Expression<Func<TTarget, TTargetProperty>> toProperty,
        Func<TFromProperty, TTargetProperty>? hostToTargetConv = null,
        Func<TTargetProperty, TFromProperty>? targetToHostConv = null,
        string? fromPropertyPath = null,
        string? toPropertyPath = null);

    public static partial IDisposable BindOneWay<TFrom, TFromProperty, TTarget, TTargetProperty>(
        this TFrom fromObject,
        TTarget targetObject,
        Expression<Func<TFrom, TFromProperty>> fromProperty,
        Expression<Func<TTarget, TTargetProperty>> toProperty,
        Func<TFromProperty, TTargetProperty>? conversionFunc = null,
        string? fromPropertyPath = null,
        string? toPropertyPath = null);

    public static partial Observable<TReturn> WhenChanged<TObj, TReturn>(
        this TObj objectToMonitor,
        Expression<Func<TObj, TReturn>> propertyExpression,
        string? propertyExpressionPath = null);

    public static partial Observable<TReturn> WhenObserved<TObj, TReturn>(
        this TObj objectToMonitor,
        Expression<Func<TObj, Observable<TReturn>>> propertyExpression,
        string? propertyExpressionPath = null);
}
