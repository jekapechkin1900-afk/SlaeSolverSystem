using System.Collections.Concurrent;
using System.Net.Sockets;

namespace SlaeSolverSystem.Master.Pools;

public class WorkerPool : IWorkerPool
{
	private readonly ConcurrentQueue<TcpClient> _workers = new();

	public int AvailableCount => _workers.Count(w => w.Connected);
	public int TotalCount => _workers.Count;

	public void Add(TcpClient worker)
	{
		_workers.Enqueue(worker);
		Console.WriteLine($"[WorkerPool] Worker {worker.Client.RemoteEndPoint} добавлен в пул. Всего в пуле: {_workers.Count}.");
	}

	public async Task<List<TcpClient>> GetWorkersAsync(int count, CancellationToken cancellationToken)
	{
		Console.WriteLine($"[WorkerPool] Попытка получить {count} воркеров. Доступно: {AvailableCount}.");
		var requestedWorkers = new List<TcpClient>(count);
		var deadline = DateTime.UtcNow.Add(cancellationToken.CanBeCanceled ? (cancellationToken.WaitHandle.WaitOne(0) ? TimeSpan.Zero : TimeSpan.FromMilliseconds(-1)) : TimeSpan.FromMilliseconds(-1));

		while (requestedWorkers.Count < count)
		{
			cancellationToken.ThrowIfCancellationRequested();

			if (_workers.TryDequeue(out var worker))
			{
				if (IsConnected(worker))
				{
					requestedWorkers.Add(worker);
					Console.WriteLine($"[WorkerPool] Воркер {worker.Client.RemoteEndPoint} выдан. Осталось получить: {count - requestedWorkers.Count}.");
				}
				else
				{
					Console.WriteLine($"[WorkerPool] Обнаружен отключенный Worker {worker.Client.RemoteEndPoint}. Удален из пула.");
					worker.Close();
				}
			}
			else
			{
				await Task.Delay(200, cancellationToken);
			}
		}
		return requestedWorkers;
	}

	public void ReturnWorkers(IEnumerable<TcpClient> workers)
	{
		foreach (var worker in workers)
		{
			if (IsConnected(worker))
			{
				_workers.Enqueue(worker);
			}
			else
			{
				Console.WriteLine($"[WorkerPool] Отключенный воркер {worker.Client.RemoteEndPoint} не возвращен в пул.");
				worker.Close();
			}
		}
		Console.WriteLine($"[WorkerPool] Воркеры возвращены. Доступно: {AvailableCount}.");
	}

	public void PruneDisconnected()
	{
		int initialCount = _workers.Count;
		var connectedWorkers = new ConcurrentQueue<TcpClient>();
		while (_workers.TryDequeue(out var worker))
		{
			if (IsConnected(worker))
			{
				connectedWorkers.Enqueue(worker);
			}
			else
			{
				worker.Close();
			}
		}
	}

	private bool IsConnected(TcpClient client)
	{
		try
		{
			return !(client.Client.Poll(1, SelectMode.SelectRead) && client.Client.Available == 0);
		}
		catch (SocketException) { return false; }
	}
}
