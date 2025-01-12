using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Runtime.Serialization.Formatters.Binary;
using Domain;
using System.Text;
using System.Threading.Tasks;

namespace Filijala
{
    internal class Program
    {
        private const int ServerPort = 5000;
        private const int ClientPort = 5001;
        private const string ServerIp = "127.0.0.1";

        private readonly Socket _serverSocket;
        private readonly Socket _clientListener;
        private readonly List<Socket> _clientSockets;
        private decimal _maxBudget;
        private int _maxConnections;
        private bool _running;
        private readonly Dictionary<string, User> _connectedUsers;
        private readonly List<Transaction> _transactions;

        public Program()
        {
            _serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _clientListener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _clientSockets = new List<Socket>();
            _connectedUsers = new Dictionary<string, User>();
            _transactions = new List<Transaction>();
        }

        static void Main(string[] args)
        {
            Console.WriteLine("Starting Banking System Branch...");

            Program branch = new Program();
            branch.Start();
        }

        public void Start()
        {
            try
            {
                // Connect to server
                _serverSocket.Connect(new IPEndPoint(IPAddress.Parse(ServerIp), ServerPort));
                Console.WriteLine("Connected to server.");

                // Initialize and get our parameters
                if (!InitializeWithServer())
                {
                    Console.WriteLine("Failed to initialize with server.");
                    return;
                }

                // Start listening for client connections
                _clientListener.Bind(new IPEndPoint(IPAddress.Any, ClientPort));
                _clientListener.Listen(_maxConnections);
                Console.WriteLine($"Branch started on port {ClientPort}, accepting up to {_maxConnections} clients");

                _running = true;

                while (_running)
                {
                    List<Socket> readList = new List<Socket> { _clientListener };
                    readList.AddRange(_clientSockets);

                    Socket.Select(readList, null, null, 1000000); // 1 second timeout

                    foreach (Socket socket in readList)
                    {
                        if (socket == _clientListener)
                        {
                            AcceptClient();
                        }
                        else
                        {
                            HandleClient(socket);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Branch error: {ex.Message}");
            }
            finally
            {
                CleanupConnections();
            }
        }

        private bool InitializeWithServer()
        {
            try
            {
                byte[] buffer = new byte[2048];
                _serverSocket.Receive(buffer);

                var response = (Dictionary<string, string>)DeserializeObject(buffer);

                if (response["success"] == "true")
                {
                    _maxBudget = decimal.Parse(response["maxBudget"]);
                    _maxConnections = int.Parse(response["maxConnections"]);
                    Console.WriteLine($"Initialized with max budget: {_maxBudget}, max connections: {_maxConnections}");
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Initialization error: {ex.Message}");
                return false;
            }
        }

        private void AcceptClient()
        {
            if (_clientSockets.Count >= _maxConnections)
            {
                Console.WriteLine("Maximum connections reached, rejecting new client");
                Socket tempSocket = _clientListener.Accept();
                SendResponse(tempSocket, new Dictionary<string, string>
                {
                    ["success"] = "false",
                    ["message"] = "Branch has reached maximum capacity"
                });
                tempSocket.Close();
                return;
            }

            Socket clientSocket = _clientListener.Accept();
            _clientSockets.Add(clientSocket);
            Console.WriteLine($"Client connected: {clientSocket.RemoteEndPoint}");

            SendResponse(clientSocket, new Dictionary<string, string>
            {
                ["success"] = "true",
                ["message"] = "Connected to branch successfully"
            });
        }

        private void HandleClient(Socket clientSocket)
        {
            try
            {
                byte[] buffer = new byte[2048];
                int received = clientSocket.Receive(buffer);

                if (received == 0)
                {
                    CloseClientSocket(clientSocket);
                    return;
                }

                var request = (Tuple<string, object>)DeserializeObject(buffer);
                Dictionary<string, string> response = ProcessClientRequest(request.Item1, request.Item2, clientSocket);
                SendResponse(clientSocket, response);
            }
            catch (SocketException)
            {
                CloseClientSocket(clientSocket);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling client: {ex.Message}");
                SendResponse(clientSocket, new Dictionary<string, string>
                {
                    ["success"] = "false",
                    ["message"] = "Internal error occurred"
                });
            }
        }

        private Dictionary<string, string> ProcessClientRequest(string type, object data, Socket clientSocket)
        {
            switch (type)
            {
                case "REGISTER":
                    return HandleRegistration((User)data);
                case "LOGIN":
                    return HandleLogin((User)data, clientSocket);
                case "BALANCE":
                    return HandleBalanceCheck((User)data);
                case "DEPOSIT":
                    return HandleDeposit((Transaction)data);
                case "WITHDRAW":
                    return HandleWithdrawal((Transaction)data);
                default:
                    return new Dictionary<string, string>
                    {
                        ["success"] = "false",
                        ["message"] = "Invalid request type"
                    };
            }
        }

        private Dictionary<string, string> HandleRegistration(User user)
        {
            return SendRequest("REGISTER", user);
        }

        private Dictionary<string, string> HandleLogin(User user, Socket clientSocket)
        {
            var response = SendRequest("LOGIN", user);

            if (response["success"] == "true")
            {
                _connectedUsers[user.Username] = user;
            }

            return response;
        }

        private Dictionary<string, string> HandleBalanceCheck(User user)
        {
            if (!_connectedUsers.ContainsKey(user.Username))
            {
                return new Dictionary<string, string>
                {
                    ["success"] = "false",
                    ["message"] = "User not logged in"
                };
            }

            return SendRequest("BALANCE", user);
        }

        private Dictionary<string, string> HandleDeposit(Transaction transaction)
        {
            if (!_connectedUsers.ContainsKey(transaction.Username))
            {
                return new Dictionary<string, string>
                {
                    ["success"] = "false",
                    ["message"] = "User not logged in"
                };
            }

            var validationResponse = ValidateTransaction(transaction);

            if (validationResponse["success"] != "true")
            {
                return validationResponse;
            }

            var response = SendRequest("DEPOSIT", transaction);

            if (response["success"] == "true")
            {
                _maxBudget += transaction.Amount;
                _transactions.Add(transaction);
            }

            return response;
        }

        private Dictionary<string, string> HandleWithdrawal(Transaction transaction)
        {
            if (!_connectedUsers.ContainsKey(transaction.Username))
            {
                return new Dictionary<string, string>
                {
                    ["success"] = "false",
                    ["message"] = "User not logged in"
                };
            }

            if (transaction.Amount > _maxBudget)
            {
                return new Dictionary<string, string>
                {
                    ["success"] = "false",
                    ["message"] = "Insufficient branch budget"
                };
            }

            var validationResponse = ValidateTransaction(transaction);

            if (validationResponse["success"] != "true")
            {
                return validationResponse;
            }

            var response = SendRequest("WITHDRAW", transaction);

            if (response["success"] == "true")
            {
                _maxBudget -= transaction.Amount;
                _transactions.Add(transaction);
            }

            return response;
        }

        private Dictionary<string, string> ValidateTransaction(Transaction transaction)
        {
            return SendRequest("VALIDATE_TRANSACTION", transaction);
        }

        private void SendResponse(Socket socket, Dictionary<string, string> response)
        {
            byte[] responseData = SerializeObject(response);
            socket.Send(responseData);
        }

        private Dictionary<string, string> SendRequest(string type, object data)
        {
            try
            {
                var request = Tuple.Create(type, data);
                byte[] requestData = SerializeObject(request);
                _serverSocket.Send(requestData);

                byte[] buffer = new byte[2048];
                int received = _serverSocket.Receive(buffer);
                var response = (Dictionary<string, string>)DeserializeObject(buffer);
                Console.WriteLine(response["message"]);
                return response;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending request: {ex.Message}");
                return new Dictionary<string, string>
                {
                    ["success"] = "false",
                    ["message"] = ex.Message
                };
            }
        }

        private void CloseClientSocket(Socket socket)
        {
            socket.Close();
            _clientSockets.Remove(socket);
            // Remove user from connected users if found
            var userToRemove = _connectedUsers.FirstOrDefault(x =>
                _clientSockets.All(s => s.RemoteEndPoint?.ToString() != socket.RemoteEndPoint?.ToString()));
            if (!string.IsNullOrEmpty(userToRemove.Key))
            {
                _connectedUsers.Remove(userToRemove.Key);
            }
            Console.WriteLine("Client disconnected");
        }

        private void CleanupConnections()
        {
            foreach (var socket in _clientSockets)
            {
                try
                {
                    socket.Close();
                }
                catch { /* Ignore cleanup errors */ }
            }
            _clientSockets.Clear();
            _connectedUsers.Clear();

            try
            {
                _serverSocket.Close();
                _clientListener.Close();
            }
            catch { /* Ignore cleanup errors */ }
        }

        private static byte[] SerializeObject(object obj)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                BinaryFormatter bf = new BinaryFormatter();
                bf.Serialize(ms, obj);
                return ms.ToArray();
            }
        }

        private static object DeserializeObject(byte[] data)
        {
            using (MemoryStream ms = new MemoryStream(data))
            {
                BinaryFormatter bf = new BinaryFormatter();
                return bf.Deserialize(ms);
            }
        }
    }
}
