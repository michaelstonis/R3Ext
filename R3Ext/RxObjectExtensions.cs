using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace R3Ext;

/// <summary>
/// Extension methods for <see cref="RxObject"/> and <see cref="RxRecord"/> 
/// providing convenient property change helpers.
/// </summary>
public static class RxObjectExtensions
{
    /// <summary>
    /// Extension variant of RaiseAndSetIfChanged for use outside the declaring class.
    /// Compares the backing field to the new value, raises PropertyChanging/PropertyChanged events if different, and updates the field.
    /// </summary>
    public static TRet RaiseAndSetIfChanged<TObj, TRet>(
        this TObj reactiveObject,
        ref TRet backingField,
        TRet newValue,
        [CallerMemberName] string? propertyName = null)
        where TObj : INotifyPropertyChanged, INotifyPropertyChanging
    {
        if (propertyName == null) throw new ArgumentNullException(nameof(propertyName));
        if (EqualityComparer<TRet>.Default.Equals(backingField, newValue))
            return newValue;

        reactiveObject.RaisePropertyChanging(propertyName);
        backingField = newValue;
        reactiveObject.RaisePropertyChanged(propertyName);
        return newValue;
    }

    /// <summary>
    /// Explicitly raise PropertyChanged for a given property. 
    /// Useful for custom properties that compute derived state.
    /// </summary>
    public static void RaisePropertyChanged<T>(this T reactiveObject, [CallerMemberName] string? propertyName = null)
        where T : INotifyPropertyChanged
    {
        if (propertyName == null) return;
        if (reactiveObject is RxObject rxObj)
            rxObj.RaisePropertyChanged(propertyName);
        else if (reactiveObject is RxRecord rxRec)
            rxRec.RaisePropertyChanged(propertyName);
    }

    /// <summary>
    /// Explicitly raise PropertyChanging for a given property.
    /// </summary>
    public static void RaisePropertyChanging<T>(this T reactiveObject, [CallerMemberName] string? propertyName = null)
        where T : INotifyPropertyChanging
    {
        if (propertyName == null) return;
        if (reactiveObject is RxObject rxObj)
            rxObj.RaisePropertyChanging(propertyName);
        else if (reactiveObject is RxRecord rxRec)
            rxRec.RaisePropertyChanging(propertyName);
    }
}
