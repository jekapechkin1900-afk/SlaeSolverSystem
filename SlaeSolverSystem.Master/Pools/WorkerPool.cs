using System.Collections.Concurrent;
using System.Net.Sockets;

namespace SlaeSolverSystem.Master.Pools;

public class WorkerPool : IWorkerPool
{
	private readonly ConcurrentQueue<TcpClient> _availableWorkers = new();

	public void Add(TcpClient worker)
	{
		Console.WriteLine($"[WorkerPool] Worker {worker.Client.RemoteEndPoint} добавлен в пул.");
		_availableWorkers.Enqueue(worker);
	}

	public async Task<List<TcpClient>> GetWorkersAsync(int count, CancellationToken cancellationToken)
	{
		var workers = new List<TcpClient>(count);
		while (workers.Count < count)
		{
			cancellationToken.ThrowIfCancellationRequested();

			if (_availableWorkers.TryDequeue(out var worker))
			{
				if (worker.Connected)
				{
					workers.Add(worker);
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
		return workers;
	}

	public void ReturnWorkers(IEnumerable<TcpClient> workers)
	{
		foreach (var worker in workers)
		{
			if (worker.Connected)
			{
				Add(worker);
			}
			else
			{
				worker.Close();
			}
		}
	}
}
