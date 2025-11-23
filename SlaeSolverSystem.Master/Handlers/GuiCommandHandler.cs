using System.Net.Sockets;
using SlaeSolverSystem.Common;
using SlaeSolverSystem.Master.Enums;
using SlaeSolverSystem.Master.Jobs;
using SlaeSolverSystem.Master.Network;

namespace SlaeSolverSystem.Master.Handlers;

public class GuiCommandHandler
{
	private readonly IJobManager _jobManager;
	private TcpClient _guiClient;
	private GuiNotifier _notifier;

	public GuiCommandHandler(IJobManager jobManager)
	{
		_jobManager = jobManager;
	}

	public void HandleClient(TcpClient client)
	{
		_guiClient?.Close();

		_guiClient = client;
		_notifier = new GuiNotifier(_guiClient);
		Console.WriteLine("[GuiCommandHandler] Новый GUI-клиент принят в обработку.");
		_ = _notifier.SendLogAsync("GUI клиент успешно подключен.");

		Task.Run(ListenForCommandsAsync);
	}

	private async Task ListenForCommandsAsync()
	{
		try
		{
			while (_guiClient.Connected)
			{
				var (command, payload) = await NetworkHelper.ReadMessageAsync(_guiClient.GetStream());
				Console.WriteLine($"[GuiCommandHandler] Получена команда: 0x{command:X2}");

				await ProcessCommandAsync(command, payload);
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine($"[GuiCommandHandler] Соединение с GUI клиентом потеряно: {ex.Message}");
		}
		finally
		{
			_guiClient?.Close();
		}
	}

	private async Task ProcessCommandAsync(byte command, byte[] payload)
	{
		try
		{
			if (command == CommandCodes.RequestPoolState)
			{
				await _notifier.SendPoolStateAsync(_jobManager.WorkerPool.AvailableCount, _jobManager.WorkerPool.TotalCount);
				return; 
			}

			var p = ParseStartParameters(payload);
			IJob job = null;

			switch (command)
			{
				case CommandCodes.StartGaussLinear:
					job = new LinearJob(_notifier, p.matrixFile, p.vectorFile);
					break;
				case CommandCodes.StartSeidelLinear:
					job = new SeidelJob(_notifier, p.matrixFile, p.vectorFile, p.epsilon, p.maxIterations, SeidelSolveMode.SingleThread);
					break;
				case CommandCodes.StartSeidelMultiThreadNoPool:
					job = new SeidelJob(_notifier, p.matrixFile, p.vectorFile, p.epsilon, p.maxIterations, SeidelSolveMode.MultiThreadWithoutPool);
					break;
				case CommandCodes.StartSeidelMultiThreadPool:
					job = new SeidelJob(_notifier, p.matrixFile, p.vectorFile, p.epsilon, p.maxIterations, SeidelSolveMode.MultiThreadWithPool);
					break;
				case CommandCodes.StartSeidelMultiThreadAsync:
					job = new SeidelJob(_notifier, p.matrixFile, p.vectorFile, p.epsilon, p.maxIterations, SeidelSolveMode.MultiThreadAsync);
					break;
				case CommandCodes.StartDistributedCalculation:
					job = new DistributedJob(_notifier, _jobManager.WorkerPool, p.matrixFile, p.vectorFile, p.nodesFile, p.epsilon, p.maxIterations);
					break;
				default:
					await _notifier.SendLogAsync($"Получена неизвестная команда: 0x{command:X2}");
					break;
			}

			if (job != null)
			{
				_jobManager.EnqueueJob(job);
				await _notifier.SendLogAsync($"Задание '{job.GetType().Name}' (режим: {GetJobModeName(command)}) добавлено в очередь.");
			}
		}
		catch (Exception ex)
		{
			await _notifier.SendLogAsync($"Ошибка обработки команды от GUI: {ex.Message}");
			await _notifier.NotifyCalculationFailedAsync();
		}
	}

	private string GetJobModeName(byte command) => command switch
	{
		CommandCodes.StartGaussLinear => "Гаусс (линейный)",
		CommandCodes.StartSeidelLinear => "Г-З (1-поток)",
		CommandCodes.StartSeidelMultiThreadNoPool => "Г-З (Threads)",
		CommandCodes.StartSeidelMultiThreadPool => "Г-З (ThreadPool)",
		CommandCodes.StartSeidelMultiThreadAsync => "Г-З (Async)",
		CommandCodes.StartDistributedCalculation => "Распределенный",
		_ => "Неизвестный"
	};

	private (string matrixFile, string vectorFile, string nodesFile, double epsilon, int maxIterations) ParseStartParameters(byte[] payload)
	{
		using var reader = new BinaryReader(new MemoryStream(payload));
		return (reader.ReadString(), reader.ReadString(), reader.ReadString(), reader.ReadDouble(), reader.ReadInt32());
	}
}
