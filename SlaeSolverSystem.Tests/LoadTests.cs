using System.Diagnostics;
using SlaeSolverSystem.Common;
using SlaeSolverSystem.Common.Clients;
using SlaeSolverSystem.Common.Contracts;
using SlaeSolverSystem.Tests.Infrastructure;
using Xunit.Abstractions;

namespace SlaeSolverSystem.Tests;

public class LoadTests : IAsyncLifetime
{
	private readonly ITestOutputHelper _output;
	private readonly MasterApiClient _apiClient;
	private readonly string _testDir;

	public LoadTests(ITestOutputHelper output)
	{
		_output = output;
		_apiClient = new MasterApiClient("127.0.0.1", 8001);
		_testDir = Path.Combine(Path.GetTempPath(), "SlaeLoadTests", Guid.NewGuid().ToString());
		Directory.CreateDirectory(_testDir);
	}

	public Task InitializeAsync() => _apiClient.ConnectAsync();

	public Task DisposeAsync()
	{
		_apiClient.Disconnect();
		try { if (Directory.Exists(_testDir)) Directory.Delete(_testDir, true); } catch { }
		return Task.CompletedTask;
	}

	[Theory]
	[Trait("Category", "Load")]
	[InlineData(50, 2, 5)]
	[InlineData(100, 4, 10)]
	[InlineData(500, 4, 45)]
	[InlineData(2000, 6, 120)]
	[InlineData(5000, 8, 600)]
	public async Task DistributedSolve_VaryingSizes_CompletesWithinTimeout(int matrixSize, int workerCount, int timeoutInSeconds)
	{
		_output.WriteLine($"--- ЗАПУСК НАГРУЗОЧНОГО ТЕСТА: Матрица {matrixSize}x{matrixSize}, Воркеров: {workerCount}, Таймаут: {timeoutInSeconds} сек ---");

		// --- ARRANGE ---
		var matrixFile = Path.Combine(_testDir, $"matrix_{matrixSize}.txt");
		var vectorFile = Path.Combine(_testDir, $"vector_{matrixSize}.txt");
		var nodesFile = Path.Combine(_testDir, $"nodes_{workerCount}.txt");

		_output.WriteLine("Генерация тестовых данных...");
		await TestDataGenerator.GenerateSlaeFilesAsync(matrixSize, matrixFile, vectorFile);
		await TestDataGenerator.GenerateNodesFileAsync(nodesFile, workerCount);
		_output.WriteLine("Данные сгенерированы.");

		var tcs = new TaskCompletionSource<CalculationResult>();
		_apiClient.CalculationFinished += result => tcs.TrySetResult(result);

		var stopwatch = Stopwatch.StartNew();

		// --- ACT ---
		_output.WriteLine("Отправка команды на сервер...");
		await _apiClient.StartCalculationAsync(
			CommandCodes.StartDistributedCalculation,
			matrixFile,
			vectorFile,
			nodesFile,
			1e-9,
			20000
		);

		_output.WriteLine("Ожидание результата от сервера...");
		var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(timeoutInSeconds * 1000));
		stopwatch.Stop();

		// --- ASSERT ---
		if (completedTask != tcs.Task)
		{
			Assert.Fail($"!!! ТЕСТ ПРОВАЛЕН: Таймаут {timeoutInSeconds} сек. превышен !!!");
		}
		Assert.Same(tcs.Task, completedTask);

		var result = await tcs.Task;

		_output.WriteLine("--- РЕЗУЛЬТАТЫ ---");
		_output.WriteLine($"Общее время (клиент): {stopwatch.ElapsedMilliseconds} мс");
		_output.WriteLine($"Время вычислений (сервер): {result.ElapsedTime} мс");
		_output.WriteLine($"Количество итераций: {result.Iterations}");
		_output.WriteLine($"Размер матрицы: {result.MatrixSize}");
		_output.WriteLine("Тест успешно пройден.");

		Assert.True(result.ElapsedTime > 0, "Время вычислений на сервере должно быть положительным.");
		Assert.Equal(matrixSize, result.MatrixSize);
	}
}