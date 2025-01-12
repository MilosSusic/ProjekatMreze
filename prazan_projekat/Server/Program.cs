using Domain;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;

namespace Server
{
     class Program
    {
       
 private const int Port = 5000;
        private readonly Socket _serverSocket;
        private readonly List<Socket> _branchSockets;
        private readonly Dictionary<string, User> _users;
        private readonly List<Transaction> _transactions;
        private bool _running;

        public Program()
        {
            _serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _branchSockets = new List<Socket>();
            _users = new Dictionary<string, User>();
            _transactions = new List<Transaction>();
        }

        static void Main(string[] args)
        {
            Console.WriteLine("Starting Banking System Server...");

            Program server = new Program();
            server.Start();
        }

        public void Start()
        {
            try
            {
                _serverSocket.Bind(new IPEndPoint(IPAddress.Any, Port));
                _serverSocket.Listen(10);
                Console.WriteLine($"Server started on port {Port}");

                _running = true;

                while (_running)
                {
                    List<Socket> readList = new List<Socket> { _serverSocket };
                    readList.AddRange(_branchSockets);

                    Socket.Select(readList, null, null, 1000000); // 1 second timeout

                    foreach (Socket socket in readList)
                    {
                        if (socket == _serverSocket)
                        {
                            AcceptBranch();
                        }
                        else
                        {
                            HandleBranch(socket);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Server error: {ex.Message}");
            }
            finally
            {
                foreach (var socket in _branchSockets)
                {
                    if (socket.Connected)
                        socket.Close();
                }
                if (_serverSocket != null)
                    _serverSocket.Close();
            }
        }

        private void AcceptBranch()
        {
            Socket branchSocket = _serverSocket.Accept();
            _branchSockets.Add(branchSocket);
            Console.WriteLine($"Branch connected: {branchSocket.RemoteEndPoint}");

            // Send initialization response as dictionary
            var response = new Dictionary<string, string>
            {
                ["success"] = "true",
                ["maxBudget"] = "1000000",
                ["maxConnections"] = "5"
            };

            SendResponse(branchSocket, response);
        }

        private void HandleBranch(Socket branchSocket)
        {
            try
            {
                byte[] buffer = new byte[2048];
                int received = branchSocket.Receive(buffer);

                if (received == 0)
                {
                    CloseBranchSocket(branchSocket);
                    return;
                }

                var request = (Tuple<string, object>)DeserializeObject(buffer);
                var response = ProcessBranchRequest(request.Item1, request.Item2);
                SendResponse(branchSocket, response);
            }
            catch (SocketException)
            {
                CloseBranchSocket(branchSocket);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling branch request: {ex.Message}");
                SendResponse(branchSocket, new Dictionary<string, string>
                {
                    ["success"] = "false",
                    ["message"] = "Internal server error"
                });
            }
        }

        private Dictionary<string, string> ProcessBranchRequest(string type, object data)
        {
            try
            {
                switch (type)
                {
                    case "REGISTER":
                        return HandleRegistration((User)data);
                    case "LOGIN":
                        return HandleLogin((User)data);
                    case "BALANCE":
                        return HandleBalanceCheck((User)data);
                    case "VALIDATE_TRANSACTION":
                        return HandleTransactionValidation((Transaction)data);
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
            catch (Exception ex)
            {
                return new Dictionary<string, string>
                {
                    ["success"] = "false",
                    ["message"] = ex.Message
                };
            }
        }

        private Dictionary<string, string> HandleRegistration(User user)
        {
            if (_users.ContainsKey(user.Username))
            {
                return new Dictionary<string, string>
                {
                    ["success"] = "false",
                    ["message"] = "Username already exists"
                };
            }

            _users.Add(user.Username, user);
            Console.WriteLine($"User registered: {user.Username}");
            return new Dictionary<string, string>
            {
                ["success"] = "true",
                ["message"] = "Registration successful",
                ["username"] = user.Username,
                ["accountNumber"] = user.AccountNumber
            };
        }

        private Dictionary<string, string> HandleLogin(User user)
        {
            if (!_users.TryGetValue(user.Username, out var storedUser))
            {
                return new Dictionary<string, string>
                {
                    ["success"] = "false",
                    ["message"] = "User not found"
                };
            }

            if (storedUser.Password != user.Password)
            {
                return new Dictionary<string, string>
                {
                    ["success"] = "false",
                    ["message"] = "Invalid password"
                };
            }

            Console.WriteLine($"User logged in: {user.Username}");
            return new Dictionary<string, string>
            {
                ["success"] = "true",
                ["message"] = "Login successful",
                ["username"] = user.Username,
                ["accountNumber"] = user.AccountNumber
            };
        }

        private Dictionary<string, string> HandleBalanceCheck(User user)
        {
            if (!_users.TryGetValue(user.Username, out var storedUser))
            {
                return new Dictionary<string, string>
                {
                    ["success"] = "false",
                    ["message"] = "User not found"
                };
            }

            return new Dictionary<string, string>
            {
                ["success"] = "true",
                ["message"] = $"Current balance: {storedUser.Balance:C}",
                ["balance"] = storedUser.Balance.ToString()
            };
        }

        private Dictionary<string, string> HandleTransactionValidation(Transaction transaction)
        {
            if (!_users.TryGetValue(transaction.Username, out var user))
            {
                return new Dictionary<string, string>
                {
                    ["success"] = "false",
                    ["message"] = "User not found"
                };
            }

            switch (transaction.Type)
            {
                case TransactionType.Deposit:
                    return new Dictionary<string, string>
                    {
                        ["success"] = "true",
                        ["message"] = "Deposit validated"
                    };
                case TransactionType.Withdrawal:
                    if (transaction.Amount > user.MaxWithdrawalAmount)
                    {
                        return new Dictionary<string, string>
                        {
                            ["success"] = "false",
                            ["message"] = "Amount exceeds maximum withdrawal limit"
                        };
                    }

                    if (transaction.Amount > user.Balance)
                    {
                        return new Dictionary<string, string>
                        {
                            ["success"] = "false",
                            ["message"] = "Insufficient funds"
                        };
                    }

                    return new Dictionary<string, string>
                    {
                        ["success"] = "true",
                        ["message"] = "Withdrawal validated"
                    };
                default:
                    return new Dictionary<string, string>
                    {
                        ["success"] = "false",
                        ["message"] = "Invalid transaction type"
                    };
            }
        }

        private Dictionary<string, string> HandleDeposit(Transaction transaction)
        {
            if (!_users.TryGetValue(transaction.Username, out var user))
            {
                return new Dictionary<string, string>
                {
                    ["success"] = "false",
                    ["message"] = "User not found"
                };
            }

            user.Balance += transaction.Amount;
            _transactions.Add(transaction);

            Console.WriteLine($"Deposit processed: {transaction.Amount:C} for user {transaction.Username}");
            return new Dictionary<string, string>
            {
                ["success"] = "true",
                ["message"] = $"Deposit successful. New balance: {user.Balance:C}",
                ["balance"] = user.Balance.ToString()
            };
        }

        private Dictionary<string, string> HandleWithdrawal(Transaction transaction)
        {
            if (!_users.TryGetValue(transaction.Username, out var user))
            {
                return new Dictionary<string, string>
                {
                    ["success"] = "false",
                    ["message"] = "User not found"
                };
            }

            if (transaction.Amount > user.MaxWithdrawalAmount)
            {
                return new Dictionary<string, string>
                {
                    ["success"] = "false",
                    ["message"] = "Amount exceeds maximum withdrawal limit"
                };
            }

            if (transaction.Amount > user.Balance)
            {
                return new Dictionary<string, string>
                {
                    ["success"] = "false",
                    ["message"] = "Insufficient funds"
                };
            }

            user.Balance -= transaction.Amount;
            _transactions.Add(transaction);

            Console.WriteLine($"Withdrawal processed: {transaction.Amount:C} for user {transaction.Username}");
            return new Dictionary<string, string>
            {
                ["success"] = "true",
                ["message"] = $"Withdrawal successful. New balance: {user.Balance:C}",
                ["balance"] = user.Balance.ToString()
            };
        }

        private void SendResponse(Socket socket, object response)
        {
            byte[] responseData = SerializeObject(response);
            socket.Send(responseData);
        }

        private void CloseBranchSocket(Socket socket)
        {
            socket.Close();
            _branchSockets.Remove(socket);
            Console.WriteLine("Branch disconnected");
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


