using SlaeSolverSystem.Common;

namespace SlaeSolverSystem.Tests;

public class LinearSlaeSolverTests
{
	[Fact]
	public async Task SolveAsync_GaussianElimination_ReturnsCorrectSolution()
	{
		// Arrange
		// 5x + 2y = 19
		// 1x + 3y = 9
		// Ответ: x = 3, y = 2
		var A = new double[,] { { 5, 2 }, { 1, 3 } };
		var b = new double[] { 19, 9 };
		var expectedX = new double[] { 3.0, 2.0 };

		// Act
		var result = await LinearSlaeSolver.SolveAsync(A, b);

		// Assert
		Assert.NotNull(result?.SolutionVector);
		Assert.Equal(expectedX.Length, result.SolutionVector.Length);

		Assert.Equal(expectedX[0], result.SolutionVector[0], precision: 10);
		Assert.Equal(expectedX[1], result.SolutionVector[1], precision: 10);
	}

	[Fact]
	public async Task SolveAsync_GaussianElimination_ForLargerSystem_ReturnsCorrectSolution()
	{
		// Arrange
		// Добавим еще один тест для системы 3x3 для надежности
		// 2x + 1y - 1z = 8
		// -3x - 1y + 2z = -11
		// -2x + 1y + 2z = -3
		// Ожидаемый ответ: x = 2, y = 3, z = -1
		var A = new double[,] { { 2, 1, -1 }, { -3, -1, 2 }, { -2, 1, 2 } };
		var b = new double[] { 8, -11, -3 };
		var expectedX = new double[] { 2.0, 3.0, -1.0 };

		// Act
		var result = await LinearSlaeSolver.SolveAsync(A, b);

		// Assert
		Assert.NotNull(result?.SolutionVector);
		Assert.Equal(expectedX.Length, result.SolutionVector.Length);

		Assert.Equal(expectedX[0], result.SolutionVector[0], precision: 12);
		Assert.Equal(expectedX[1], result.SolutionVector[1], precision: 12);
		Assert.Equal(expectedX[2], result.SolutionVector[2], precision: 12);
	}
}
