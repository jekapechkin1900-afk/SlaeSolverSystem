using SlaeSolverSystem.Common;
using SlaeSolverSystem.Common.Clients;
using Xunit.Abstractions;

namespace SlaeSolverSystem.Tests;

public class EndToEndTests : IAsyncLifetime
{
	private readonly ITestOutputHelper _output;
	private readonly MasterApiClient _apiClient;
	private readonly string _testDir;

	public EndToEndTests(ITestOutputHelper output)
	{
		_output = output;
		_apiClient = new MasterApiClient("127.0.0.1", 8001);
		_testDir = Path.Combine(Path.GetTempPath(), "SlaeE2ETests");
		Directory.CreateDirectory(_testDir);
	}

	public Task InitializeAsync() => _apiClient.ConnectAsync();

	public Task DisposeAsync()
	{
		_apiClient.Disconnect();
		try { Directory.Delete(_testDir, true); } catch { }
		return Task.CompletedTask;
	}

	[Fact]
	[Trait("Category", "Integration")]
	public async Task LinearGaussSolve_EndToEnd_ShouldCompleteSuccessfully()
	{
		// Arrange
		_output.WriteLine("--- Запуск E2E теста для линейного метода Гаусса ---");
		var tcs = new TaskCompletionSource<(long time, int size)>();
		_apiClient.LinearCalculationFinished += (time, size) => tcs.TrySetResult((time, size));

		string matrixFile = Path.Combine(_testDir, "test_matrix_gauss.txt");
		string vectorFile = Path.Combine(_testDir, "test_vector_gauss.txt");

		// Используем корректные данные: 5x + 2y = 19; x + 3y = 9. Ответ: x=3, y=2
		await File.WriteAllTextAsync(matrixFile, "5 2\n1 3");
		await File.WriteAllTextAsync(vectorFile, "19\n9");
		_output.WriteLine("Тестовые файлы созданы.");

		// Act
		_output.WriteLine("Отправка команды StartGaussLinear на сервер...");
		await _apiClient.StartCalculationAsync(
			CommandCodes.StartGaussLinear,
			matrixFile,
			vectorFile,
			"",
			0,  
			0 
		);

		// Assert
		_output.WriteLine("Ожидание ответа от сервера...");
		var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(10000));
		Assert.Same(tcs.Task, completedTask);

		var result = await tcs.Task;
		_output.WriteLine($"Сервер ответил. Время выполнения: {result.time} мс, Размер: {result.size}.");
		Assert.True(result.time >= 0);
		Assert.Equal(2, result.size);

		_output.WriteLine("Тест успешно пройден.");
	}
}
