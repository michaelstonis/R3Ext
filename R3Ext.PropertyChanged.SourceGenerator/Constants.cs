using System;
namespace R3Ext.PropertyChanged.SourceGenerator;

// Minimal constants ported from original project; trimmed for initial incremental generation.
internal static partial class Constants
{
    public const string BindExtensionClassSource = """
using System;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using R3;
namespace R3Ext.PropertyChanged
{
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    [System.Diagnostics.DebuggerNonUserCode]
    [System.Reflection.Obfuscation(Exclude=true)]
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    internal static partial class BindingExtensions
    {
        // R3 version: removed IScheduler (use TimeProvider via ObservableSystem if needed) and System.Reactive types.
        public static partial IDisposable BindOneWay<TFrom,TPropertyType,TTarget>(
            this TFrom fromObject,
            TTarget targetObject,
            Expression<Func<TFrom,TPropertyType>> fromProperty,
            Expression<Func<TTarget,TPropertyType>> toProperty,
            object scheduler = null,
            [CallerMemberName] string callerMemberName = null,
            [CallerFilePath] string callerFilePath = null,
            [CallerLineNumber] int callerLineNumber = 0) where TFrom:class,INotifyPropertyChanged;

        public static partial IDisposable BindOneWay<TFrom,TFromProperty,TTarget,TTargetProperty>(
            this TFrom fromObject,
            TTarget targetObject,
            Expression<Func<TFrom,TFromProperty>> fromProperty,
            Expression<Func<TTarget,TTargetProperty>> toProperty,
            Func<TFromProperty,TTargetProperty> conversionFunc,
            object scheduler = null,
            [CallerMemberName] string callerMemberName = null,
            [CallerFilePath] string callerFilePath = null,
            [CallerLineNumber] int callerLineNumber = 0) where TFrom:class,INotifyPropertyChanged;

        public static partial IDisposable BindTwoWay<TFrom,TFromProperty,TTarget,TTargetProperty>(
            this TFrom fromObject,
            TTarget targetObject,
            Expression<Func<TFrom,TFromProperty>> fromProperty,
            Expression<Func<TTarget,TTargetProperty>> toProperty,
            Func<TFromProperty,TTargetProperty> hostToTargetConv,
            Func<TTargetProperty,TFromProperty> targetToHostConv,
            object scheduler = null,
            [CallerMemberName] string callerMemberName = null,
            [CallerFilePath] string callerFilePath = null,
            [CallerLineNumber] int callerLineNumber = 0) where TFrom:class,INotifyPropertyChanged where TTarget:class,INotifyPropertyChanged;

        public static partial IDisposable BindTwoWay<TFrom,TProperty,TTarget>(
            this TFrom fromObject,
            TTarget targetObject,
            Expression<Func<TFrom,TProperty>> fromProperty,
            Expression<Func<TTarget,TProperty>> toProperty,
            object scheduler = null,
            [CallerMemberName] string callerMemberName = null,
            [CallerFilePath] string callerFilePath = null,
            [CallerLineNumber] int callerLineNumber = 0) where TFrom:class,INotifyPropertyChanged where TTarget:class,INotifyPropertyChanged;
    }
}
""";

    public const string WhenExtensionClassSource = """
using System;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using R3;
namespace R3Ext.PropertyChanged
{
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    [System.Diagnostics.DebuggerNonUserCode]
    [System.Reflection.Obfuscation(Exclude=true)]
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    internal static partial class NotifyPropertyExtensions
    {
        // R3 version: Use Observable<TReturn> instead of IObservable<TReturn>.
        public static partial Observable<TReturn> WhenChanged<TObj,TReturn>(
            this TObj objectToMonitor,
            Expression<Func<TObj,TReturn>> propertyExpression,
            [CallerMemberName] string callerMemberName = null,
            [CallerFilePath] string callerFilePath = null,
            [CallerLineNumber] int callerLineNumber = 0) where TObj:INotifyPropertyChanged;

        public static partial Observable<TReturn> WhenChanging<TObj,TReturn>(
            this TObj objectToMonitor,
            Expression<Func<TObj,TReturn>> propertyExpression,
            [CallerMemberName] string callerMemberName = null,
            [CallerFilePath] string callerFilePath = null,
            [CallerLineNumber] int callerLineNumber = 0) where TObj:INotifyPropertyChanging;
    }
}
""";
}
