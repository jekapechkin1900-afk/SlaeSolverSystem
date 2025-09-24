using System.Globalization;
using SlaeSolverSystem.Common;
using SlaeSolverSystem.Master.Network;

namespace SlaeSolverSystem.Master.Jobs;

public class LinearJob(GuiNotifier notifier, string matrixFile, string vectorFile) : IJob
{
	private readonly GuiNotifier _notifier = notifier;
	private readonly string _matrixFile = matrixFile;
	private readonly string _vectorFile = vectorFile;

	public async Task ExecuteAsync()
	{
		try
		{
			await _notifier.SendLogAsync("Линейный тест: Начало выполнения задания.");
			await _notifier.SendStatusAsync("Чтение файлов (линейный)");

			if (!File.Exists(_matrixFile)) throw new FileNotFoundException("Файл матрицы не найден!", _matrixFile);
			if (!File.Exists(_vectorFile)) throw new FileNotFoundException("Файл вектора не найден!", _vectorFile);

			var bLines = (await File.ReadAllLinesAsync(_vectorFile))
						 .Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
			var b = bLines.Select(l => double.Parse(l.Trim(), CultureInfo.InvariantCulture)).ToArray();

			var matrixLines = (await File.ReadAllLinesAsync(_matrixFile))
							  .Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
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

			await _notifier.SendLogAsync($"Линейный тест: Данные для матрицы {size}x{size} успешно прочитаны.");

			await _notifier.SendStatusAsync("Вычисление (метод Гаусса)");
			var result = await LinearSlaeSolver.SolveAsync(A, b);

			await _notifier.SendLogAsync($"Линейный тест: Вычисления завершены за {result.ElapsedMilliseconds} мс.");

			await _notifier.NotifyLinearResultAsync(result.ElapsedMilliseconds, -1, result.MatrixSize); 
			await _notifier.SendLogAsync("Линей-ный тест: Результат успешно отправлен.");
			await _notifier.SendStatusAsync("Готов к работе");
		}
		catch (Exception ex)
		{
			Console.WriteLine($"[LinearJob] КРИТИЧЕСКАЯ ОШИБКА: {ex.Message}\n{ex}");
			await _notifier.SendLogAsync($"ОШИБКА в линейном тесте: {ex.Message}");
			await _notifier.SendStatusAsync("Ошибка");
			await _notifier.NotifyCalculationFailedAsync();
		}
	}
}
