using SlaeSolverSystem.Worker.Core; 

namespace SlaeSolverSystem.Tests;

public class WorkerTaskTests
{
	private readonly WorkerTask _workerTask;

	// Конструктор выполняется перед каждым отдельным тестом,
	// обеспечивая их полную изоляцию.
	public WorkerTaskTests()
	{
		_workerTask = new WorkerTask();
	}

	#region Тесты на состояние (State Tests)

	[Fact]
	public void InitialState_IsSet_IsFalse()
	{
		// Assert
		Assert.False(_workerTask.IsSet);
	}

	[Fact]
	public void SetData_WithValidData_IsSet_IsTrue()
	{
		// Arrange
		var matrix = new double[,] { { 1 } };
		var b = new double[] { 1 };

		// Act
		_workerTask.SetData(0, 1, 1, matrix, b);

		// Assert
		Assert.True(_workerTask.IsSet);
	}

	[Fact]
	public void Reset_AfterSettingData_IsSet_IsFalse()
	{
		// Arrange
		var matrix = new double[,] { { 1 } };
		var b = new double[] { 1 };
		_workerTask.SetData(0, 1, 1, matrix, b);

		// Act
		_workerTask.Reset();

		// Assert
		Assert.False(_workerTask.IsSet);
	}

	#endregion

	#region Тесты на некорректные вызовы (Invalid Call Tests)

	[Fact]
	public void CalculatePart_WhenTaskIsNotSet_ThrowsInvalidOperationException()
	{
		// Arrange
		var x_vector = new double[] { 0 };

		// Act & Assert
		var exception = Assert.Throws<InvalidOperationException>(() =>
			_workerTask.CalculatePart(x_vector)
		);
		Assert.Equal("Задача не установлена.", exception.Message);
	}

	#endregion

	#region Тесты на вычисления (Calculation Tests)

	[Fact]
	public void CalculatePart_ForSingleEquation_CalculatesCorrectly()
	{
		// Arrange: 10x = 20 => x = 2
		var matrix = new double[,] { { 10 } };
		var b = new double[] { 20 };
		_workerTask.SetData(0, 1, 1, matrix, b);
		var x_old = new double[] { 0 };

		// Act
		var result = _workerTask.CalculatePart(x_old);

		// Assert
		Assert.Single(result);
		Assert.Equal(2.0, result[0], precision: 10);
	}

	[Fact]
	public void CalculatePart_ForSimple2x2System_CalculatesCorrectly()
	{
		// Arrange: Worker обрабатывает всю систему 2x2
		// 5x + 2y = 19
		// 1x + 3y = 9
		var matrix = new double[,] { { 5, 2 }, { 1, 3 } };
		var b = new double[] { 19, 9 };
		_workerTask.SetData(0, 2, 2, matrix, b);
		var x_old = new double[] { 0, 0 };

		// Act
		var result = _workerTask.CalculatePart(x_old);

		// Assert
		// x_new = (19 - 2*0) / 5 = 3.8
		// y_new = (9 - 1*0) / 3 = 3.0
		Assert.Equal(2, result.Length);
		Assert.Equal(3.8, result[0], precision: 10);
		Assert.Equal(3.0, result[1], precision: 10);
	}

	[Fact]
	public void CalculatePart_ForPartOfLargerSystem_CalculatesCorrectly()
	{
		// Arrange: Worker обрабатывает только вторую строку (индекс 1) из системы 3x3.
		// 10x + 2y + 3z = 45
		// 1x  + 8y + 1z = 35
		// 2x  + 1y + 9z = 52
		// Начальное приближение x_old = [1, 1, 1]
		var workerMatrix = new double[,] { { 1, 8, 1 } };
		var workerB = new double[] { 35 };
		_workerTask.SetData(startRow: 1, rowCount: 1, matrixSize: 3, workerMatrix, workerB);
		var x_old = new double[] { 1, 1, 1 };

		// Act
		var result = _workerTask.CalculatePart(x_old);

		// Assert
		// y_new = (35 - (1*1 + 1*1)) / 8 = 33 / 8 = 4.125
		Assert.Single(result);
		Assert.Equal(4.125, result[0], precision: 10);
	}

	[Fact]
	public void CalculatePart_WithZeroOnDiagonal_ReturnsZeroWithoutException()
	{
		// Arrange: 0x = 10. Деление на ноль.
		// По нашей логике, должен вернуться 0, а не исключение.
		var matrix = new double[,] { { 0 } };
		var b = new double[] { 10 };
		_workerTask.SetData(0, 1, 1, matrix, b);
		var x_old = new double[] { 0 };

		// Act
		var result = _workerTask.CalculatePart(x_old);

		// Assert
		Assert.Single(result);
		Assert.Equal(0.0, result[0]);
	}

	#endregion

	#region Параметризованные тесты (Parameterized Tests)

	// Этот тест выполнится 3 раза с разными данными
	[Theory]
	[InlineData(new double[] { 0, 0 }, 3.8, 3.0)] // Начальное приближение [0,0]
	[InlineData(new double[] { 1, 1 }, 3.4, 2.6666666667)] // Начальное приближение [1,1]
	[InlineData(new double[] { 3, 2 }, 3.0, 2.0)] // Начальное приближение = ответ
	public void CalculatePart_WithDifferentInitialVectors_CalculatesCorrectly(double[] initialX, double expectedX0, double expectedX1)
	{
		// Arrange
		// Система та же: 5x + 2y = 19; 1x + 3y = 9
		var matrix = new double[,] { { 5, 2 }, { 1, 3 } };
		var b = new double[] { 19, 9 };
		_workerTask.SetData(0, 2, 2, matrix, b);

		// Act
		var result = _workerTask.CalculatePart(initialX);

		// Assert
		Assert.Equal(2, result.Length);
		Assert.Equal(expectedX0, result[0], precision: 10);
		Assert.Equal(expectedX1, result[1], precision: 10);
	}

	[Theory]
	[InlineData(1)]
	[InlineData(10)]
	[InlineData(100)]
	public void Reset_CalledMultipleTimes_StateIsCorrectlyReset(int callCount)
	{
		// Arrange
		var matrix = new double[,] { { 1 } };
		var b = new double[] { 1 };
		_workerTask.SetData(0, 1, 1, matrix, b);
		Assert.True(_workerTask.IsSet); // Убедимся, что состояние установлено

		// Act
		for (int i = 0; i < callCount; i++)
		{
			_workerTask.Reset();
		}

		// Assert
		Assert.False(_workerTask.IsSet);
	}

	[Fact]
	public void SetData_CanBeCalledAgainAfterReset()
	{
		// Arrange
		var matrix1 = new double[,] { { 1 } };
		var b1 = new double[] { 1 };
		_workerTask.SetData(0, 1, 1, matrix1, b1);
		_workerTask.Reset();

		var matrix2 = new double[,] { { 2, 3 }, { 4, 5 } };
		var b2 = new double[] { 6, 7 };

		// Act
		_workerTask.SetData(0, 2, 2, matrix2, b2);

		// Assert
		Assert.True(_workerTask.IsSet);
		// Проверим, что новые данные действительно работают
		var result = _workerTask.CalculatePart(new double[] { 0, 0 });
		Assert.Equal(3.0, result[0]); // (6 - 3*0) / 2 = 3
		Assert.Equal(1.4, result[1]); // (7 - 4*0) / 5 = 1.4
	}
	#endregion
}