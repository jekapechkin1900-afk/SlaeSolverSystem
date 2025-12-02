using SlaeSolverSystem.Common;
using SlaeSolverSystem.Common.Clients;
using SlaeSolverSystem.Common.Contracts;
using SlaeSolverSystem.Tests.Infrastructure;
using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace SlaeSolverSystem.Tests
{
	public class ScalabilityTests : IAsyncLifetime
	{
		private readonly ITestOutputHelper _output;
		private readonly ProcessManager _processManager;
		private readonly MasterApiClient _apiClient;
		private readonly string _testDir;

		public ScalabilityTests(ITestOutputHelper output)
		{
			_output = output;
			_processManager = new ProcessManager();
			_apiClient = new MasterApiClient("127.0.0.1", 8001);
			_testDir = Path.Combine(Path.GetTempPath(), "SlaeScalabilityTests", Guid.NewGuid().ToString());
			Directory.CreateDirectory(_testDir);
		}

		public async Task InitializeAsync()
		{
			_processManager.StartMaster();
			await Task.Delay(1000);
			await _apiClient.ConnectAsync();
		}

		public Task DisposeAsync()
		{
			_apiClient.Disconnect();
			_processManager.Dispose();
			try { Directory.Delete(_testDir, true); } catch { }
			return Task.CompletedTask;
		}

		/// <summary>
		/// СЦЕНАРИЙ 1: Рост размерности матрицы при фиксированном количестве потоков (ресурсов).
		/// Цель: Определить стабильность и рост времени выполнения.
		/// Используем 4 Worker'а.
		/// </summary>
		[Theory]
		[Trait("Category", "Performance")]
		[InlineData(100)]
		[InlineData(500)]
		[InlineData(1000)]
		[InlineData(2000)] 
		[InlineData(4000)] 
		public async Task Scenario1_MatrixGrowth_FixedWorkers(int matrixSize)
		{
			int fixedWorkerCount = 4;
			_output.WriteLine($"СЦЕНАРИЙ 1: Матрица {matrixSize}x{matrixSize}, Workers={fixedWorkerCount}");

			// Запускаем 4 воркера
			for (int i = 0; i < fixedWorkerCount; i++) _processManager.StartWorker();
			await Task.Delay(2000);

			var result = await RunDistributedTest(matrixSize, fixedWorkerCount);

			_output.WriteLine($"Результат: Время={result.ElapsedTime}мс, Итераций={result.Iterations}");
			Assert.True(result.ElapsedTime > 0);
		}

		/// <summary>
		/// СЦЕНАРИЙ 2: Изменение количества потоков (воркеров) при фиксированной матрице.
		/// Цель: Измерить ускорение вычислений.
		/// Матрица фиксирована: 1000x1000.
		/// </summary>
		[Theory]
		[Trait("Category", "Performance")]
		[InlineData(1)]
		[InlineData(2)]
		[InlineData(4)]
		[InlineData(8)]
		[InlineData(16)]
		[InlineData(32)]
		public async Task Scenario2_WorkerGrowth_FixedMatrix(int workerCount)
		{
			int fixedMatrixSize = 1000;
			_output.WriteLine($"СЦЕНАРИЙ 2: Workers={workerCount}, Матрица {fixedMatrixSize}x{fixedMatrixSize}");

			for (int i = 0; i < workerCount; i++) _processManager.StartWorker();
			await Task.Delay(1000 + workerCount * 200);

			var result = await RunDistributedTest(fixedMatrixSize, workerCount);

			_output.WriteLine($"Результат: Время={result.ElapsedTime}мс, Ресурсов={result.UsedResources}");

			if (workerCount > 1)
			{
				Assert.True(result.UsedResources > 1);
			}
		}

		private async Task<CalculationResult> RunDistributedTest(int size, int workerCount)
		{
			var matrixFile = Path.Combine(_testDir, $"m_{size}_{workerCount}.txt");
			var vectorFile = Path.Combine(_testDir, $"v_{size}_{workerCount}.txt");
			var nodesFile = Path.Combine(_testDir, $"n_{size}_{workerCount}.txt");

			await TestDataGenerator.GenerateSlaeFilesAsync(size, matrixFile, vectorFile);
			await TestDataGenerator.GenerateNodesFileAsync(nodesFile, workerCount);

			var tcs = new TaskCompletionSource<CalculationResult>();
			_apiClient.CalculationFinished += r => tcs.TrySetResult(r);

			await _apiClient.StartCalculationAsync(
				CommandCodes.StartSeidelMultiThreadAsync,
				isDistributed: true,
				matrixFile, vectorFile, nodesFile,
				1e-6, 5000
			);

			var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(60000));
			if (completedTask != tcs.Task) throw new TimeoutException("Test timed out");

			return await tcs.Task;
		}
	}
}