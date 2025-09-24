using SlaeSolverSystem.Common.Clients;

namespace SlaeSolverSystem.Tests;

public class EndToEndTests
{
	private const string MasterIp = "127.0.0.1";
	private const int MasterPort = 8001;

	[Fact]
	[Trait("Category", "Integration")]
	public async Task LinearSolve_EndToEnd_ShouldCompleteSuccessfully()
	{
		// Arrange
		var apiClient = new MasterApiClient(MasterIp, MasterPort);
		var tcs = new TaskCompletionSource<(long time, int size)>();

		apiClient.LinearCalculationFinished += (time, size) => tcs.TrySetResult((time, size));

		string matrixFile = "test_matrix_e2e.txt";
		string vectorFile = "test_vector_e2e.txt";
		await File.WriteAllTextAsync(matrixFile, "5 2\n1 3");
		await File.WriteAllTextAsync(vectorFile, "19\n11");

		// Act
		await apiClient.ConnectAsync();
		await apiClient.StartLinearCalculationAsync(Path.GetFullPath(matrixFile), Path.GetFullPath(vectorFile), "", 1e-9, 1000);

		// Assert
		var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(10000));
		Assert.Same(tcs.Task, completedTask);

		var result = await tcs.Task;
		Assert.True(result.time >= 0);
		Assert.Equal(2, result.size);

		// Cleanup
		apiClient.Disconnect();
		File.Delete(matrixFile);
		File.Delete(vectorFile);
	}
}
