using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;


namespace Plugin.BLE.Windows;

/// <summary>
/// Thread safe Dictionary mimicking the Concurrent API without the need or complexity of concurrency.
/// Value creation unlike ConcurrentDictionary is async and should be locked.
/// </summary>
/// <typeparam name="TKey"></typeparam>
/// <typeparam name="TValue"></typeparam>
public class SafeCreateDictionary<TKey, TValue>
{
	private readonly SemaphoreSlim _semaphore = new(1, 1);
	private readonly Dictionary<TKey, TValue> _dictionary = [];

	public IEnumerable<TValue> Values => _dictionary.Values.ToList();

	public TValue GetOrAdd(TKey key, Func<TValue> valueCreator)
	{
		_semaphore.Wait();
		try { return _dictionary.TryGetValue(key, out TValue value) ? value : _dictionary[key] = valueCreator.Invoke(); }
		finally { _semaphore.Release(); }
	}

	public bool TryGetValue(TKey key, out TValue value)
	{
		_semaphore.Wait();
		try { return _dictionary.TryGetValue(key, out value); }
		finally { _semaphore.Release(); }
	}

	// This method remains async cuz the updater contains an async method to verify availability of the underlying device.
	// A device can be Added to the Master collection either by the BluetoothLeDevice Manager from the OS unsolicited,
	// generated upon device discovery or recreated by the user application where it holds a reference to the IDevice but the underlying device had already been purged by the OS.
	public async Task<TValue> AddOrUpdate(TKey key, Func<TValue> valueCreator, Func<TValue, Task<TValue>> valueUpdater)
	{
		await _semaphore.WaitAsync().ConfigureAwait(false);
		try { return _dictionary.TryGetValue(key, out TValue value) ? _dictionary[key] = await valueUpdater(value).ConfigureAwait(false) : _dictionary[key] = valueCreator.Invoke(); }
		finally { _semaphore.Release(); }
	}

	public bool TryRemove(TKey key, out TValue value)
	{
		_semaphore.Wait();
		try { return _dictionary.TryGetValue(key, out value) && _dictionary.Remove(key); }
		finally { _semaphore.Release(); }
	}
}
