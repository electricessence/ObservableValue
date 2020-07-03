using System;
using System.Collections.Generic;
using System.Text;

namespace Open.Observable
{
	public interface IObservableValue<T> : IObservable<T>
	{
		T Value { get; }
		bool IsInitialized { get; }
	}
}
