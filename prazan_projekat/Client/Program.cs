using Domain;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;

namespace Client
{
     class Program
    { 
        private const int BranchPort = 5001;
        private const string BranchIp = "127.0.0.1";
        private Socket _branchSocket;
        private bool _isConnected;
        private User _currentUser;

        public Program()
        {
            _branchSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _currentUser = null;
        }

        static void Main(string[] args)
        {

            Console.WriteLine("Starting Banking System Client...");

            Program client = new Program();

            client.Start();

        }

        public void Start()
        {
            try
            {
                ConnectToBranch();

                while (true)
                {
                    if (!_isConnected)
                    {
                        Console.WriteLine("Not connected to branch. Press Enter to try reconnecting or 'exit' to quit.");
                        if (Console.ReadLine()?.ToLower() == "exit")
                            break;
                        ConnectToBranch();
                        continue;
                    }

                    ShowMenu();
                    string choice = Console.ReadLine();

                    switch (choice)
                    {
                        case "1":
                            Register();
                            break;
                        case "2":
                            Login();
                            break;
                        case "3":
                            if (_currentUser != null)
                                CheckBalance();
                            else
                                Console.WriteLine("Please login first.");
                            break;
                        case "4":
                            if (_currentUser != null)
                                Deposit();
                            else
                                Console.WriteLine("Please login first.");
                            break;
                        case "5":
                            if (_currentUser != null)
                                Withdraw();
                            else
                                Console.WriteLine("Please login first.");
                            break;
                        case "6":
                            _isConnected = false;
                            break;
                        default:
                            Console.WriteLine("Invalid choice.");
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Client error: {ex.Message}");
            }
            finally
            {
                if (_branchSocket.Connected)
                    _branchSocket.Close();
            }
        }

        private void ShowMenu()
        {
            Console.WriteLine("\n=== Banking System Client ===");
            Console.WriteLine("1. Register");
            Console.WriteLine("2. Login");
            Console.WriteLine("3. Check Balance");
            Console.WriteLine("4. Deposit");
            Console.WriteLine("5. Withdraw");
            Console.WriteLine("6. Exit");
            Console.Write("Choose an option: ");
        }

        private void ConnectToBranch()
        {
            try
            {
                if (_branchSocket.Connected)
                    _branchSocket.Close();

                _branchSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                _branchSocket.Connect(new IPEndPoint(IPAddress.Parse(BranchIp), BranchPort));

                byte[] buffer = new byte[2048];
                int received = _branchSocket.Receive(buffer);
                var response = (Dictionary<string, string>)DeserializeObject(buffer);

                if (response["success"] != "true")
                {
                    Console.WriteLine($"Branch rejected connection: {response["message"]}");
                    _isConnected = false;
                    return;
                }

                _isConnected = true;
                Console.WriteLine("Connected to branch successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Connection error: {ex.Message}");
                _isConnected = false;
            }
        }

        private void Register()
        {
            Console.Write("Enter username: ");
            string username = Console.ReadLine();
            Console.Write("Enter password: ");
            string password = Console.ReadLine();
            Console.Write("Enter first name: ");
            string firstName = Console.ReadLine();
            Console.Write("Enter last name: ");
            string lastName = Console.ReadLine();
            Console.Write("Enter maximum withdrawal amount: ");
            if (!decimal.TryParse(Console.ReadLine(), out decimal maxWithdrawal))
            {
                Console.WriteLine("Invalid amount.");
                return;
            }

            User user = new User
            {
                Username = username,
                Password = password,
                FirstName = firstName,
                LastName = lastName,
                MaxWithdrawalAmount = maxWithdrawal
            };

            SendRequest("REGISTER", user);
        }

        private void Login()
        {
            Console.Write("Enter username: ");
            string username = Console.ReadLine();
            Console.Write("Enter password: ");
            string password = Console.ReadLine();

            User user = new User
            {
                Username = username,
                Password = password
            };

            var response = SendRequest("LOGIN", user);
            if (response["success"] == "true")
            {
                _currentUser = user;
            }
        }

        private void CheckBalance()
        {
            SendRequest("BALANCE", _currentUser);
        }

        private void Deposit()
        {
            Console.Write("Enter amount to deposit: ");
            if (!decimal.TryParse(Console.ReadLine(), out decimal amount))
            {
                Console.WriteLine("Invalid amount.");
                return;
            }

            Transaction transaction = new Transaction(_currentUser.Username, amount, TransactionType.Deposit);

            SendRequest("DEPOSIT", transaction);
        }

        private void Withdraw()
        {
            Console.Write("Enter amount to withdraw: ");
            if (!decimal.TryParse(Console.ReadLine(), out decimal amount))
            {
                Console.WriteLine("Invalid amount.");
                return;
            }

            Transaction transaction = new Transaction(_currentUser.Username, amount, TransactionType.Withdrawal);

            SendRequest("WITHDRAW", transaction);
        }

        private Dictionary<string, string> SendRequest(string type, object data)
        {
            try
            {
                var request = Tuple.Create(type, data);
                byte[] requestData = SerializeObject(request);
                _branchSocket.Send(requestData);

                byte[] buffer = new byte[2048];
                int received = _branchSocket.Receive(buffer);
                var response = (Dictionary<string, string>)DeserializeObject(buffer);
                Console.WriteLine(response["message"]);
                return response;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending request: {ex.Message}");
                _isConnected = false;
                return new Dictionary<string, string>
                {
                    ["success"] = "false",
                    ["message"] = ex.Message
                };
            }
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


