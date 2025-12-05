using System.Collections.Concurrent;
using System.Net.Sockets;

namespace SlaeSolverSystem.Master.Pools;

public class WorkerPool : IWorkerPool
{
	private readonly List<TcpClient> _workers = new();
	private readonly object _lock = new();

	public event Action<int, int> PoolStateChanged;
	public int AvailableCount { get { lock (_lock) return _workers.Count(IsConnected); } }
	public int TotalCount { get { lock (_lock) return _workers.Count; } }

	public WorkerPool()
	{
		Task.Run(MonitorWorkersAsync);
	}

	private async Task MonitorWorkersAsync()
	{
		while (true)
		{
			bool stateChanged = false;
			lock (_lock)
			{
				int removedCount = _workers.RemoveAll(w =>
				{
					bool alive = IsConnected(w);
					if (!alive)
					{
						try { Console.WriteLine($"[Monitor] Обнаружен отключенный Worker {w.Client.RemoteEndPoint}. Удаляю."); w.Close(); } catch { }
					}
					return !alive;
				});

				if (removedCount > 0) stateChanged = true;
			}

			if (stateChanged)
			{
				NotifyStateChanged();
			}

			await Task.Delay(1000); 
		}
	}

	private void NotifyStateChanged()
	{
		int avail, total;
		lock (_lock)
		{
			avail = _workers.Count(IsConnected);
			total = _workers.Count;
		}
		Task.Run(() => PoolStateChanged?.Invoke(avail, total));
	}

	public void Add(TcpClient worker)
	{
		lock (_lock)
		{
			_workers.Add(worker);
			Console.WriteLine($"[WorkerPool] Worker {worker.Client.RemoteEndPoint} добавлен. Всего: {_workers.Count}.");
		}
		NotifyStateChanged();
	}

	public async Task<List<TcpClient>> GetWorkersAsync(int count, CancellationToken cancellationToken)
	{
		try
		{
			Console.WriteLine($"[WorkerPool] Запрос на {count} воркеров...");
			var result = new List<TcpClient>();

			while (true)
			{
				cancellationToken.ThrowIfCancellationRequested();

				lock (_lock)
				{
					_workers.RemoveAll(w => !IsConnected(w));

					if (_workers.Count >= count)
					{
						for (int i = 0; i < count; i++)
						{
							result.Add(_workers[0]);
							_workers.RemoveAt(0);
						}
						Console.WriteLine($"[WorkerPool] Выдано {count} воркеров. Осталось: {_workers.Count}.");
						break; 
					}
				}

				await Task.Delay(200, cancellationToken);
			}

			NotifyStateChanged(); 
			return result;

		}
		catch (TaskCanceledException)
		{
			throw new TimeoutException("Время ожидания воркеров истекло.");
		}
	}

	public void ReturnWorkers(IEnumerable<TcpClient> workers)
	{
		lock (_lock)
		{
			foreach (var worker in workers)
			{
				if (IsConnected(worker))
				{
					_workers.Add(worker);
				}
				else
				{
					Console.WriteLine($"[WorkerPool] Worker {worker.Client.RemoteEndPoint} отключен и не возвращен в пул.");
					worker.Close();
				}
			}
			Console.WriteLine($"[WorkerPool] Воркеры возвращены. Всего: {_workers.Count}.");
		}
		NotifyStateChanged();
	}

	private bool IsConnected(TcpClient client)
	{
		try
		{
			if (client == null || !client.Connected) return false;

			if (client.Client.Poll(0, SelectMode.SelectRead))
			{
				byte[] buff = new byte[1];
				if (client.Client.Receive(buff, SocketFlags.Peek) == 0)
				{
					return false;
				}
			}
			return true;
		}
		catch
		{
			return false;
		}
	}

	public List<TcpClient> GetAllAvailableWorkers()
	{
		lock (_lock)
		{
			_workers.RemoveAll(w => !IsConnected(w));

			var allWorkers = new List<TcpClient>(_workers);
			_workers.Clear(); 

			Console.WriteLine($"[WorkerPool] Выдано всех доступных воркеров: {allWorkers.Count}.");
			NotifyStateChanged();
			return allWorkers;
		}
	}
}
