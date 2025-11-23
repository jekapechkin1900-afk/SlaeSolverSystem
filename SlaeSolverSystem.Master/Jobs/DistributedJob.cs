using System.Diagnostics;
using System.Globalization;
using System.Net.Sockets;
using SlaeSolverSystem.Common;
using SlaeSolverSystem.Master.Network;
using SlaeSolverSystem.Master.Pools;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace SlaeSolverSystem.Master.Jobs;

public class DistributedJob(GuiNotifier notifier, IWorkerPool workerPool, string matrixFile, string vectorFile, string nodesFile, double epsilon, int maxIterations) : IJob
{
	private readonly GuiNotifier _notifier = notifier;
	private readonly IWorkerPool _workerPool = workerPool;
	private readonly string _matrixFile = matrixFile;
	private readonly string _vectorFile = vectorFile;
	private readonly string _nodesFile = nodesFile;
	private readonly double _epsilon = epsilon;
	private readonly int _maxIterations = maxIterations;

	public async Task ExecuteAsync()
	{
		var activeWorkers = new List<TcpClient>();
		try
		{
			await _notifier.SendLogAsync("Распределенный тест: Начало выполнения задания.");
			await _notifier.SendStatusAsync("Чтение файлов");

			// --- ЭТАП 1: ЧТЕНИЕ ДАННЫХ ---
			var workerIps = (await File.ReadAllLinesAsync(_nodesFile)).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
			await _notifier.SendLogAsync($"Распределенный тест: Чтение файла узлов: '{_nodesFile}'. Найдено {workerIps.Count} адресов.");
			
			var bLines = (await File.ReadAllLinesAsync(_vectorFile)).Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
			var b = bLines.Select(l => double.Parse(l.Trim(), CultureInfo.InvariantCulture)).ToArray();
			var matrixLines = (await File.ReadAllLinesAsync(_matrixFile)).Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
			int size = b.Length;
			await _notifier.SendLogAsync($"Распределенный тест: Данные для матрицы {size}x{size} успешно прочитаны.");

			if (matrixLines.Length != size) throw new InvalidDataException("Размеры матрицы и вектора не совпадают.");
			var A = new double[size, size];
			for (int i = 0; i < size; i++)
			{
				var rowElements = matrixLines[i].Split([' '], StringSplitOptions.RemoveEmptyEntries);
				for (int j = 0; j < size; j++) A[i, j] = double.Parse(rowElements[j], CultureInfo.InvariantCulture);
			}

			// --- ЭТАП 2: ПОЛУЧЕНИЕ WORKER'ОВ ИЗ ПУЛА ---
			await _notifier.SendLogAsync($"Распределенный тест: Ожидание {workerIps.Count} Worker'ов из пула...");
			await _notifier.SendStatusAsync("Подключение узлов");

			using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
			activeWorkers = await _workerPool.GetWorkersAsync(workerIps.Count, cts.Token);
			await _notifier.SendLogAsync($"Распределенный тест: {activeWorkers.Count} воркеров успешно зарезервировано в пуле.");

			// --- ЭТАП 3: РАСПРЕДЕЛЕНИЕ ЗАДАЧ ---
			await _notifier.SendLogAsync("Этап 3: Распределение задач по Worker'ам...");
			await _notifier.SendStatusAsync("Распределение задач");

			var distributionTasks = new List<Task>();
			int rowsPerWorker = size / activeWorkers.Count;
			int extraRows = size % activeWorkers.Count;
			int currentRow = 0;

			for (int i = 0; i < activeWorkers.Count; i++)
			{
				int rowsForThisWorker = rowsPerWorker + (i < extraRows ? 1 : 0);
				if (rowsForThisWorker == 0) continue;
				distributionTasks.Add(DistributeAndConfirmAsync(activeWorkers[i], A, b, currentRow, rowsForThisWorker, size));
				currentRow += rowsForThisWorker;
			}
			await Task.WhenAll(distributionTasks);
			await _notifier.SendLogAsync("Все Worker'ы приняли задачи.");

			// --- ЭТАП 4: ИТЕРАЦИОННЫЙ ПРОЦЕСС ---
			await _notifier.SendStatusAsync("Вычисление");
			var stopwatch = Stopwatch.StartNew();
			double[] x = new double[size];
			double[] x_old = new double[size];
			int iteration = 0;
			await _notifier.SendLogAsync($"Итерационный процесс начат. Epsilon={_epsilon}, MaxIter={_maxIterations}.");

			while (iteration < _maxIterations)
			{
				Array.Copy(x, x_old, size);

				var sendTasks = activeWorkers.Select(w => NetworkHelper.SendMessageAsync(w.GetStream(), CommandCodes.IterationVector, NetworkHelper.ToBytes(x))).ToList();
				await Task.WhenAll(sendTasks);

				var receiveTasks = activeWorkers.Select(w => NetworkHelper.ReadMessageAsync(w.GetStream())).ToList();
				var results = await Task.WhenAll(receiveTasks);

				currentRow = 0;
				for (int i = 0; i < activeWorkers.Count; i++)
				{
					int rowsForThisWorker = rowsPerWorker + (i < extraRows ? 1 : 0);
					if (rowsForThisWorker == 0) continue;
					var partialResult = NetworkHelper.ToDoubleArray(results[i].Payload);
					Array.Copy(partialResult, 0, x, currentRow, partialResult.Length);
					currentRow += rowsForThisWorker;
				}

				double error = Math.Sqrt(x.Zip(x_old, (a, b) => (a - b) * (a - b)).Sum());
				await _notifier.SendProgressAsync(iteration + 1, error);

				if (error < _epsilon)
				{
					stopwatch.Stop();
					await _notifier.SendLogAsync($"РЕШЕНИЕ НАЙДЕНО. Сходимость достигнута за {iteration + 1} итераций.");
					await _notifier.SendLogAsync($"Общее время вычислений: {stopwatch.ElapsedMilliseconds} мс.");
					await _notifier.NotifyDistributedResultAsync(stopwatch.ElapsedMilliseconds, iteration + 1, x, size);
					break;
				}
				
				await _notifier.SendLogAsync($"Итерационный процесс завершен. Финальная ошибка: {error:E5}.");
				iteration++;
			}

			if (iteration >= _maxIterations)
				await _notifier.SendLogAsync("ПРЕВЫШЕНО МАКСИМАЛЬНОЕ КОЛИЧЕСТВО ИТЕРАЦИЙ.");

			// --- ЭТАП 5: ЗАВЕРШЕНИЕ ---
			var resetTasks = activeWorkers.Select(w => NetworkHelper.SendMessageAsync(w.GetStream(), CommandCodes.Reset, [])).ToList();
			await Task.WhenAll(resetTasks);
			await _notifier.SendStatusAsync("Завершено");
		}
		catch (Exception ex)
		{
			Console.WriteLine($"[DistributedJob] КРИТИЧЕСКАЯ ОШИБКА: {ex.Message}\n{ex}");
			await _notifier.SendLogAsync($"ОШИБКА в распределенном тесте: {ex.Message}");
			await _notifier.SendStatusAsync("Ошибка");
			await _notifier.NotifyCalculationFailedAsync();
		}
		finally
		{
			if (activeWorkers.Count != 0)
			{
				_workerPool.ReturnWorkers(activeWorkers);
				await _notifier.SendLogAsync($"Worker'ы ({activeWorkers.Count} шт.) возвращены в пул. Доступно: {_workerPool.AvailableCount}.");
			}
		}
	}

	private async Task DistributeAndConfirmAsync(TcpClient worker, double[,] A, double[] b, int startRow, int rowCount, int matrixSize)
	{
		using var ms = new MemoryStream();
		using var writer = new BinaryWriter(ms);
		writer.Write(startRow);
		writer.Write(rowCount);
		writer.Write(matrixSize);
		for (int i = startRow; i < startRow + rowCount; i++)
		{
			for (int j = 0; j < matrixSize; j++) writer.Write(A[i, j]);
			writer.Write(b[i]);
		}
		await NetworkHelper.SendMessageAsync(worker.GetStream(), CommandCodes.SetTask, ms.ToArray());
		var (cmd, _) = await NetworkHelper.ReadMessageAsync(worker.GetStream());
		if (cmd != CommandCodes.TaskAccepted)
			throw new Exception($"Worker {worker.Client.RemoteEndPoint} не подтвердил получение задачи.");
	}
}