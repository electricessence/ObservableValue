using System;
using System.Reactive.Subjects;
using System.Threading;
using Open.Threading;

namespace Open.Observable
{
	public class ObservableValue<T> : IObservable<T>, IDisposable, IEquatable<T>
	{
		/// <summary>
		/// Constructs an ObservableValue.
		/// </summary>
		/// <typeparam name="T">The type of the value.</typeparam>
		/// <param name="initialValue">The initial value</param>
		public ObservableValue(T initialValue = default)
		{
			_value = initialValue;
			_subject = new Subject<T>();
			Sync = new Lazy<ReaderWriterLockSlim>(()=> new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion), true);
		}

		private readonly Subject<T> _subject;

		private static readonly Lazy<ReaderWriterLockSlim> DisposedReadWriteLock = new Lazy<ReaderWriterLockSlim>(() =>
		{
			var sync = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
			sync.Dispose();
			return sync;
		}, false);

		// Prevents infinite recursion and ensures values are synchronized.
		private Lazy<ReaderWriterLockSlim> Sync;

		private T _value;

		/// <summary>
		/// Gets the current value.
		/// </summary>
		public T Value => Sync.Value.ReadValue(() => _value);

		/// <summary>
		/// Updates the value and posts updates to the subscribers if the value changed..
		/// </summary>
		/// <param name="value">The value to post.</param>
		/// <returns>True if the value changed.  False if unchanged.</returns>
		public virtual bool Post(T value) => Sync.Value.ReadUpgradeableWriteConditional(() => !AreEqual(value, _value), () =>
		{
			_value = value;
			_subject.OnNext(value);
		});

		private bool AreEqual(T a, T b)
		{
			if (_disposed == 1) throw new ObjectDisposedException(GetType().ToString());
			return a is null ? b is null : a.Equals(b);
		}

		/// <inheritdoc />
		public IDisposable Subscribe(IObserver<T> observer) => Sync.Value.ReadValue(() =>
		{
			var sub = _subject.Subscribe(observer); // ensures observer is not null and subject not disposed.
			observer.OnNext(_value);
			return sub;
		});

		private int _disposed;
		protected virtual void Dispose(bool disposing)
		{
			if (Interlocked.CompareExchange(ref _disposed, 1, 0) == 0)
			{
				if (disposing)
				{
					// Swap sync with disposed version to prevent further queueing of reads and writes.
					var sync = Interlocked.Exchange(ref Sync, DisposedReadWriteLock);
					_subject.Dispose();
					if (sync.IsValueCreated)
					{
						var s = sync.Value;
						// Force clearing any existing reads/writes.
						s.Write(() => _value = default!);
						s.Dispose();
					}
					else
					{
						_value = default!;
					}
				}
			}
		}

		/// <inheritdoc />
		public void Dispose()
		{
			// Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
			Dispose(disposing: true);
			GC.SuppressFinalize(this);
		}

		/// <inheritdoc />
		public bool Equals(T other) => AreEqual(other, Value);

		[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1065:Do not raise exceptions in unexpected locations", Justification = "Nullable enable should protected this but have to be sure.")]
		public static implicit operator T(ObservableValue<T> o)
		{
			if (o is null) throw new ArgumentNullException(nameof(o));
			return o.Value;
		}
	}

	public static class ObservableValue
	{
		/// <summary>
		/// Creates an ObservableValue.
		/// </summary>
		/// <typeparam name="T">The type of the value.</typeparam>
		/// <param name="initialValue">The initial value</param>
		/// <returns>The ObservableValue created.</returns>
		public static ObservableValue<T> Create<T>(T initialValue = default) => new ObservableValue<T>(initialValue);
	}
}
