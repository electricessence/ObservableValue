# ObservableValue

A simple synchronized observable (`IObservable<T>`) that when its value is updated (value changes), its observers are notified.

`System.Reactive` is included and can be used to extend its behavior.

## Usage

### Namespace

```cs
using Open.Observable;
```

### Creating

```cs
var value = new ObservableValue<int>(1 /* optional initial value */);
```

or

```cs
var value = ObservableValue.Create(1); // type inferred
```

### Subscribing

```cs
value.Subscribe(v => { /* do something with the value */ });
```

### Updating

```cs
var changed = value.Post(2);
```
