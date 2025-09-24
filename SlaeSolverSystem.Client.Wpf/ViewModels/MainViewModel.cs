using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using Microsoft.Win32;
using SlaeSolverSystem.Client.Wpf.Commands;
using SlaeSolverSystem.Common.Clients;
using SlaeSolverSystem.Common.Contracts;

namespace SlaeSolverSystem.Client.Wpf.ViewModels;

public class MainViewModel : BaseViewModel
{
	private MetroWindow _mainWindow;
	private IDialogCoordinator _dialogCoordinator;
	private readonly DispatcherTimer _timer;
	private DateTime _startTime;

	private readonly MasterApiClient _apiClient;
	private long _lastLinearTime;

	#region Public Properties for Binding

	public ObservableCollection<string> LogMessages { get; } = new ObservableCollection<string>();

	private string _logText;
	public string LogText
	{
		get => _logText;
		private set { _logText = value; OnPropertyChanged(); }
	}


	private TimeSpan _elapsedTime;
	public TimeSpan ElapsedTime
	{
		get => _elapsedTime;
		private set { _elapsedTime = value; OnPropertyChanged(); }
	}

	private string _matrixFilePath;
	public string MatrixFilePath { get => _matrixFilePath; set { _matrixFilePath = value; OnPropertyChanged(); } }

	private string _vectorFilePath;
	public string VectorFilePath { get => _vectorFilePath; set { _vectorFilePath = value; OnPropertyChanged(); } }

	private string _nodesFilePath;
	public string NodesFilePath { get => _nodesFilePath; set { _nodesFilePath = value; OnPropertyChanged(); } }

	private double _epsilon = 1e-9;
	public double Epsilon { get => _epsilon; set { _epsilon = value; OnPropertyChanged(); } }

	private int _maxIterations = 10000;
	public int MaxIterations { get => _maxIterations; set { _maxIterations = value; OnPropertyChanged(); } }

	private string _statusText = "Готов к работе";
	public string StatusText { get => _statusText; set { _statusText = value; OnPropertyChanged(); } }

	private int _iteration;
	public int Iteration { get => _iteration; set { _iteration = value; OnPropertyChanged(); } }

	private double _currentError;
	public double CurrentError { get => _currentError; set { _currentError = value; OnPropertyChanged(); } }

	private bool _isRunning;
	public bool IsRunning
	{
		get => _isRunning;
		private set
		{
			_isRunning = value;
			OnPropertyChanged();
			OnPropertyChanged(nameof(IsNotRunning));
		}
	}
	public bool IsNotRunning => !IsRunning;

	private string _resultPreviewText;
	public string ResultPreviewText { get => _resultPreviewText; private set { _resultPreviewText = value; OnPropertyChanged(); } }

	private string _resultFilePathOnServer;
	public string ResultFilePathOnServer { get => _resultFilePathOnServer; private set { _resultFilePathOnServer = value; OnPropertyChanged(); } }

	private double[] _lastSolutionVector;
	public bool CanSaveResult => _lastSolutionVector != null && _lastSolutionVector.Length > 0 && !IsRunning;

	public ObservableCollection<TestResultViewModel> TestResults { get; } = new ObservableCollection<TestResultViewModel>();

	#endregion

	#region Commands
	public ICommand BrowseMatrixCommand { get; }
	public ICommand BrowseVectorCommand { get; }
	public ICommand BrowseNodesCommand { get; }
	public ICommand StartDistributedTestCommand { get; }
	public ICommand SaveResultCommand { get; }
	public ICommand StartLinearTestCommand { get; }
	public ICommand ClearResultsCommand { get; }
	#endregion

	public MainViewModel(IDialogCoordinator dialogCoordinator = null)
	{
		_dialogCoordinator = dialogCoordinator;
		_apiClient = new MasterApiClient("127.0.0.1", 8001);

		BrowseMatrixCommand = new RelayCommand(_ => BrowseFile(path => MatrixFilePath = path), _ => IsNotRunning);
		BrowseVectorCommand = new RelayCommand(_ => BrowseFile(path => VectorFilePath = path), _ => IsNotRunning);
		BrowseNodesCommand = new RelayCommand(_ => BrowseFile(path => NodesFilePath = path), _ => IsNotRunning);
		StartDistributedTestCommand = new RelayCommand(async _ => await StartDistributedTest(), _ => CanStart());
		SaveResultCommand = new RelayCommand(_ => PromptToSaveResult(), _ => CanSaveResult);
		StartLinearTestCommand = new RelayCommand(async _ => await StartLinearTest(), _ => CanStart());
		ClearResultsCommand = new RelayCommand(_ => { TestResults.Clear(); _lastLinearTime = 0; }, _ => IsNotRunning);
		LogMessages.CollectionChanged += OnLogMessagesChanged;

		_timer = new DispatcherTimer
		{
			Interval = TimeSpan.FromSeconds(1)
		};
		_timer.Tick += OnTimerTick;


		SubscribeToApiEvents();
	}

	public void SetWindow(MetroWindow window) => _mainWindow = window;

	private async Task ShowMessageAsync(string title, string message, MessageDialogStyle style = MessageDialogStyle.Affirmative)
	{
		if (_dialogCoordinator != null)
		{
			await _dialogCoordinator.ShowMessageAsync(this, title, message, style);
		}
		else 
		{
			MessageBox.Show(message, title);
		}
	}

	private void SubscribeToApiEvents()
	{
		_apiClient.LogReceived += msg => DispatcherInvoke(() => LogMessages.Insert(0, msg));
		_apiClient.StatusReceived += status => DispatcherInvoke(() => StatusText = status);
		_apiClient.ProgressReceived += (iter, err) => DispatcherInvoke(() => { Iteration = iter; CurrentError = err; });
		_apiClient.Disconnected += OnDisconnected;
		_apiClient.CalculationFinished += OnCalculationFinished;
		_apiClient.LinearCalculationFinished += OnLinearCalculationFinished;
		_apiClient.CalculationFailed += OnCalculationFailed;
	}

	#region API Event Handlers

	private void OnLogMessagesChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
	{
		var sb = new StringBuilder();
		foreach (var message in LogMessages)
		{
			sb.AppendLine(message);
		}
		LogText = sb.ToString();
	}

	private void OnLinearCalculationFinished(long time, int matrixSize)
	{
		DispatcherInvoke(() =>
		{
			_timer.Stop();
			_lastLinearTime = time;
			TestResults.Add(new TestResultViewModel
			{
				TestType = "Линейный",
				MatrixSize = matrixSize,
				TimeMs = time,
				Iterations = 0,
				Speedup = 1.0
			});
			IsRunning = false;
			StatusText = "Готов к работе";
		});
	}

	private void OnCalculationFailed()
	{
		DispatcherInvoke(() =>
		{
			_timer.Stop();
			IsRunning = false;
			StatusText = "Ошибка при выполнении";
		});
	}


	private void OnCalculationFinished(CalculationResult result)
	{
		DispatcherInvoke(() =>
		{
			_timer.Stop();
			_lastSolutionVector = result.SolutionVector;

			var sb = new StringBuilder("Первые 10 элементов вектора x:\n");
			for (int i = 0; i < Math.Min(result.SolutionVector.Length, 10); i++)
				sb.AppendLine($"x[{i}] = {result.SolutionVector[i]:F10}");
			ResultPreviewText = sb.ToString();

			ResultFilePathOnServer = "Результат получен. Нажмите 'Сохранить', чтобы сохранить его в файл.";
			LogMessages.Insert(0, "Результат получен клиентом.");

			double speedup = (_lastLinearTime > 0) ? Math.Round((double)_lastLinearTime / result.ElapsedTime, 2) : 0;
			int nodeCount = File.ReadAllLines(NodesFilePath).Length;
			TestResults.Add(new TestResultViewModel
			{
				TestType = $"Распред. ({nodeCount} узлов)",
				MatrixSize = result.MatrixSize,
				TimeMs = result.ElapsedTime,
				Iterations = result.Iterations,
				Speedup = speedup
			});

			IsRunning = false;
			StatusText = "Завершено";
			OnPropertyChanged(nameof(CanSaveResult));
		});
	}

	private void OnDisconnected()
	{
		DispatcherInvoke(() =>
		{
			_timer.Stop();
			if (IsRunning)
			{
				IsRunning = false;
				StatusText = "Отключен от Master-сервера";
				LogMessages.Insert(0, "ОШИБКА: Соединение с сервером потеряно во время выполнения.");
			}
			else
			{
				StatusText = "Отключен от Master-сервера";
			}
		});
	}
	#endregion

	#region Command Methods
	private async Task StartLinearTest()
	{
		await StartTestInternalAsync("линейного теста", () =>
			_apiClient.StartLinearCalculationAsync(MatrixFilePath, VectorFilePath, NodesFilePath, Epsilon, MaxIterations));
	}

	private async Task StartDistributedTest()
	{
		await StartTestInternalAsync("распределенного теста", () =>
			_apiClient.StartDistributedCalculationAsync(MatrixFilePath, VectorFilePath, NodesFilePath, Epsilon, MaxIterations));
	}

	private async Task StartTestInternalAsync(string testName, Func<Task> apiCall)
	{
		if (IsRunning) return;

		IsRunning = true;
		LogMessages.Clear();
		ResultPreviewText = string.Empty;
		ResultFilePathOnServer = string.Empty;
		Iteration = 0;
		CurrentError = 0;
		_lastSolutionVector = null;
		_startTime = DateTime.Now;
		ElapsedTime = TimeSpan.Zero;
		LogMessages.Clear();
		_timer.Start();
		OnPropertyChanged(nameof(CanSaveResult));

		LogMessages.Insert(0, $"Начало {testName}...");
		StatusText = "Подключение к Master-серверу...";

		try
		{
			await _apiClient.ConnectAsync();
			LogMessages.Insert(0, "Подключение к Master-серверу установлено.");
			StatusText = $"Отправка задачи ({testName})...";

			await apiCall();

			LogMessages.Insert(0, "Задача отправлена. Ожидание ответа от сервера...");
		}
		catch (Exception ex)
		{
			_timer.Stop();
			await ShowMessageAsync("Ошибка", $"Не удалось запустить тест: {ex.Message}");
			StatusText = "Ошибка подключения";
			IsRunning = false; 
		}
	}

	private bool CanStart() => !IsRunning && !string.IsNullOrEmpty(MatrixFilePath) && !string.IsNullOrEmpty(VectorFilePath) && !string.IsNullOrEmpty(NodesFilePath);

	private void BrowseFile(Action<string> setPathAction)
	{
		var ofd = new OpenFileDialog();
		if (ofd.ShowDialog() == true)
		{
			setPathAction(ofd.FileName);
			OnPropertyChanged(nameof(MatrixFilePath));
			OnPropertyChanged(nameof(VectorFilePath));
			OnPropertyChanged(nameof(NodesFilePath));
		}
	}

	private void OnTimerTick(object sender, EventArgs e)
	{
		ElapsedTime = DateTime.Now - _startTime;
	}

	private void PromptToSaveResult()
	{
		if (_lastSolutionVector == null || _lastSolutionVector.Length == 0) return;

		var sfd = new SaveFileDialog
		{
			Title = "Сохранить вектор решения",
			Filter = "Текстовый файл (*.txt)|*.txt|Все файлы (*.*)|*.*",
			FileName = $"result_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt"
		};

		if (sfd.ShowDialog() == true)
		{
			try
			{
				var lines = _lastSolutionVector.Select(d => d.ToString("F10", CultureInfo.InvariantCulture));
				File.WriteAllLines(sfd.FileName, lines);
				MessageBox.Show($"Результат успешно сохранен в файл:\n{sfd.FileName}", "Сохранение успешно", MessageBoxButton.OK, MessageBoxImage.Information);
				LogMessages.Insert(0, $"Результат сохранен в: {sfd.FileName}");
			}
			catch (Exception ex)
			{
				_ = ShowMessageAsync("Ошибка сохранения", $"Не удалось сохранить файл: {ex.Message}");
				LogMessages.Insert(0, $"ОШИБКА: Не удалось сохранить результат в файл.");
			}
		}
	}
	#endregion

	private void DispatcherInvoke(Action action) => Application.Current?.Dispatcher.Invoke(action);
}