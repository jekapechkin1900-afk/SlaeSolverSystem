using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SlaeSolverSystem.Common;
using SlaeSolverSystem.Common.Enums;
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
		Console.WriteLine($"[MessageHandler] Задача принята. Диапазон строк: [{startRow} - {startRow + rowCount - 1}].");
		await _masterClient.SendMessageAsync(CommandCodes.TaskAccepted, []);
		Console.WriteLine("[MessageHandler] Подтверждение TaskAccepted отправлено.");
	}

	private async Task HandleIterationVectorAsync(byte[] payload)
	{
		Console.WriteLine("Worker: Обработка вектора итерации...");
		byte mode = payload[0];
		var vectorPayload = payload.Skip(1).ToArray();

		var fullX = NetworkHelper.ToDoubleArray(vectorPayload);
		Console.WriteLine($"[MessageHandler] Получен вектор x (размер: {fullX.Length}). Режим: {(SeidelSolveMode)mode}.");

		var stopwatch = Stopwatch.StartNew();
		double[] partialResult;

		var solveMode = (SeidelSolveMode)mode;

		switch (solveMode)
		{
			case SeidelSolveMode.SingleThread:
				partialResult = _workerTask.CalculatePartSingleThread(fullX);
				break;
			case SeidelSolveMode.MultiThreadWithPool:
				partialResult = _workerTask.CalculatePartMultiThreadWithPool(fullX);
				break;
			case SeidelSolveMode.MultiThreadWithoutPool:
				partialResult = _workerTask.CalculatePartMultiThreadWithoutPool(fullX);
				break;
			case SeidelSolveMode.MultiThreadAsync:
				partialResult = await _workerTask.CalculatePartMultiThreadAsync(fullX);
				break;
			default:
				Console.WriteLine($"[MessageHandler] ОШИБКА: Неизвестный режим: {mode}. Используется SingleThread.");
				partialResult = _workerTask.CalculatePartSingleThread(fullX);
				break;
		}

		stopwatch.Stop();

		int usedThreads = 1;
		if (solveMode != SeidelSolveMode.SingleThread)
		{
			usedThreads = Environment.ProcessorCount;
		}

		var resultPayload = new byte[4 + partialResult.Length * 8];

		Buffer.BlockCopy(BitConverter.GetBytes(usedThreads), 0, resultPayload, 0, 4);
		Buffer.BlockCopy(partialResult, 0, resultPayload, 4, partialResult.Length * 8);

		Console.WriteLine($"Worker: Отправляю {partialResult.Length} элементов и info о {usedThreads} потоках...");
		await _masterClient.SendMessageAsync(CommandCodes.PartialResult, resultPayload);
		Console.WriteLine("Worker: Результат отправлен.");
	}

	private void HandleReset()
	{
		_workerTask.Reset();
	}
}