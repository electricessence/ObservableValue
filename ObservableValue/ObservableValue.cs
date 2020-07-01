using System;
using System.Diagnostics.CodeAnalysis;
using System.Reactive.Subjects;
using System.Threading;
using Open.Threading;

namespace Open.Observable
{
	public sealed class ObservableValue<T> : SubjectBase<T>, IEquatable<T>
	{
		#region Constructors
		/// <summary>
		/// Constructs an ObservableValue.
		/// </summary>
		public ObservableValue()
		{
			_subject = new Subject<T>();
			Sync = new Lazy<ReaderWriterLockSlim>(() => new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion), true);
		}

		/// <summary>
		/// Constructs an ObservableValue.
		/// </summary>
		/// <typeparam name="T">The type of the value.</typeparam>
		/// <param name="initialValue">The initial value</param>
		public ObservableValue(T value) : this()
		{
			IsInitialized = true;
			_value = value;
		}
		#endregion

		private readonly Subject<T> _subject;

		private static readonly Lazy<ReaderWriterLockSlim> DisposedReadWriteLock = new Lazy<ReaderWriterLockSlim>(() =>
		{
			var sync = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
			sync.Dispose();
			return sync;
		}, false);

		// Prevents infinite recursion and ensures values are synchronized.
		private Lazy<ReaderWriterLockSlim> Sync;

#if NETSTANDARD2_1
		[AllowNull]
#endif
		private T _value = default!;

		/// <summary>
		/// The current value.
		/// </summary>
		public T Value => _value;

		/// <summary>
		/// True if the value has been initialzed.
		/// </summary>
		public bool IsInitialized { get; private set; }

		public void ReadSyncronized(Action<T> reader) => Sync.Value.Read(() => reader(_value));

		/// <summary>
		/// If the value is not yet initialzed, the value set, the subscribers are notified and this returns true.
		/// If the value is intialized, nothing is done, and this returns false.
		/// </summary>
		/// <param name="value">The value to initialize and send to all observers.</param>
		public bool Init(T value)
		{
			if (IsInitialized) return false;
			var initialized = false;
			var sync = Sync.Value;
			sync.ReadUpgradeable(() =>
			{
				if (IsInitialized) return;
				var initialized = false;
				sync.Write(() =>
				{
					if (IsInitialized) return;
					_value = value;
					IsInitialized = initialized = true;
				});
				if (initialized) _subject.OnNext(value);
			});
			return initialized;
		}

		/// <summary>
		/// Updates the value and posts updates to the subscribers if the value changed..
		/// </summary>
		/// <param name="value">The value to post.</param>
		/// <returns>True if the value changed.  False if unchanged.</returns>
		public bool Post(T value, bool onlyNotifyIfChanged = false)
		{
			var updated = false;
			var sync = Sync.Value;
			sync.ReadUpgradeable(() =>
			{
				var initialized = false;
				sync.WriteConditional(
					writeLock => !(initialized = IsInitialized) || !onlyNotifyIfChanged || !AreEqual(value, _value),
					() =>
					{
						_value = value;
						updated = true;
						if (!initialized) IsInitialized = true;
					});

				if (updated) _subject.OnNext(value);
			});
			return updated;
		}

		private bool AreEqual(T a, T b)
		{
			if (_disposed == 1) throw new ObjectDisposedException(GetType().ToString());
			return a is null ? b is null : a.Equals(b);
		}

		#region SubjectBase<T> Implementation
		/// <inheritdoc />
		public override bool IsDisposed => _disposed == 1;

		/// <inheritdoc />
		public override bool HasObservers => _subject.HasObservers;

		/// <inheritdoc />
		public override void OnNext(T value) => Post(value);

		/// <inheritdoc />
		public override void OnError(Exception error) => Sync.Value.Write(() => _subject.OnError(error));

		/// <inheritdoc />
		public override void OnCompleted() => Sync.Value.Write(() => _subject.OnCompleted());

		/// <inheritdoc />
		public override IDisposable Subscribe(IObserver<T> observer) => Sync.Value.ReadValue(() =>
		{
			var sub = _subject.Subscribe(observer); // ensures observer is not null and subject not disposed.
			if (IsInitialized) observer.OnNext(_value);
			return sub;
		});
		#endregion

		#region IDisposable
		private int _disposed;

		void Dispose(bool disposing)
		{
			if (!disposing) return;

			// Swap sync with disposed version to prevent further queueing of reads and writes.
			var sync = Interlocked.Exchange(ref Sync, DisposedReadWriteLock);
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
			_subject.Dispose();
		}

		/// <inheritdoc />
		public sealed override void Dispose()
		{
			if (Interlocked.CompareExchange(ref _disposed, 1, 0) == 0) Dispose(true);
			GC.SuppressFinalize(this);
		}
		#endregion

		/// <inheritdoc />
		public bool Equals(T other) => AreEqual(other, Value);

		[SuppressMessage("Design", "CA1065:Do not raise exceptions in unexpected locations", Justification = "Nullable enable should protected this but have to be sure.")]
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
		/// <returns>The ObservableValue created.</returns>
		public static ObservableValue<T> Create<T>() => new ObservableValue<T>();

		/// <summary>
		/// Creates an ObservableValue.
		/// </summary>
		/// <typeparam name="T">The type of the value.</typeparam>
		/// <param name="initialValue">The initial value</param>
		/// <returns>The ObservableValue created.</returns>
		public static ObservableValue<T> Create<T>(T initialValue) => new ObservableValue<T>(initialValue);
	}
}
