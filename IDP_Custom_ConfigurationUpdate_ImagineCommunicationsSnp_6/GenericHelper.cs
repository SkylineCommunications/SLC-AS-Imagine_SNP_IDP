using System;
using System.Diagnostics;
using System.Threading;

public static class GenericHelper
{
	/// <summary>
	///     Retry until success or until timeout.
	/// </summary>
	/// <param name="func">Operation to retry.</param>
	/// <param name="timeout">Max TimeSpan during which the operation specified in <paramref name="func" /> can be retried.</param>
	/// <param name="millisecondsInterval">Period between retries.</param>
	/// <returns>
	///     <c>true</c> if one of the retries succeeded within the specified <paramref name="timeout" />. Otherwise
	///     <c>false</c>.
	/// </returns>
	public static bool Retry(Func<bool> func, TimeSpan timeout, int millisecondsInterval)
	{
		bool success = false;

		var sw = new Stopwatch();
		sw.Start();

		do
		{
			success = func();

			if (!success)
			{
				Thread.Sleep(millisecondsInterval);
			}
		} while (!success && sw.Elapsed <= timeout);

		return success;
	}
}
