using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using MahApps.Metro.Controls.Dialogs;
using Microsoft.Win32;
using SlaeSolverSystem.Client.Wpf.Commands;
using SlaeSolverSystem.Common;
using SlaeSolverSystem.Common.Clients;
using SlaeSolverSystem.Common.Contracts;

namespace SlaeSolverSystem.Client.Wpf.ViewModels;

public class MainViewModel : BaseViewModel
{
	private readonly MasterApiClient _apiClient;
	private readonly DispatcherTimer _timer;
	private readonly IDialogCoordinator _dialogCoordinator; 
	private DateTime _startTime;

	private long _baseTimeT1;
	private string _lastTestName;

	#region Public Properties for Binding

	public bool IsDistributed { get; set; } = false;

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

	// Свойства путей...
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

	public int AvailableWorkers { get; private set; }
	public int TotalWorkers { get; private set; }
	public ObservableCollection<TestResultViewModel> TestResults { get; } = new();

	#endregion

	#region Commands
	// ... (команды те же)
	public ICommand BrowseMatrixCommand { get; }
	public ICommand BrowseVectorCommand { get; }
	public ICommand BrowseNodesCommand { get; }
	public ICommand SaveResultCommand { get; }
	public ICommand ClearResultsCommand { get; }
	public ICommand StartGaussLinearCommand { get; }
	public ICommand StartSeidelLinearCommand { get; }
	public ICommand StartSeidelMultiThreadNoPoolCommand { get; }
	public ICommand StartSeidelMultiThreadPoolCommand { get; }
	public ICommand StartSeidelMultiThreadAsyncCommand { get; }
	public ICommand StartDistributedTestCommand { get; }
	#endregion

	public MainViewModel(IDialogCoordinator dialogCoordinator)
	{
		_dialogCoordinator = dialogCoordinator;
		_apiClient = new MasterApiClient("127.0.0.1", 8001);

		_timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
		_timer.Tick += (s, e) => ElapsedTime = DateTime.Now - _startTime;

		LogMessages.CollectionChanged += (s, e) => {
			LogText = string.Join(Environment.NewLine, LogMessages);
			OnPropertyChanged(nameof(LogText));
		};

		BrowseMatrixCommand = new RelayCommand(_ => BrowseFile(path => MatrixFilePath = path, nameof(MatrixFilePath)), _ => IsNotRunning);
		BrowseVectorCommand = new RelayCommand(_ => BrowseFile(path => VectorFilePath = path, nameof(VectorFilePath)), _ => IsNotRunning);
		BrowseNodesCommand = new RelayCommand(_ => BrowseFile(path => NodesFilePath = path, nameof(NodesFilePath)), _ => IsNotRunning);
		SaveResultCommand = new RelayCommand(_ => PromptToSaveResult(), _ => CanSaveResult);

		ClearResultsCommand = new RelayCommand(_ => {
			TestResults.Clear();
			LogMessages.Clear();
			_baseTimeT1 = 0;
		}, _ => IsNotRunning);

		StartGaussLinearCommand = new RelayCommand(async _ => await StartTest("Гаусс (линейный)", CommandCodes.StartGaussLinear), _ => CanStart());
		StartSeidelLinearCommand = new RelayCommand(async _ => await StartTest("Г-З (1-поток)", CommandCodes.StartSeidelLinear), _ => CanStart()); // <-- ИМЯ ВАЖНО
		StartSeidelMultiThreadPoolCommand = new RelayCommand(async _ => await StartTest("Г-З (ThreadPool)", CommandCodes.StartSeidelMultiThreadPool), _ => CanStart());
		StartSeidelMultiThreadNoPoolCommand = new RelayCommand(async _ => await StartTest("Г-З (Threads)", CommandCodes.StartSeidelMultiThreadNoPool), _ => CanStart());
		StartSeidelMultiThreadAsyncCommand = new RelayCommand(async _ => await StartTest("Г-З (Async)", CommandCodes.StartSeidelMultiThreadAsync), _ => CanStart());
		StartDistributedTestCommand = new RelayCommand(async _ => await StartTest("Распределенный", CommandCodes.StartDistributedCalculation), _ => CanStart());

		SubscribeToApiEvents();
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
		_apiClient.PoolStateReceived += (available, total) => DispatcherInvoke(() => {
			AvailableWorkers = available;
			TotalWorkers = total;
			OnPropertyChanged(nameof(AvailableWorkers));
			OnPropertyChanged(nameof(TotalWorkers));
		});
	}

	#region API Event Handlers

	private void OnLinearCalculationFinished(long time, int matrixSize)
	{
		DispatcherInvoke(() =>
		{
			_baseTimeT1 = time;

			var resultVM = new TestResultViewModel
			{
				TestType = "Гаусс (линейный)",
				MatrixSize = matrixSize,
				TimeMs = time,
				Iterations = 0,
				Resources = 1,
				ResourceType = "Поток",
				Speedup = 1.0,
				Efficiency = 1.0
			};
			AddResultAndFinalize(resultVM);
		});
	}

	private void OnCalculationFinished(CalculationResult result)
	{
		DispatcherInvoke(() =>
		{
			long t1 = _baseTimeT1 > 0 ? _baseTimeT1 : result.ElapsedTime;
			long tn = result.ElapsedTime;
			int n = result.UsedResources; 

			double speedup = (double)t1 / tn; // Sn = T1 / Tn
			double efficiency = speedup / n;  // En = Sn / n

			if (_lastTestName == "Гаусс (линейный)")
			{
				speedup = 1.0;
				efficiency = 1.0;
				n = 1;
			}

			string resourceType = IsDistributed ? "Потоков (Всего на воркерах)" : "Потоков";
			if (_lastTestName.Contains("1-поток") || _lastTestName.Contains("линейный")) resourceType = "Поток";

			var resultVM = new TestResultViewModel
			{
				TestType = IsDistributed ? $"Распред. ({_lastTestName})" : _lastTestName,
				MatrixSize = result.MatrixSize,
				TimeMs = result.ElapsedTime,
				Iterations = result.Iterations,
				Resources = n,
				ResourceType = resourceType,
				Speedup = Math.Round(speedup, 2),
				Efficiency = Math.Round(efficiency, 2)
			};
			AddResultAndFinalize(resultVM);

			_lastSolutionVector = result.SolutionVector;
			var sb = new StringBuilder("Первые 10 элементов вектора x:\n");
			for (int i = 0; i < Math.Min(result.SolutionVector.Length, 10); i++)
				sb.AppendLine($"x[{i}] = {result.SolutionVector[i]:F10}");
			ResultPreviewText = sb.ToString();
			ResultFilePathOnServer = "Результат получен. Нажмите 'Сохранить', чтобы сохранить его в файл.";
			OnPropertyChanged(nameof(CanSaveResult));
		});
	}

	private void AddResultAndFinalize(TestResultViewModel resultVM)
	{
		_timer.Stop();
		TestResults.Add(resultVM);
		IsRunning = false;
		StatusText = "Завершено";
	}

	private void OnCalculationFailed() => DispatcherInvoke(() => FinalizeWithError("Ошибка при выполнении"));

	private void OnDisconnected() => DispatcherInvoke(() => {
		if (IsRunning) FinalizeWithError("Соединение с сервером потеряно");
		else StatusText = "Отключен от Master-сервера";
	});

	private void FinalizeWithError(string status)
	{
		_timer.Stop();
		IsRunning = false;
		StatusText = status;
		LogMessages.Insert(0, $"ОШИБКА: {status}");
	}

	#endregion

	#region Command Methods

	private async Task StartTest(string testName, byte commandCode)
	{
		if (IsRunning) return;

		_lastTestName = testName;

		try
		{
			await _apiClient.ConnectAsync();
			await Task.Delay(100);

			var confirmationMessage = $"Запустить '{testName}'?\n\nДоступно Worker'ов в пуле: {AvailableWorkers} из {TotalWorkers}.";
			var dialogResult = await _dialogCoordinator.ShowMessageAsync(this, "Подтверждение запуска", confirmationMessage, MessageDialogStyle.AffirmativeAndNegative);
			if (dialogResult != MessageDialogResult.Affirmative) return;

			IsRunning = true;
			LogMessages.Insert(0, $"--- {DateTime.Now:HH:mm:ss} ---");
			ResultPreviewText = string.Empty;
			ResultFilePathOnServer = string.Empty;
			Iteration = 0;
			CurrentError = 0;
			_lastSolutionVector = null;

			_startTime = DateTime.Now;
			ElapsedTime = TimeSpan.Zero;
			_timer.Start();

			OnPropertyChanged(nameof(CanSaveResult));

			LogMessages.Insert(0, $"Начало теста '{testName}'...");
			StatusText = "Подключение к Master-серверу...";

			await _apiClient.StartCalculationAsync(commandCode, IsDistributed, MatrixFilePath, VectorFilePath, NodesFilePath, Epsilon, MaxIterations);
			LogMessages.Insert(0, "Задача отправлена. Ожидание ответа от сервера...");
		}
		catch (Exception ex)
		{
			_timer.Stop();
			await _dialogCoordinator.ShowMessageAsync(this, "Ошибка", $"Не удалось запустить тест: {ex.Message}");
			StatusText = "Ошибка подключения";
			IsRunning = false;
		}
	}

	private bool CanStart() => !IsRunning && !string.IsNullOrEmpty(MatrixFilePath) && !string.IsNullOrEmpty(VectorFilePath) && !string.IsNullOrEmpty(NodesFilePath);

	private void BrowseFile(Action<string> setPathAction, string propertyName)
	{
		var ofd = new OpenFileDialog();
		if (ofd.ShowDialog() == true)
		{
			setPathAction(ofd.FileName);
			OnPropertyChanged(propertyName);
		}
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
				_ = _dialogCoordinator.ShowMessageAsync(this, "Успешно", $"Результат сохранен в файл:\n{sfd.FileName}");
			}
			catch (Exception ex)
			{
				_ = _dialogCoordinator.ShowMessageAsync(this, "Ошибка сохранения", $"Не удалось сохранить файл: {ex.Message}");
			}
		}
	}
	#endregion

	private void DispatcherInvoke(Action action) => Application.Current?.Dispatcher.Invoke(action);
}