using System;
using Xunit;

namespace Open.Observable.Tests
{
	public class Tests
	{
		[Fact]
		public void PatternTest()
		{
			var value = ObservableValue.Create(1);
			Assert.Equal(1, value);
			var n = 0;
			var obs = value.Subscribe(v => n = v);
			Assert.Equal(1, n);
			value.Post(2);
			Assert.Equal(2, n);
			Assert.Equal(2, value);
			obs.Dispose();
			value.Post(3);
			Assert.Equal(2, n);
			Assert.Equal(3, value);
			value.Dispose();
			Assert.Throws<ObjectDisposedException>(() => value.Post(4));
		}
	}
}
