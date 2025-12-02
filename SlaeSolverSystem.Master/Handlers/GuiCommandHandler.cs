using System.Net.Sockets;
using SlaeSolverSystem.Common;
using SlaeSolverSystem.Common.Enums;
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
				case CommandCodes.StartSeidelMultiThreadNoPool:
				case CommandCodes.StartSeidelMultiThreadPool:
				case CommandCodes.StartSeidelMultiThreadAsync:
					var mode = CommandToSeidelMode(command);
					if (p.isDistributed)
					{
						job = new DistributedJob(mode, _notifier, _jobManager.WorkerPool, p.matrixFile, p.vectorFile, p.nodesFile, p.epsilon, p.maxIterations);
					}
					else
					{
						job = new SeidelJob(_notifier, p.matrixFile, p.vectorFile, p.epsilon, p.maxIterations, mode);
					}
					break;
				default:
					await _notifier.SendLogAsync($"Получена неизвестная команда: 0x{command:X2}");
					break;
			}

			if (job != null)
			{
				_jobManager.EnqueueJob(job);
				await _notifier.SendLogAsync($"Задание '{GetJobName(p.isDistributed, command)}' добавлено в очередь.");
			}
		}
		catch (Exception ex)
		{
			await _notifier.SendLogAsync($"Ошибка обработки команды от GUI: {ex.Message}");
			await _notifier.NotifyCalculationFailedAsync();
		}
	}

	private SeidelSolveMode CommandToSeidelMode(byte command) => command switch
	{
		CommandCodes.StartSeidelLinear => SeidelSolveMode.SingleThread,
		CommandCodes.StartSeidelMultiThreadPool => SeidelSolveMode.MultiThreadWithPool,
		CommandCodes.StartSeidelMultiThreadNoPool => SeidelSolveMode.MultiThreadWithoutPool,
		CommandCodes.StartSeidelMultiThreadAsync => SeidelSolveMode.MultiThreadAsync,
		_ => throw new ArgumentOutOfRangeException(nameof(command), "Неизвестная команда для метода Гаусса-Зейделя")
	};

	private string GetJobName(bool isDistributed, byte command)
	{
		if (command == CommandCodes.StartGaussLinear) return "Гаусс (линейный)";
		string prefix = isDistributed ? "Распределенный " : "Локальный ";
		return prefix + CommandToSeidelMode(command).ToString();
	}

	private (string matrixFile, string vectorFile, string nodesFile, double epsilon, int maxIterations, bool isDistributed) ParseStartParameters(byte[] payload)
	{
		using var reader = new BinaryReader(new MemoryStream(payload));
		bool isDistributed = reader.ReadBoolean(); 
		string matrixFile = reader.ReadString();   
		string vectorFile = reader.ReadString();   
		string nodesFile = reader.ReadString();    
		double epsilon = reader.ReadDouble();      
		int maxIterations = reader.ReadInt32();  

		return (matrixFile, vectorFile, nodesFile, epsilon, maxIterations, isDistributed);
	}
}
