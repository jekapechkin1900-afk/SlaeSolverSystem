using System.Collections.Concurrent;
using SlaeSolverSystem.Master.Pools;

namespace SlaeSolverSystem.Master.Jobs;

public class JobManager : IJobManager
{
	private readonly ConcurrentQueue<IJob> _jobQueue = new();
	private readonly CancellationTokenSource _cancellationTokenSource = new();
	private Task _processingTask;

	public IWorkerPool WorkerPool { get; }

	public JobManager(IWorkerPool workerPool)
	{
		WorkerPool = workerPool;
	}

	public void EnqueueJob(IJob job)
	{
		_jobQueue.Enqueue(job);
		Console.WriteLine($"[JobManager] Новое задание типа '{job.GetType().Name}' добавлено в очередь. Всего в очереди: {_jobQueue.Count}.");
	}

	public void StartProcessing()
	{
		if (_processingTask != null)
		{
			Console.WriteLine("[JobManager] Обработка очереди уже запущена.");
			return;
		}

		Console.WriteLine("[JobManager] Запуск обработки очереди заданий...");
		_processingTask = Task.Run(ProcessQueueAsync, _cancellationTokenSource.Token);
	}

	public void StopProcessing()
	{
		Console.WriteLine("[JobManager] Остановка обработки очереди...");
		_cancellationTokenSource.Cancel();
	}

	private async Task ProcessQueueAsync()
	{
		var token = _cancellationTokenSource.Token;
		while (!token.IsCancellationRequested)
		{
			if (_jobQueue.TryDequeue(out IJob job))
			{
				Console.WriteLine($"[JobManager] Взято из очереди задание '{job.GetType().Name}'. Начинаю выполнение...");
				try
				{
					await job.ExecuteAsync();
					Console.WriteLine($"[JobManager] Задание '{job.GetType().Name}' успешно завершено.");
				}
				catch (Exception ex)
				{
					Console.WriteLine($"[JobManager] КРИТИЧЕСКАЯ ОШИБКА при выполнении задания '{job.GetType().Name}': {ex.Message}");
				}
			}
			else
			{
				await Task.Delay(500, token);
			}
		}
		Console.WriteLine("[JobManager] Обработка очереди остановлена.");
	}
}