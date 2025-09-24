using SlaeSolverSystem.Worker;

var masterIp = "127.0.0.1";
var masterPort = 8000;

var app = new WorkerApp(masterIp, masterPort);
await app.StartAsync();
