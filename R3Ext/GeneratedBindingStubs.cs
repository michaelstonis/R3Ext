using System;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using R3;

namespace R3Ext;

public static partial class BindingExtensions
{
    public static partial IDisposable BindTwoWay<TFrom,TFromProperty,TTarget,TTargetProperty>(
        this TFrom fromObject,
        TTarget targetObject,
        Expression<Func<TFrom,TFromProperty>> fromProperty,
        Expression<Func<TTarget,TTargetProperty>> toProperty,
        Func<TFromProperty,TTargetProperty> hostToTargetConv = null!,
        Func<TTargetProperty,TFromProperty> targetToHostConv = null!,
        [CallerArgumentExpression("fromProperty")] string fromPropertyPath = null!,
        [CallerArgumentExpression("toProperty")] string toPropertyPath = null!);

    public static partial IDisposable BindOneWay<TFrom,TFromProperty,TTarget,TTargetProperty>(
        this TFrom fromObject,
        TTarget targetObject,
        Expression<Func<TFrom,TFromProperty>> fromProperty,
        Expression<Func<TTarget,TTargetProperty>> toProperty,
        Func<TFromProperty,TTargetProperty> conversionFunc = null!,
        [CallerArgumentExpression("fromProperty")] string fromPropertyPath = null!,
        [CallerArgumentExpression("toProperty")] string toPropertyPath = null!);

    public static partial Observable<TReturn> WhenChanged<TObj,TReturn>(
        this TObj objectToMonitor,
        Expression<Func<TObj,TReturn>> propertyExpression,
        [CallerArgumentExpression("propertyExpression")] string propertyExpressionPath = null!);
}
