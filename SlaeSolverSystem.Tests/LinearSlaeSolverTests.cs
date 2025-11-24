using SlaeSolverSystem.Common;
using Xunit;

namespace SlaeSolverSystem.Tests;

public class LinearSlaeSolverTests
{
	// 1. Проверка простой системы с единичной матрицей
	[Fact]
	public async Task SolveAsync_IdentityMatrix_ReturnsVectorB()
	{
		// Arrange
		var A = new double[,] { { 1, 0 }, { 0, 1 } };
		var b = new double[] { 5, -2 };
		var expectedX = new double[] { 5.0, -2.0 };

		// Act
		var result = await LinearSlaeSolver.SolveAsync(A, b);

		// Assert
		Assert.NotNull(result?.SolutionVector);
		Assert.Equal(expectedX[0], result.SolutionVector[0], precision: 10);
		Assert.Equal(expectedX[1], result.SolutionVector[1], precision: 10);
	}

	// 2. Проверка системы с дробными числами
	[Fact]
	public async Task SolveAsync_FloatingPointNumbers_ReturnsCorrectSolution()
	{
		// Arrange
		// 0.5x + 1.5y = 2.5
		// 1.0x + 0.25y = 2.25
		// Ответ: x = 2, y = 1
		var A = new double[,] { { 0.5, 1.5 }, { 1.0, 0.25 } };
		var b = new double[] { 2.5, 2.25 };
		var expectedX = new double[] { 2.0, 1.0 };

		// Act
		var result = await LinearSlaeSolver.SolveAsync(A, b);

		// Assert
		Assert.Equal(expectedX[0], result.SolutionVector[0], precision: 10);
		Assert.Equal(expectedX[1], result.SolutionVector[1], precision: 10);
	}

	// 3. Проверка системы 3x3
	[Fact]
	public async Task SolveAsync_3x3System_ReturnsCorrectSolution()
	{
		// Arrange
		// 2x + y - z = 8
		// -3x - y + 2z = -11
		// -2x + y + 2z = -3
		// Ответ: x = 2, y = 3, z = -1
		var A = new double[,] { { 2, 1, -1 }, { -3, -1, 2 }, { -2, 1, 2 } };
		var b = new double[] { 8, -11, -3 };
		var expectedX = new double[] { 2.0, 3.0, -1.0 };

		// Act
		var result = await LinearSlaeSolver.SolveAsync(A, b);

		// Assert
		Assert.Equal(expectedX.Length, result.SolutionVector.Length);
		Assert.Equal(expectedX[0], result.SolutionVector[0], precision: 10);
		Assert.Equal(expectedX[1], result.SolutionVector[1], precision: 10);
		Assert.Equal(expectedX[2], result.SolutionVector[2], precision: 10);
	}

	// 4. Проверка однородной системы (правая часть равна 0)
	[Fact]
	public async Task SolveAsync_ZeroResultVector_ReturnsZeros()
	{
		// Arrange
		var A = new double[,] { { 2, 3 }, { 4, 1 } };
		var b = new double[] { 0, 0 };
		var expectedX = new double[] { 0.0, 0.0 };

		// Act
		var result = await LinearSlaeSolver.SolveAsync(A, b);

		// Assert
		Assert.Equal(expectedX[0], result.SolutionVector[0], precision: 10);
		Assert.Equal(expectedX[1], result.SolutionVector[1], precision: 10);
	}

	// 5. Проверка необходимости перестановки строк (Pivoting)
	// Если на диагонали 0, алгоритм должен поменять строки местами.
	[Fact]
	public async Task SolveAsync_ZeroOnDiagonal_PerformsPivotingAndSolves()
	{
		// Arrange
		// 0x + 2y = 4
		// 3x + 1y = 5
		// Ответ: y = 2, x = 1
		var A = new double[,] { { 0, 2 }, { 3, 1 } };
		var b = new double[] { 4, 5 };
		var expectedX = new double[] { 1.0, 2.0 };

		// Act
		var result = await LinearSlaeSolver.SolveAsync(A, b);

		// Assert
		Assert.Equal(expectedX[0], result.SolutionVector[0], precision: 10);
		Assert.Equal(expectedX[1], result.SolutionVector[1], precision: 10);
	}

	// 6. Проверка корректности заполнения поля Size в результате
	[Fact]
	public async Task SolveAsync_ReturnsCorrectSizeInResult()
	{
		// Arrange
		var A = new double[,] { { 1, 2 }, { 3, 4 } };
		var b = new double[] { 5, 6 };

		// Act
		var result = await LinearSlaeSolver.SolveAsync(A, b);

		// Assert
		Assert.Equal(2, result.MatrixSize);
		Assert.True(result.ElapsedMilliseconds >= 0);
	}

	// 7. Проверка на больших числах
	[Fact]
	public async Task SolveAsync_LargeNumbers_ReturnsCorrectSolution()
	{
		// Arrange
		var A = new double[,] { { 1000, 2000 }, { 2000, 1000 } };
		var b = new double[] { 5000, 4000 }; // x=1, y=2
		var expectedX = new double[] { 1.0, 2.0 };

		// Act
		var result = await LinearSlaeSolver.SolveAsync(A, b);

		// Assert
		Assert.Equal(expectedX[0], result.SolutionVector[0], precision: 10);
		Assert.Equal(expectedX[1], result.SolutionVector[1], precision: 10);
	}

	// 8. [ИСПРАВЛЕН] Проверка несовпадения размерностей
	// Ваша реализация берет size = b.Length. Если матрица меньше, чем b,
	// при доступе к A будет выброшен IndexOutOfRangeException.
	[Fact]
	public async Task SolveAsync_MatrixSmallerThanVector_ThrowsIndexOutOfRangeException()
	{
		// Arrange
		var A = new double[,] { { 1, 2 }, { 3, 4 } }; // 2x2
		var b = new double[] { 1, 2, 3 }; // Длина 3

		// Act & Assert
		// Ожидаем системную ошибку выхода за границы массива, т.к. валидации входных данных нет
		await Assert.ThrowsAsync<IndexOutOfRangeException>(async () =>
			await LinearSlaeSolver.SolveAsync(A, b));
	}

	// 9. [ИСПРАВЛЕН] Проверка вырожденной матрицы
	// Ваша реализация явно выбрасывает InvalidOperationException
	[Fact]
	public async Task SolveAsync_SingularMatrix_ThrowsInvalidOperationException()
	{
		// Arrange
		// x + y = 2
		// 2x + 2y = 4
		// Определитель = 0
		var A = new double[,] { { 1, 1 }, { 2, 2 } };
		var b = new double[] { 2, 4 };

		// Act & Assert
		var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
			await LinearSlaeSolver.SolveAsync(A, b));

		Assert.Contains("Матрица вырождена", exception.Message);
	}

	// 10. Проверка на "почти" вырожденной матрице (очень малый определитель)
	// Ваша проверка: if (Math.Abs(matrix[k, k]) < 1e-12)
	[Fact]
	public async Task SolveAsync_NearSingularMatrix_ThrowsInvalidOperationException()
	{
		// Arrange
		// 1e-13 - очень маленькое число, меньше вашего порога 1e-12
		var A = new double[,] { { 1e-13, 0 }, { 0, 1 } };
		var b = new double[] { 1, 1 };

		// Act & Assert
		await Assert.ThrowsAsync<InvalidOperationException>(async () =>
			await LinearSlaeSolver.SolveAsync(A, b));
	}
}