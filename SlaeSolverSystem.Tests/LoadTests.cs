using System.Diagnostics;
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

	public async Task InitializeAsync()
	{
		await _apiClient.ConnectAsync();
	}

	public Task DisposeAsync()
	{
		_apiClient.Disconnect();
		try { Directory.Delete(_testDir, true); } catch { }
		return Task.CompletedTask;
	}

	[Theory]
	[Trait("Category", "Load")]
	[InlineData(50, 2, 5)]
	[InlineData(100, 4, 10)]
	[InlineData(500, 4, 30)]
	[InlineData(1000, 4, 90)]
	[InlineData(5000, 8, 600)] 
	public async Task DistributedSolve_VaryingSizes_CompletesWithinTimeout(int matrixSize, int workerCount, int timeoutInSeconds)
	{
		_output.WriteLine($"--- ЗАПУСК ТЕСТА: Матрица {matrixSize}x{matrixSize}, Воркеров: {workerCount}, Таймаут: {timeoutInSeconds} сек ---");

		// --- ARRANGE ---
		var matrixFile = Path.Combine(_testDir, $"matrix_{matrixSize}.txt");
		var vectorFile = Path.Combine(_testDir, $"vector_{matrixSize}.txt");
		var nodesFile = Path.Combine(_testDir, $"nodes_{workerCount}.txt");

		_output.WriteLine("Генерация тестовых данных...");
		await TestDataGenerator.GenerateSlaeFilesAsync(matrixSize, matrixFile, vectorFile);
		await TestDataGenerator.GenerateNodesFileAsync(nodesFile, workerCount);
		_output.WriteLine("Данные сгенерированы.");

		var tcs = new TaskCompletionSource<CalculationResult>();
		_apiClient.CalculationFinished += (result) => tcs.TrySetResult(result);

		var stopwatch = Stopwatch.StartNew();

		// --- ACT ---
		_output.WriteLine("Отправка команды на сервер...");
		await _apiClient.StartDistributedCalculationAsync(
			matrixFile,
			vectorFile,
			nodesFile,
			1e-9,
			20000 
		);

		var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(timeoutInSeconds * 1000));
		stopwatch.Stop();

		// --- ASSERT ---
		Assert.Same(tcs.Task, completedTask);
		if (completedTask != tcs.Task)
		{
			_output.WriteLine($"!!! ТЕСТ ПРОВАЛЕН: Таймаут {timeoutInSeconds} сек. превышен !!!");
			return; 
		}

		var result = await tcs.Task;

		_output.WriteLine("--- РЕЗУЛЬТАТЫ ---");
		_output.WriteLine($"Общее время (клиент): {stopwatch.ElapsedMilliseconds} мс");
		_output.WriteLine($"Время вычислений (сервер): {result.ElapsedTime} мс");
		_output.WriteLine($"Количество итераций: {result.Iterations}");
		_output.WriteLine($"Размер матрицы: {result.MatrixSize}");

		Assert.True(result.ElapsedTime > 0, "Время вычислений на сервере должно быть положительным.");
		Assert.Equal(matrixSize, result.MatrixSize);
	}
}
