using System.Diagnostics;
using System.Globalization;
using SlaeSolverSystem.Common;
using SlaeSolverSystem.Master.Enums;
using SlaeSolverSystem.Master.Network;

namespace SlaeSolverSystem.Master.Jobs;

public class SeidelJob : IJob
{
	private readonly GuiNotifier _notifier;
	private readonly string _matrixFile;
	private readonly string _vectorFile;
	private readonly double _epsilon;
	private readonly int _maxIterations;
	private readonly SeidelSolveMode _mode;
	private readonly string _jobName;

	public SeidelJob(GuiNotifier notifier, string matrixFile, string vectorFile, double epsilon, int maxIterations, SeidelSolveMode mode)
	{
		_notifier = notifier;
		_matrixFile = matrixFile;
		_vectorFile = vectorFile;
		_epsilon = epsilon;
		_maxIterations = maxIterations;
		_mode = mode;
		_jobName = $"Г-З ({_mode})";
	}

	public async Task ExecuteAsync()
	{
		try
		{
			await _notifier.SendLogAsync($"{_jobName}: Начало выполнения задания.");
			await _notifier.SendStatusAsync($"Чтение файлов ({_jobName})");

			var (A, b) = await ReadDataAsync();
			
			int size = b.Length;

			await _notifier.SendLogAsync($"{_jobName}: Данные для матрицы {size}x{size} успешно прочитаны.");
			await _notifier.SendStatusAsync($"Вычисление ({_jobName})");

			var calculator = new LocalCalculator(0, size, size, A, b);

			var stopwatch = Stopwatch.StartNew();
			double[] x = new double[size];
			int iteration = 0;
			double error;

			do
			{
				var x_prev = (double[])x.Clone();

				switch (_mode)
				{
					case SeidelSolveMode.SingleThread:
						x = calculator.CalculatePartSingleThread(x_prev);
						break;
					case SeidelSolveMode.MultiThreadWithPool:
						x = calculator.CalculatePartMultiThreadWithPool(x_prev);
						break;
					case SeidelSolveMode.MultiThreadWithoutPool:
						x = calculator.CalculatePartMultiThreadWithoutPool(x_prev);
						break;
					case SeidelSolveMode.MultiThreadAsync:
						x = await calculator.CalculatePartMultiThreadAsync(x_prev);
						break;
				}

				error = Math.Sqrt(x.Zip(x_prev, (val1, val2) => (val1 - val2) * (val1 - val2)).Sum());
				if (iteration % 10 == 0) 
					await _notifier.SendProgressAsync(iteration + 1, error);

				iteration++;

			} while (error > _epsilon && iteration < _maxIterations);

			stopwatch.Stop();
			long elapsedTime = stopwatch.ElapsedMilliseconds;
			await _notifier.SendProgressAsync(iteration, error);

			if (error > _epsilon)
				await _notifier.SendLogAsync($"{_jobName}: ПРЕВЫШЕНО МАКСИМАЛЬНОЕ КОЛИЧЕСТВО ИТЕРАЦИЙ.");

			await _notifier.SendLogAsync($"{_jobName}: Вычисления завершены за {elapsedTime} мс. Итераций: {iteration}.");

			await _notifier.NotifyDistributedResultAsync(elapsedTime, iteration, x, size);
			await _notifier.SendStatusAsync("Готов к работе");
		}
		catch (Exception ex)
		{
			Console.WriteLine($"[{_jobName}] КРИТИЧЕСКАЯ ОШИБКА: {ex.Message}\n{ex}");
			await _notifier.SendLogAsync($"ОШИБКА в тесте '{_jobName}': {ex.Message}");
			await _notifier.SendStatusAsync("Ошибка");
			await _notifier.NotifyCalculationFailedAsync();
		}
	}

	private async Task<(double[,] A, double[] b)> ReadDataAsync()
	{
		if (!File.Exists(_matrixFile)) throw new FileNotFoundException("Файл матрицы не найден!", _matrixFile);
		if (!File.Exists(_vectorFile)) throw new FileNotFoundException("Файл вектора не найден!", _vectorFile);

		var bLines = (await File.ReadAllLinesAsync(_vectorFile)).Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
		var b = bLines.Select(l => double.Parse(l.Trim(), CultureInfo.InvariantCulture)).ToArray();

		var matrixLines = (await File.ReadAllLinesAsync(_matrixFile)).Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
		int size = b.Length;
		if (matrixLines.Length != size) throw new InvalidDataException($"Размеры матрицы ({matrixLines.Length}) и вектора ({size}) не совпадают.");

		var A = new double[size, size];
		for (int i = 0; i < size; i++)
		{
			var rowElements = matrixLines[i].Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
			if (rowElements.Length != size) throw new InvalidDataException($"Количество элементов в строке {i + 1} матрицы ({rowElements.Length}) не совпадает с размером ({size}).");
			for (int j = 0; j < size; j++)
			{
				if (!double.TryParse(rowElements[j], CultureInfo.InvariantCulture, out A[i, j]))
					throw new InvalidDataException($"Не удалось прочитать элемент [{i + 1}, {j + 1}] матрицы.");
			}
		}
		return (A, b);
	}
}