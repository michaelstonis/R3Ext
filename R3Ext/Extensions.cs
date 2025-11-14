using System;
using Microsoft.Extensions.Logging;
using R3;

namespace R3Ext;

public static class ObservableDebugExtensions
{
	public static Observable<T> Log<T>(this Observable<T> source, ILogger? logger = null, string? tag = null)
	{
		var prefix = string.IsNullOrWhiteSpace(tag) ? "R3Ext" : tag;

		if (logger != null)
		{
			return source.Do(
				onNext: x => logger.LogInformation("[{Prefix}] OnNext: {Value}", prefix, x),
				onErrorResume: ex => logger.LogError(ex, "[{Prefix}] OnErrorResume: {Message}", prefix, ex.Message),
				onCompleted: r =>
				{
					if (r.IsSuccess)
						logger.LogInformation("[{Prefix}] OnCompleted: Success", prefix);
					else
						logger.LogError(r.Exception, "[{Prefix}] OnCompleted: {Message}", prefix, r.Exception?.Message);
				}
			);
		}
		else
		{
			return source.Do(
				onNext: x => System.Diagnostics.Debug.WriteLine($"[{prefix}] OnNext: {x}"),
				onErrorResume: ex => System.Diagnostics.Debug.WriteLine($"[{prefix}] OnErrorResume: {ex.Message}"),
				onCompleted: r => System.Diagnostics.Debug.WriteLine($"[{prefix}] OnCompleted: {(r.IsSuccess ? "Success" : r.Exception?.Message)}")
			);
		}
	}
}

