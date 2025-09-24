using SlaeSolverSystem.Common;
using SlaeSolverSystem.Worker.Interfaces;

namespace SlaeSolverSystem.Worker.Core;

public class MessageHandler(IMasterClient masterClient, IWorkerTask workerTask)
{
	private readonly IMasterClient _masterClient = masterClient;
	private readonly IWorkerTask _workerTask = workerTask;

	public async Task HandleMessageAsync(byte command, byte[] payload)
	{
		Console.WriteLine($"Worker: Получена команда: 0x{command:X2}");
		switch (command)
		{
			case CommandCodes.SetTask:
				await HandleSetTaskAsync(payload);
				break;
			case CommandCodes.IterationVector:
				await HandleIterationVectorAsync(payload);
				break;
			case CommandCodes.Reset:
				HandleReset();
				break;
			default:
				Console.WriteLine($"Worker: Получена неизвестная команда: 0x{command:X2}");
				break;
		}
	}

	private async Task HandleSetTaskAsync(byte[] payload)
	{
		Console.WriteLine($"Worker: Обработка задачи SetTask. Payload: {payload.Length} байт.");
		using var reader = new BinaryReader(new MemoryStream(payload));

		var startRow = reader.ReadInt32();
		var rowCount = reader.ReadInt32();
		var matrixSize = reader.ReadInt32();

		var localMatrix = new double[rowCount, matrixSize];
		var localB = new double[rowCount];

		for (int i = 0; i < rowCount; i++)
		{
			for (int j = 0; j < matrixSize; j++)
				localMatrix[i, j] = reader.ReadDouble();
			localB[i] = reader.ReadDouble();
		}

		_workerTask.SetData(startRow, rowCount, matrixSize, localMatrix, localB);

		await _masterClient.SendMessageAsync(CommandCodes.TaskAccepted, []);
		Console.WriteLine("Worker: Подтверждение TaskAccepted отправлено.");
	}

	private async Task HandleIterationVectorAsync(byte[] payload)
	{
		Console.WriteLine("Worker: Обработка вектора итерации...");
		var fullX = NetworkHelper.ToDoubleArray(payload);
		Console.WriteLine($"Worker: Вектор x получен, размер: {fullX.Length}. Начинаю вычисления...");

		var partialResult = _workerTask.CalculatePart(fullX);

		Console.WriteLine($"Worker: Вычисления завершены. Отправляю {partialResult.Length} элементов результата...");
		await _masterClient.SendMessageAsync(CommandCodes.PartialResult, NetworkHelper.ToBytes(partialResult));
		Console.WriteLine("Worker: Частичный результат отправлен Master'у.");
	}

	private void HandleReset()
	{
		_workerTask.Reset();
	}
}
