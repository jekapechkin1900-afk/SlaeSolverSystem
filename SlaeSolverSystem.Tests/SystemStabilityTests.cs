using SlaeSolverSystem.Common;
using SlaeSolverSystem.Common.Clients;
using SlaeSolverSystem.Common.Contracts;
using SlaeSolverSystem.Tests.Infrastructure;
using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace SlaeSolverSystem.Tests
{
	public class SystemStabilityTests : IAsyncLifetime
	{
		private readonly ITestOutputHelper _output;
		private readonly ProcessManager _processManager;
		private readonly MasterApiClient _apiClient;
		private readonly string _testDir;

		public SystemStabilityTests(ITestOutputHelper output)
		{
			_output = output;
			_processManager = new ProcessManager();
			_apiClient = new MasterApiClient("127.0.0.1", 8001);
			_testDir = Path.Combine(Path.GetTempPath(), "SlaeStabilityTests", Guid.NewGuid().ToString());
			Directory.CreateDirectory(_testDir);
		}

		public async Task InitializeAsync()
		{
			_output.WriteLine("Запуск Master-сервера...");
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
		/// ТЕСТ 1: Устойчивость при массовом подключении (Connection Storm).
		/// Создает 20 Worker'ов с интервалом 20-50 мс.
		/// Проверяет, что все они корректно зарегистрировались в пуле.
		/// </summary>
		[Fact]
		[Trait("Category", "Stability")]
		public async Task Server_Handles_MassiveWorkerConnections_Correctly()
		{
			int workersCount = 20;
			_output.WriteLine($"НАЧАЛО ТЕСТА 1: Подключение {workersCount} воркеров (шторм подключений)...");

			var rand = new Random();

			for (int i = 0; i < workersCount; i++)
			{
				_processManager.StartWorker();
				int delay = rand.Next(20, 51); 
				await Task.Delay(delay);
			}

			_output.WriteLine("Все процессы Worker'ов запущены. Ожидание регистрации...");

			await Task.Delay(2000);

			var tcs = new TaskCompletionSource<(int available, int total)>();
			_apiClient.PoolStateReceived += (avail, total) => tcs.TrySetResult((avail, total));

			await _apiClient.RequestPoolStateAsync();

			var responseTask = await Task.WhenAny(tcs.Task, Task.Delay(5000));
			Assert.Same(tcs.Task, responseTask); 

			var poolState = await tcs.Task;

			_output.WriteLine($"РЕЗУЛЬТАТ: Сервер сообщает: Доступно {poolState.available}, Всего {poolState.total}.");

			Assert.Equal(workersCount, poolState.total);
			Assert.Equal(workersCount, poolState.available);
			_output.WriteLine("ТЕСТ 1 ПРОЙДЕН: Все воркеры успешно зарегистрированы, потерь нет.");
		}

		/// <summary>
		/// ТЕСТ 2: Производительность цикла и устойчивость очереди (Massive Processing).
		/// Запускает тяжелую задачу на большом количестве воркеров (10 шт).
		/// Проверяет, что система выдерживает сотни итераций обмена данными.
		/// </summary>
		[Fact]
		[Trait("Category", "Stability")]
		public async Task System_Handles_HeavyLoad_And_IterativeExchange()
		{
			int workersCount = 10; 
			int matrixSize = 1000;
			int iterations = 100;  
			int timeoutMs = 300000; 

			_output.WriteLine($"НАЧАЛО ТЕСТА 2: Матрица {matrixSize}x{matrixSize}, {workersCount} воркеров, {iterations} итераций.");

			for (int i = 0; i < workersCount; i++)
			{
				_processManager.StartWorker();
				await Task.Delay(200);
			}

			var matrixFile = Path.Combine(_testDir, "matrix.txt");
			var vectorFile = Path.Combine(_testDir, "vector.txt");
			var nodesFile = Path.Combine(_testDir, "nodes.txt");

			await TestDataGenerator.GenerateSlaeFilesAsync(matrixSize, matrixFile, vectorFile);
			await TestDataGenerator.GenerateNodesFileAsync(nodesFile, workersCount);

			_output.WriteLine("Ожидание подключения всех воркеров...");
			int connectedWorkers = 0;
			for (int i = 0; i < 20; i++)
			{
				var poolTcs = new TaskCompletionSource<(int, int)>();
				_apiClient.PoolStateReceived += (avail, total) => poolTcs.TrySetResult((avail, total));

				await _apiClient.RequestPoolStateAsync();
				var state = await Task.WhenAny(poolTcs.Task, Task.Delay(1000));

				if (state == poolTcs.Task)
				{
					connectedWorkers = poolTcs.Task.Result.Item2; 
					if (connectedWorkers >= workersCount) break;
				}
				_apiClient.PoolStateReceived -= (avail, total) => poolTcs.TrySetResult((avail, total)); 
			}

			if (connectedWorkers < workersCount)
			{
				_output.WriteLine($"ПРЕДУПРЕЖДЕНИЕ: Подключилось только {connectedWorkers} из {workersCount} воркеров. Тест продолжится, но будет медленнее.");
			}
			else
			{
				_output.WriteLine($"Все {workersCount} воркеров готовы.");
			}

			var tcs = new TaskCompletionSource<CalculationResult>();
			_apiClient.CalculationFinished += result => tcs.TrySetResult(result);
			_apiClient.CalculationFailed += () => tcs.TrySetException(new Exception("Server reported Calculation Failed"));

			_output.WriteLine("Отправка команды на старт...");

			await _apiClient.StartCalculationAsync(
				CommandCodes.StartSeidelMultiThreadAsync,
				isDistributed: true,
				matrixFile, vectorFile, nodesFile,
				epsilon: 1e-20, 
				maxIterations: iterations 
			);

			var task = await Task.WhenAny(tcs.Task, Task.Delay(timeoutMs));

			if (task != tcs.Task)
			{
				Assert.Fail($"Тайм-аут {timeoutMs / 1000} сек. истек! Система слишком медленная или зависла.");
			}

			var result = await tcs.Task;

			_output.WriteLine($"РЕЗУЛЬТАТ: Обработано итераций: {result.Iterations}. Время: {result.ElapsedTime} мс. Ресурсов: {result.UsedResources}");

			Assert.True(result.Iterations >= iterations, $"Ожидалось {iterations} итераций, выполнено {result.Iterations}");
			Assert.Equal(matrixSize, result.MatrixSize);

			_output.WriteLine("ТЕСТ 2 ПРОЙДЕН УСПЕШНО.");
		}
	}
}