using SlaeSolverSystem.Worker.Core;
using System;
using System.Threading.Tasks;
using Xunit;

namespace SlaeSolverSystem.Tests
{
	public class WorkerTaskTests
	{
		private readonly WorkerTask _workerTask;

		public WorkerTaskTests()
		{
			_workerTask = new WorkerTask();
		}

		#region Тесты на состояние (State Tests)

		[Fact]
		public void InitialState_IsSet_IsFalse()
		{
			Assert.False(_workerTask.IsSet);
		}

		[Fact]
		public void SetData_WithValidData_IsSet_IsTrue()
		{
			var matrix = new double[,] { { 1 } };
			var b = new double[] { 1 };
			_workerTask.SetData(0, 1, 1, matrix, b);
			Assert.True(_workerTask.IsSet);
		}

		[Fact]
		public void Reset_AfterSettingData_IsSet_IsFalse()
		{
			var matrix = new double[,] { { 1 } };
			var b = new double[] { 1 };
			_workerTask.SetData(0, 1, 1, matrix, b);
			_workerTask.Reset();
			Assert.False(_workerTask.IsSet);
		}

		#endregion

		#region Тесты на некорректные вызовы (Invalid Call Tests)

		[Fact]
		public void CalculatePartSingleThread_WhenTaskIsNotSet_ThrowsInvalidOperationException()
		{
			var x_vector = new double[] { 0 };
			var exception = Assert.Throws<InvalidOperationException>(() => _workerTask.CalculatePartSingleThread(x_vector));
			Assert.Equal("Задача не установлена.", exception.Message);
		}

		#endregion

		#region Тесты на вычисления (Calculation Tests - Все методы)

		// Используем Theory для проверки всех методов одной логикой
		[Theory]
		[InlineData("SingleThread")]
		[InlineData("ThreadPool")]
		[InlineData("ManualThreads")]
		[InlineData("Async")]
		public async Task CalculatePart_AllMethods_ForSimple2x2System_CalculatesCorrectly(string method)
		{
			// Arrange
			// 5x + 2y = 19
			// 1x + 3y = 9
			var matrix = new double[,] { { 5, 2 }, { 1, 3 } };
			var b = new double[] { 19, 9 };
			_workerTask.SetData(0, 2, 2, matrix, b);
			var x_old = new double[] { 0, 0 };

			// Act
			double[] result = null;
			switch (method)
			{
				case "SingleThread":
					result = _workerTask.CalculatePartSingleThread(x_old);
					break;
				case "ThreadPool":
					result = _workerTask.CalculatePartMultiThreadWithPool(x_old);
					break;
				case "ManualThreads":
					result = _workerTask.CalculatePartMultiThreadWithoutPool(x_old);
					break;
				case "Async":
					result = await _workerTask.CalculatePartMultiThreadAsync(x_old);
					break;
			}

			// Assert
			// x_new = (19 - 2*0) / 5 = 3.8
			// y_new = (9 - 1*0) / 3 = 3.0
			Assert.NotNull(result);
			Assert.Equal(2, result.Length);
			Assert.Equal(3.8, result[0], precision: 10);
			Assert.Equal(3.0, result[1], precision: 10);
		}

		[Fact]
		public void CalculatePart_WithZeroOnDiagonal_ReturnsZeroWithoutException()
		{
			// Arrange: 0x = 10.
			var matrix = new double[,] { { 0 } };
			var b = new double[] { 10 };
			_workerTask.SetData(0, 1, 1, matrix, b);
			var x_old = new double[] { 0 };

			// Act
			var result = _workerTask.CalculatePartSingleThread(x_old);

			// Assert
			Assert.Single(result);
			Assert.Equal(0.0, result[0]);
		}

		#endregion

		#region Параметризованные тесты (Parameterized Tests)

		[Theory]
		[InlineData(new double[] { 0, 0 }, 3.8, 3.0)]
		[InlineData(new double[] { 1, 1 }, 3.4, 2.6666666667)]
		[InlineData(new double[] { 3, 2 }, 3.0, 2.0)]
		public void CalculatePartSingleThread_WithDifferentInitialVectors_CalculatesCorrectly(double[] initialX, double expectedX0, double expectedX1)
		{
			var matrix = new double[,] { { 5, 2 }, { 1, 3 } };
			var b = new double[] { 19, 9 };
			_workerTask.SetData(0, 2, 2, matrix, b);

			var result = _workerTask.CalculatePartSingleThread(initialX);

			Assert.Equal(2, result.Length);
			Assert.Equal(expectedX0, result[0], precision: 10);
			Assert.Equal(expectedX1, result[1], precision: 10);
		}

		[Theory]
		[InlineData(1)]
		[InlineData(10)]
		public void Reset_CalledMultipleTimes_StateIsCorrectlyReset(int callCount)
		{
			var matrix = new double[,] { { 1 } };
			var b = new double[] { 1 };
			_workerTask.SetData(0, 1, 1, matrix, b);
			Assert.True(_workerTask.IsSet);

			for (int i = 0; i < callCount; i++)
			{
				_workerTask.Reset();
			}

			Assert.False(_workerTask.IsSet);
		}

		[Fact]
		public void SetData_CanBeCalledAgainAfterReset()
		{
			var matrix1 = new double[,] { { 1 } };
			var b1 = new double[] { 1 };
			_workerTask.SetData(0, 1, 1, matrix1, b1);
			_workerTask.Reset();

			var matrix2 = new double[,] { { 2, 3 }, { 4, 5 } };
			var b2 = new double[] { 6, 7 };

			_workerTask.SetData(0, 2, 2, matrix2, b2);

			Assert.True(_workerTask.IsSet);
			var result = _workerTask.CalculatePartSingleThread(new double[] { 0, 0 });
			Assert.Equal(3.0, result[0]);
			Assert.Equal(1.4, result[1]);
		}
		#endregion
	}
}