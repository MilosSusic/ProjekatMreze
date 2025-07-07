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
        private readonly Dictionary<string, Korisnik> _korisnici;
        private readonly List<Transakcija> _transakcije;
        private bool _prokrenutaAplikacija;

        public Program()
        {
            _serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _korisnici = new Dictionary<string, Korisnik>();
            _transakcije = new List<Transakcija>();
        }

        static void Main(string[] args)
        {
            Console.WriteLine("Pokretanje Servera...");

            Program server = new Program();
            server.Start();
        }

        public void Start()
        {
            try
            {
                _serverSocket.Bind(new IPEndPoint(IPAddress.Any, Port));
                Console.WriteLine($"Server pokrenut na portu {Port}");

                _prokrenutaAplikacija = true;

                while (_prokrenutaAplikacija)
                {
                    try
                    {
                        byte[] buffer = new byte[2048];
                        EndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);

                        int bytesReceived = _serverSocket.ReceiveFrom(buffer, ref remoteEndPoint);

                        if (bytesReceived > 0)
                        {
                            var zahtev = (Tuple<string, object>)DeserializeObject(buffer);
                            var odgovor = ObradiZahtevFilijale(zahtev.Item1, zahtev.Item2);
                            PosaljiOdgovor(_serverSocket, odgovor, remoteEndPoint);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
            finally
            {
                if (_serverSocket != null)
                    _serverSocket.Close();
            }
        }

        private Dictionary<string, string> ObradiZahtevFilijale(string type, object data)
        {
            try
            {
                switch (type)
                {
                    case "INIT":
                        return ObradiInicijalizaciju();
                    case "REGISTER":
                        return ObradiRegistraciju((Korisnik)data);
                    case "LOGIN":
                        return ObradiLogin((Korisnik)data);
                    case "BALANCE":
                        return ObradiProveruStanja((Korisnik)data);
                    case "VALIDATE_TRANSACTION":
                        return ObradiValidnostTransakcije((Transakcija)data);
                    case "DEPOSIT":
                        return ObradiUplatu((Transakcija)data);
                    case "WITHDRAW":
                        return ObradiIsplatu((Transakcija)data);
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

        private Dictionary<string, string> ObradiInicijalizaciju()
        {
            return new Dictionary<string, string>
            {
                ["success"] = "true",
                ["maxBudget"] = "1000000",
                ["maxConnections"] = "5"
            };
        }

        private Dictionary<string, string> ObradiRegistraciju(Korisnik korisnik)
        {
            if (_korisnici.ContainsKey(korisnik.KorisnickoIme))
            {
                return new Dictionary<string, string>
                {
                    ["success"] = "false",
                    ["message"] = "Username already exists"
                };
            }

            _korisnici.Add(korisnik.KorisnickoIme, korisnik);
            Console.WriteLine($"Korisnik registrovan: {korisnik.KorisnickoIme}");
            return new Dictionary<string, string>
            {
                ["success"] = "true",
                ["message"] = "Registration successful",
                ["username"] = korisnik.KorisnickoIme,
                ["accountNumber"] = korisnik.BrojRacuna
            };
        }

        private Dictionary<string, string> ObradiLogin(Korisnik korisnik)
        {
            if (!_korisnici.TryGetValue(korisnik.KorisnickoIme, out var storedUser))
            {
                return new Dictionary<string, string>
                {
                    ["success"] = "false",
                    ["message"] = "User not found"
                };
            }

            if (storedUser.Sifra != korisnik.Sifra)
            {
                return new Dictionary<string, string>
                {
                    ["success"] = "false",
                    ["message"] = "Invalid password"
                };
            }

            Console.WriteLine($"Korisnik prijavljen: {korisnik.KorisnickoIme}");
            return new Dictionary<string, string>
            {
                ["success"] = "true",
                ["message"] = "Login successful",
                ["username"] = korisnik.KorisnickoIme,
                ["accountNumber"] = korisnik.BrojRacuna
            };
        }

        private Dictionary<string, string> ObradiProveruStanja(Korisnik korisnik)
        {
            if (!_korisnici.TryGetValue(korisnik.KorisnickoIme, out var storedUser))
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
                ["message"] = $"Current balance: {storedUser.Stanje:C}",
                ["balance"] = storedUser.Stanje.ToString()
            };
        }

        private Dictionary<string, string> ObradiValidnostTransakcije(Transakcija transakcija)
        {
            if (!_korisnici.TryGetValue(transakcija.KorisnickoIme, out var user))
            {
                return new Dictionary<string, string>
                {
                    ["success"] = "false",
                    ["message"] = "User not found"
                };
            }

            switch (transakcija.Tip)
            {
                case TipTransakcije.Uplata:
                    return new Dictionary<string, string>
                    {
                        ["success"] = "true",
                        ["message"] = "Deposit validated"
                    };
                case TipTransakcije.Isplata:
                    if (transakcija.Kolicina > user.MaxSumaZaIsplatu)
                    {
                        return new Dictionary<string, string>
                        {
                            ["success"] = "false",
                            ["message"] = "Amount exceeds maximum withdrawal limit"
                        };
                    }

                    if (transakcija.Kolicina > user.Stanje)
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

        private Dictionary<string, string> ObradiUplatu(Transakcija transakcija)
        {
            if (!_korisnici.TryGetValue(transakcija.KorisnickoIme, out var user))
            {
                return new Dictionary<string, string>
                {
                    ["success"] = "false",
                    ["message"] = "User not found"
                };
            }

            user.Stanje += transakcija.Kolicina;
            _transakcije.Add(transakcija);

            Console.WriteLine($"Uplata: {transakcija.Kolicina:C} za {transakcija.KorisnickoIme}");
            return new Dictionary<string, string>
            {
                ["success"] = "true",
                ["message"] = $"Deposit successful. New balance: {user.Stanje:C}",
                ["balance"] = user.Stanje.ToString()
            };
        }

        private Dictionary<string, string> ObradiIsplatu(Transakcija transakcija)
        {
            if (!_korisnici.TryGetValue(transakcija.KorisnickoIme, out var user))
            {
                return new Dictionary<string, string>
                {
                    ["success"] = "false",
                    ["message"] = "User not found"
                };
            }

            if (transakcija.Kolicina > user.MaxSumaZaIsplatu)
            {
                return new Dictionary<string, string>
                {
                    ["success"] = "false",
                    ["message"] = "Amount exceeds maximum withdrawal limit"
                };
            }

            if (transakcija.Kolicina > user.Stanje)
            {
                return new Dictionary<string, string>
                {
                    ["success"] = "false",
                    ["message"] = "Insufficient funds"
                };
            }

            user.Stanje -= transakcija.Kolicina;
            _transakcije.Add(transakcija);

            Console.WriteLine($"Isplata: {transakcija.Kolicina:C} za {transakcija.KorisnickoIme}");
            return new Dictionary<string, string>
            {
                ["success"] = "true",
                ["message"] = $"Withdrawal successful. New balance: {user.Stanje:C}",
                ["balance"] = user.Stanje.ToString()
            };
        }

        private void PosaljiOdgovor(Socket socket, object response, EndPoint remoteEndPoint)
        {
            byte[] responseData = SerializeObject(response);
            socket.SendTo(responseData, remoteEndPoint);
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

        private static object DeserializeObject(byte[] podaci)
        {
            using (MemoryStream ms = new MemoryStream(podaci))
            {
                BinaryFormatter bf = new BinaryFormatter();
                return bf.Deserialize(ms);
            }
        }
    }
}


