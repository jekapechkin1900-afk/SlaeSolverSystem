using SlaeSolverSystem.Master;

var master = new MasterApp(workerPort: 8000, guiPort: 8001);
master.Start();
await Task.Delay(-1);