using Newtonsoft.Json.Linq;
using Server.Plugins.Configuration;
using Server.Users;
using Stormancer;
using Stormancer.Core;
using Stormancer.Diagnostics;
using Stormancer.Server.Components;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Server.Management;
using System.Diagnostics;

namespace Server.Plugins.GameSession
{
    public enum ServerStatus
    {
        WaitingPlayers = 0,
        AllPlayersConnected = 1,
        Starting = 2,
        Started = 3,
        Shutdown = 4,
        Faulted = 5
    }

    public enum PlayerStatus
    {
        NotConnected = 0,
        Connected = 1,
        Ready = 2,
        Faulted = 3,
        Disconnected = 4
    }

    internal class GameSessionService : IGameSessionService
    {
        private const string p2pTokenRoute = "player.p2ptoken";

        private readonly IUserSessions _sessions;
        private readonly IConfiguration _configuration;
        private readonly ILogger _logger;
        private readonly ISceneHost _scene;
        private readonly IEnvironment _environment;
        private readonly IDelegatedTransports _pools;
        private readonly Func<IEnumerable<IGameSessionEventHandler>> _eventHandlers;

        private GameSessionConfiguration _config;

        private IDisposable _portLease;

        private System.Diagnostics.Process _gameServerProcess;
        private byte[] _serverGuid;

        private class Client
        {
            public Client(IScenePeerClient peer)
            {
                Peer = peer;
                Reset();
            }

            public void Reset()
            {
                GameCompleteTcs?.TrySetCanceled();
                GameCompleteTcs = new TaskCompletionSource<Action<Stream, ISerializer>>();
                ResultData = null;
            }
            public IScenePeerClient Peer { get; set; }

            public Stream ResultData { get; set; }

            public PlayerStatus Status { get; set; }

            public string FaultReason { get; set; }

            public TaskCompletionSource<Action<Stream, ISerializer>> GameCompleteTcs { get; private set; }
        }
        private ConcurrentDictionary<string, Client> _clients = new ConcurrentDictionary<string, Client>();
        private ServerStatus _status = ServerStatus.WaitingPlayers;

        private string _ip = "";
        private ushort _port;
        private string _p2pToken;

        public GameSessionService(
            ISceneHost scene,
            IUserSessions sessions,
            IConfiguration configuration,
            IEnvironment environment,
            IDelegatedTransports pools,
            ManagementClientAccessor management,
            ILogger logger,
            Func<IEnumerable<IGameSessionEventHandler>> eventHandlers)
        {
            _management = management;
            _scene = scene;
            _sessions = sessions;
            _configuration = configuration;
            _logger = logger;
            _environment = environment;
            _pools = pools;

            _eventHandlers = eventHandlers;

            scene.Shuttingdown.Add(args =>
            {
                _logger.Log(LogLevel.Trace, "gameserver", $"Shutting down gamesession scene {_scene.Id}.", new { _scene.Id, Port = _port });

                try
                {
                    if (_gameServerProcess != null && !_gameServerProcess.HasExited)
                    {
                        _gameServerProcess.Close();
                        _gameServerProcess = null;
                    }
                }
                catch { }
                finally
                {
                    _portLease?.Dispose();
                }
                _logger.Log(LogLevel.Trace, "gameserver", $"gamesession scene {_scene.Id} shut down.", new
                {
                    _scene.Id,
                    Port = _port
                });

                return Task.FromResult(true);
            });
            scene.Connecting.Add(this.PeerConnecting);
            scene.Connected.Add(this.PeerConnected);
            scene.Disconnected.Add((args) => this.PeerDisconnecting(args.Peer));
            scene.AddRoute("player.ready", ReceivedReady, _ => _);
            scene.AddRoute("player.faulted", ReceivedFaulted, _ => _);
        }


        private async Task ReceivedReady(Packet<IScenePeerClient> packet)
        {
            try
            {
                var peer = packet.Connection;
                if (peer == null)
                {
                    throw new ArgumentNullException("peer");
                }
                if (peer.ContentType == "application/octet-stream")
                {
                    var peerGuid = new Guid(peer.UserData);
                    var serverGuid = new Guid(_serverGuid);
                    if (serverGuid == peerGuid)
                    {
                        await SignalServerReady(peer.Id);
                        return;
                    }

                }

                var user = await _sessions.GetUser(peer);

                if (user == null)
                {
                    throw new InvalidOperationException("Unauthenticated peer.");
                }

                Client currentClient;
                if (!_clients.TryGetValue(user.Id, out currentClient))
                {
                    throw new InvalidOperationException("Unknown client.");
                }

                _logger.Log(LogLevel.Trace, "gamesession", "received a ready message from an user.", new { userId = user.Id, currentClient.Status });

                if (currentClient.Status < PlayerStatus.Ready)
                {
                    currentClient.Status = PlayerStatus.Ready;

                    BroadcastClientUpdate(currentClient, user.Id, packet.ReadObject<string>());
                }

                if (user.Id == _config.hostUserId && (((bool?)_configuration.Settings.gameSession?.usep2p) == true))
                {
                    var p2pToken = await _scene.DependencyResolver.Resolve<IPeerInfosService>().CreateP2pToken(peer.Id);

                    _p2pToken = p2pToken;

                    foreach (var p in _scene.RemotePeers.Where(p => p != peer))
                    {
                        p.Send(p2pTokenRoute, p2pToken);
                    }
                }
                await TryStart();
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Error, "gamesession", "an error occurred while receiving a ready message", ex);
                throw;
            }
        }

        private void BroadcastClientUpdate(Client client, string userId, string data = null)
        {
            _scene.Broadcast("player.update", new PlayerUpdate { UserId = userId, Status = (byte)client.Status, Data = data ?? "" }, PacketPriority.MEDIUM_PRIORITY, PacketReliability.RELIABLE_ORDERED);
        }

        private async Task ReceivedFaulted(Packet<IScenePeerClient> packet)
        {
            var peer = packet.Connection;
            if (peer == null)
            {
                throw new ArgumentNullException("peer");
            }
            var user = await _sessions.GetUser(peer);

            if (user == null)
            {
                throw new InvalidOperationException("Unauthenticated peer.");
            }

            Client currentClient;
            if (!_clients.TryGetValue(user.Id, out currentClient))
            {
                throw new InvalidOperationException("Unknown client.");
            }

            var reason = packet.ReadObject<string>();
            currentClient.Status = PlayerStatus.Faulted;

            if (this._status == ServerStatus.WaitingPlayers
                || this._status == ServerStatus.AllPlayersConnected)
            {
                this._status = ServerStatus.Faulted;

                // TODO
            }
            // TODO
        }

        public void SetConfiguration(dynamic metadata)
        {
            if (metadata.gameSession != null)
            {
                _config = ((JObject)metadata.gameSession).ToObject<GameSessionConfiguration>();
            }
        }


        private async Task PeerConnecting(IScenePeerClient peer)
        {
            if (peer.ContentType == "application/octet-stream")
            {
                var peerGuid = new Guid(peer.UserData);
                var serverGuid = new Guid(_serverGuid);
                if (serverGuid == peerGuid)
                {
                    return;
                }
                else
                {
                    throw new ClientException("Failed to authenticate as dedicated server");
                }
            }
            if (peer == null)
            {
                throw new ArgumentNullException("peer");
            }
            var user = await _sessions.GetUser(peer);

            if (user == null)
            {
                throw new ClientException("You are not authenticated.");
            }

            if (_config == null)
            {
                throw new InvalidOperationException("Game session plugin configuration missing in scene instance metadata. Please check the scene creation process.");
            }
            if (!_config.userIds.Contains(user.Id) && !_config.Public)
            {
                throw new ClientException("You are not authorized to join this game.");
            }

            var client = new Client(peer);

            if (!_clients.TryAdd(user.Id, client))
            {
                Client alreadyConnectedClient;
                if (_clients.TryGetValue(user.Id, out alreadyConnectedClient) && alreadyConnectedClient.Status != PlayerStatus.Disconnected && !_clients.TryUpdate(user.Id, client, alreadyConnectedClient))
                {
                    throw new ClientException("Failed to add player to the game session.");
                }

            }

            client.Status = PlayerStatus.Connected;
            if (!_config.Public)
            {
                BroadcastClientUpdate(client, user.Id);
            }

        }

        private async Task SignalServerReady(long peerId)
        {
            _p2pToken = await _scene.DependencyResolver.Resolve<IPeerInfosService>().CreateP2pToken(peerId);
            _logger.Log(LogLevel.Trace, "gameserver", "Server responded as ready.", new { Port = _port });
            _scene.Broadcast("server.started", new GameServerStartMessage { P2PToken = _p2pToken });
            _status = ServerStatus.Started;
        }

        private async Task PeerConnected(IScenePeerClient peer)
        {
            if (peer.ContentType == "application/octet-stream")
            {
                var peerGuid = new Guid(peer.UserData);
                var serverGuid = new Guid(_serverGuid);
                if (serverGuid == peerGuid)
                {
                    _serverPeer = peer;
                    return;
                }
            }
            if (peer == null)
            {
                throw new ArgumentNullException("peer");
            }
            var user = await _sessions.GetUser(peer);

            if (user == null)
            {
                throw new ClientException("You are not authenticated.");
            }

            foreach (var uId in _clients.Keys)
            {
                if (uId != user.Id)
                {
                    var currentClient = _clients[uId];
                    peer.Send("player.update",
                        new PlayerUpdate { UserId = uId, Status = (byte)currentClient.Status, Data = currentClient.FaultReason ?? "" },
                        PacketPriority.MEDIUM_PRIORITY, PacketReliability.RELIABLE_ORDERED);
                }
            }
            if (_status == ServerStatus.Started)
            {
                peer.Send("server.started", new GameServerStartMessage { P2PToken = _p2pToken });
            }
        }

        private AsyncLock _lock = new AsyncLock();
        private readonly ManagementClientAccessor _management;
        private IScenePeerClient _serverPeer;

        public async Task TryStart()
        {
            Debugger.Break();
            using (await _lock.LockAsync())
            {
                if ((_config.userIds.All(id => _clients.Keys.Contains(id)) && _clients.Values.All(client => client.Status == PlayerStatus.Ready) || _config.Public) && _status == ServerStatus.WaitingPlayers)
                {
                    _status = ServerStatus.Starting;
                    _logger.Log(LogLevel.Trace, "gamesession", "Starting game session.", new { });
                    await Start();
                    _logger.Log(LogLevel.Trace, "gamesession", "Game session started.", new { });
                    var ctx = new GameSessionStartedCtx(_scene, _clients.Select(kvp => new Player(kvp.Value.Peer, kvp.Key)));
                    await _eventHandlers()?.RunEventHandler(eh => eh.GameSessionStarted(ctx), ex => _logger.Log(LogLevel.Error, "gameSession", "An error occured while running gameSession.Started event handlers", ex));
                }
            }
        }

        private async Task Start()
        {
            Debugger.Break();
            var serverEnabled = ((JToken)_configuration?.Settings?.gameServer) != null;
            var path = (string)_configuration.Settings?.gameServer?.executable;
            var verbose = ((bool?)_configuration.Settings?.gameServer?.verbose) ?? false;
            var log = ((bool?)_configuration.Settings?.gameServer?.log) ?? false;

            if (!serverEnabled)
            {
                _logger.Log(LogLevel.Trace, "gamesession", "No server executable enabled. Game session started.", new { });
                _status = ServerStatus.Started;
                return;
            }

            try
            {

                if (path == null)
                {
                    throw new InvalidOperationException("Missing 'gameServer.executable' configuration value");
                }

                if (path == "dummy")
                {
                    _logger.Log(LogLevel.Trace, "gameserver", "Using dummy: no executable server available.", new { });
                    try
                    {
                        await LeaseServerPort();

                        await Task.Delay(TimeSpan.FromSeconds(5));

                        _status = ServerStatus.Started;
                        var gameStartMessage = new GameServerStartMessage { P2PToken = null };
                        _logger.Log(LogLevel.Trace, "gameserver", "Dummy server started, sending server.started message to connected players.", gameStartMessage);
                        _scene.Broadcast("server.started", gameStartMessage);
                    }
                    catch (Exception ex)
                    {
                        _logger.Log(LogLevel.Error, "gameserver", "An error occurred while trying to lease a port", ex);
                        throw;
                    }

                    return;
                }

                var prc = new System.Diagnostics.Process();
                Debugger.Break();            
                await LeaseServerPort();
                // TODO Get port


                var managementClient = await _management.GetApplicationClient();
                _serverGuid = Guid.NewGuid().ToByteArray();
                var token = await managementClient.CreateConnectionToken(_scene.Id, _serverGuid, "application/octet-stream");
                prc.StartInfo.Arguments = $"PORT={_port.ToString()} { (log ? "-log" : "")}";//$"-port={_port} {(log ? "-log" : "")}";
                // TODO give port in arg to server
                prc.StartInfo.FileName = path;
                prc.StartInfo.CreateNoWindow = false;
                prc.StartInfo.UseShellExecute = false;
                prc.EnableRaisingEvents = true;
                //prc.StartInfo.RedirectStandardOutput = true;
                //prc.StartInfo.RedirectStandardError = true;
                prc.StartInfo.EnvironmentVariables.Add("connectionToken", token);
                //prc.StartInfo.EnvironmentVariables.Add("P2Pport", _port.ToString());
                // TODO 
                _logger.Log(LogLevel.Debug, "gameserver", $"Starting server {prc.StartInfo.FileName} with args {prc.StartInfo.Arguments}", new { env = prc.StartInfo.EnvironmentVariables });


                //prc.OutputDataReceived += (sender, args) =>
                //{
                //    if (verbose)
                //    {
                //        _logger.Log(LogLevel.Trace, "gameserver", "Received data output from Intrepid server.", new { args.Data });
                //    }


                //};
                //prc.ErrorDataReceived += (sender, args) =>
                //  {
                //      _logger.Error("gameserver", $"An error occured while trying to start the game server : '{args.Data}'");
                //  };

                prc.Exited += (sender, args) =>
                {
                    _p2pToken = null;
                    _logger.Error("gameserver", "Server stopped");
                    _status = ServerStatus.Shutdown;
                    foreach (var client in _clients.Values)
                    {

                        client.Peer?.Disconnect("Game server stopped");
                    }
                    if (_config.canRestart)
                    {
                        _status = ServerStatus.WaitingPlayers;
                        
                        Reset();
                    }
                };
                Debugger.Break();
                _gameServerProcess = prc;
                bool sucess = prc.Start();

                if (sucess) 
                    _logger.Log(LogLevel.Debug, "gameserver", "Starting process success ", "");
                else
                    _logger.Log(LogLevel.Debug, "gameserver", "Starting process failed ", "");
                //prc.BeginErrorReadLine();
                //prc.BeginOutputReadLine();


            }
            catch (Exception ex)
            {            
                _logger.Log(LogLevel.Error, "gameserver", "Failed to start server.", ex);
                if (_config.canRestart)
                {
                    _status = ServerStatus.WaitingPlayers;
                    await Reset();
                }
                else
                {
                    _status = ServerStatus.Shutdown;
                }              
                foreach (var client in _clients.Values)
                {
                    await client.Peer.Disconnect("Game server stopped");
                }
            }
        }

        private async Task LeaseServerPort()
        {


            var lease = await _pools.AcquirePort((string)_configuration.Settings?.gameServer?.transport ?? "public1");
            if (!lease.Success)
            {

                throw new InvalidOperationException("Unable to acquire port for the server");
            }
            _portLease = lease;
            _port = lease.Port;
            _ip = lease.PublicIp;

        }

        public async Task PeerDisconnecting(IScenePeerClient peer)
        {
            if (peer == null)
            {
                throw new ArgumentNullException("peer");
            }
            var user = await _sessions.GetUser(peer);

            Client client = null;
            string userId = null;
            if (user != null)
            {
                userId = user.Id;
                client = _clients[user.Id];
                if (userId == _config.hostUserId)
                {
                    _p2pToken = null;
                }
            }
            else
            {
                // the peer disconnected from the app and is not in the sessions anymore.
                foreach (var kvp in _clients)
                {
                    if (kvp.Value.Peer == peer)
                    {
                        userId = kvp.Key;

                        if (_config.Public)
                        {
                            _clients.TryRemove(userId, out client);
                        }
                        // no need to continue searching for the client, we already found it
                        break;
                    }
                }
            }

            if (client != null)
            {

                client.Peer = null;
                client.Status = PlayerStatus.Disconnected;

                BroadcastClientUpdate(client, userId);
                await EvaluateGameComplete();
            }


            if (!_clients.Values.Any(c => c.Status != PlayerStatus.Disconnected))
            {
                if (_gameServerProcess != null && !_gameServerProcess.HasExited)
                {
                    _logger.Log(LogLevel.Info, "gameserver", $"Closing down game server for scene {_scene.Id}.", new { prcId = _gameServerProcess.Id });
                    _serverPeer.Send("gameSession.shutdown", s => { }, PacketPriority.MEDIUM_PRIORITY, PacketReliability.RELIABLE);
                    //_gameServerProcess.Close();
                    await Task.Delay(10000);
                    if(!_gameServerProcess.HasExited)
                    {
                        _logger.Log(LogLevel.Error, "gameserver", $"Failed to close dedicated server. Killing it instead. The server should shutdown when receiving a message on the 'gameSession.shutdown' route.", new { prcId = _gameServerProcess.Id });
                        _gameServerProcess.Kill();
                    }
                    _gameServerProcess = null;

                }
                _portLease?.Dispose();

                _logger.Log(LogLevel.Trace, "gameserver", $"Game server for scene {_scene.Id} shut down.", new { _scene.Id, Port = _port });
            }

        }
        public Task Reset()
        {
            foreach (var client in _clients.Values)
            {
                client.Reset();
            }
            return Tasks.Empty;
        }
        public async Task<Action<Stream, ISerializer>> PostResults(Stream inputStream, IScenePeerClient remotePeer)
        {
            if (this._status != ServerStatus.Started)
            {
                throw new ClientException($"Unable to post result before game session start. Server status is {this._status}");
            }
            var user = await _sessions.GetUser(remotePeer);
            _clients[user.Id].ResultData = inputStream;

            await EvaluateGameComplete();
            return await _clients[user.Id].GameCompleteTcs.Task;
        }

        private async Task EvaluateGameComplete()
        {
            using (await _lock.LockAsync())
            {
                if (_clients.Values.All(c => c.ResultData != null || c.Peer == null))//All remaining clients sent their data
                {
                    var ctx = new GameSessionCompleteCtx(_scene, _clients.Select(kvp => new GameSessionResult(kvp.Key, kvp.Value.Peer, kvp.Value.ResultData)), _clients.Keys);

                    await _eventHandlers()?.RunEventHandler(eh => eh.GameSessionCompleted(ctx), ex =>
                    {
                        _logger.Log(LogLevel.Error, "gameSession", "An error occured while running gameSession.GameSessionCompleted event handlers", ex);
                        foreach (var client in _clients.Values)
                        {
                            client.GameCompleteTcs.TrySetException(ex);
                        }
                    });

                    foreach (var client in _clients.Values)
                    {
                        client.GameCompleteTcs.TrySetResult(ctx.ResultsWriter);
                    }
                }
            }
        }
    }
}
