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
    class Program
    {
        private const string ServerIp = "192.168.0.15";
        private const int ServerPort = 5000;
        private const int DefaultFilijalaPort = 5001;

        private readonly Socket _serverSocket;
        private readonly Socket _klijentListener;
        private readonly List<Socket> _klijentSockets;
        private decimal _maxBudzet;
        private int _maxKonekcija;
        private bool _radi;
        private readonly Dictionary<string, Korisnik> _povezaniKorisnici;
        private readonly List<Transakcija> _transakcije;
        private readonly List<TransferTransakcija> _transferTransakcije;
        private EndPoint _serverEndPoint;
        private int _filijalaPort;

        public Program()
        {
            _serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _klijentListener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _klijentSockets = new List<Socket>();
            _povezaniKorisnici = new Dictionary<string, Korisnik>();
            _transakcije = new List<Transakcija>();
            _transferTransakcije = new List<TransferTransakcija>();
            _serverEndPoint = new IPEndPoint(IPAddress.Parse(ServerIp), ServerPort);
        }

        private int PronadjiSlobodniPort(int startPort)
        {
            for (var port = startPort; port < startPort + 100; port++)
            {
                try
                {
                    using (var testSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
                    {
                        testSocket.Bind(new IPEndPoint(IPAddress.Any, port));
                        testSocket.Close();
                        return port;
                    }
                }
                catch (SocketException)
                {
                    // Port zauzet, predji na sledeci
                }
            }

            throw new InvalidOperationException($"No available ports found starting from {startPort}");
        }

        static void Main(string[] args)
        {
            Console.WriteLine("Pokretanje filijale...");

            Program filijala = new Program();
            filijala.Start();
        }

        public void Start()
        {
            try
            {
                if (!InicijalizacijaSaServerom())
                {
                    Console.WriteLine("Inicijalizacija sa serverom nije uspela.");
                    return;
                }

                _filijalaPort = PronadjiSlobodniPort(DefaultFilijalaPort);
                _klijentListener.Bind(new IPEndPoint(IPAddress.Any, _filijalaPort));
                _klijentListener.Listen(_maxKonekcija);
                Console.WriteLine($"Filijala je pokrenuta na portu {_filijalaPort}, prihvatajuci do {_maxKonekcija} klijenata");

                _radi = true;

                while (_radi)
                {
                    var soketiSaPorukama = new List<Socket> { _klijentListener };
                    soketiSaPorukama.AddRange(_klijentSockets);

                    Socket.Select(soketiSaPorukama, null, null, 1000000); // 1 second timeout

                    foreach (var socket in soketiSaPorukama)
                    {
                        if (socket == _klijentListener)
                        {
                            PrihvatiKlijenta();
                        }
                        else
                        {
                            ObradiKlijenta(socket);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
            finally
            {
                PocistiKonekcije();
            }
        }

        private bool InicijalizacijaSaServerom()
        {
            try
            {
                // Posalji zahtev za inicijalizaciju
                var initRequest = Tuple.Create("INIT", (object)null);
                byte[] requestData = SerialazujObjekat(initRequest);
                _serverSocket.SendTo(requestData, _serverEndPoint);

                // Prihvati odgovor
                byte[] buffer = new byte[2048];
                EndPoint tempEP = new IPEndPoint(IPAddress.Any, 0);
                _serverSocket.ReceiveFrom(buffer, ref tempEP);

                var odgovor = (Dictionary<string, string>)DeserialazujObjekat(buffer);

                if (odgovor["success"] == "true")
                {
                    _maxBudzet = decimal.Parse(odgovor["maxBudget"]);
                    _maxKonekcija = int.Parse(odgovor["maxConnections"]);
                    Console.WriteLine($"Inicijalizovano sa maksimalnim budžetom: {_maxBudzet}, maksimalnim brojem konekcija: {_maxKonekcija}");
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return false;
            }
        }

        private void PrihvatiKlijenta()
        {
            if (_klijentSockets.Count >= _maxKonekcija)
            {
                Console.WriteLine("Dostignut je maksimalan broj konekcija, odbijanje novog klijenta");
                Socket tempSocket = _klijentListener.Accept();
                SendResponse(tempSocket, new Dictionary<string, string>
                {
                    ["success"] = "false",
                    ["message"] = "Filijala je dostigla maksimalni kapacitet"
                });
                tempSocket.Close();
                return;
            }

            Socket klijentSocket = _klijentListener.Accept();
            _klijentSockets.Add(klijentSocket);
            Console.WriteLine($"Klijent povezan: {klijentSocket.RemoteEndPoint}");

            SendResponse(klijentSocket, new Dictionary<string, string>
            {
                ["success"] = "true",
                ["message"] = "Povezivanje na filijalu je uspjesno"
            });
        }

        private void ObradiKlijenta(Socket klijentSocket)
        {
            try
            {
                byte[] buffer = new byte[2048];
                int primljeno = klijentSocket.Receive(buffer);

                if (primljeno == 0)
                {
                    ZatvoriKlijentskiSoket(klijentSocket);
                    return;
                }

                var zahtev = (Tuple<string, object>)DeserialazujObjekat(buffer);
                Dictionary<string, string> odgovor = ProcessClientRequest(zahtev.Item1, zahtev.Item2, klijentSocket);
                SendResponse(klijentSocket, odgovor);
            }
            catch (SocketException)
            {
                ZatvoriKlijentskiSoket(klijentSocket);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling client: {ex.Message}");
                SendResponse(klijentSocket, new Dictionary<string, string>
                {
                    ["success"] = "false",
                    ["message"] = "Internal error occurred"
                });
            }
        }

        private Dictionary<string, string> ProcessClientRequest(string tip, object podaci, Socket klijentSocket)
        {
            switch (tip)
            {
                case "REGISTER":
                    return ObradiRegistraciju((Korisnik)podaci);
                case "LOGIN":
                    return ObradiLogin((Korisnik)podaci, klijentSocket);
                case "BALANCE":
                    return ObradiProveruStanja((Korisnik)podaci);
                case "DEPOSIT":
                    return ObradiUplatu((Transakcija)podaci);
                case "WITHDRAW":
                    return ObradiIsplatu((Transakcija)podaci);
                case "TRANSFER":
                    return ObradiTransfer((TransferTransakcija)podaci);
                case "VALIDATE_TRANSFER":
                    return ObradiValidnostTransfera((TransferTransakcija)podaci);
                default:
                    return new Dictionary<string, string>
                    {
                        ["success"] = "false",
                        ["message"] = "Invalid request type"
                    };
            }
        }

        private Dictionary<string, string> ObradiRegistraciju(Korisnik korisnik)
        {
            return PosaljiZahtev("REGISTER", korisnik);
        }

        private Dictionary<string, string> ObradiLogin(Korisnik korisnik, Socket clientSocket)
        {
            var odgovor = PosaljiZahtev("LOGIN", korisnik);

            if (odgovor["success"] == "true")
            {
                _povezaniKorisnici[korisnik.KorisnickoIme] = korisnik;
            }

            return odgovor;
        }

        private Dictionary<string, string> ObradiProveruStanja(Korisnik korisnik)
        {
            if (!_povezaniKorisnici.ContainsKey(korisnik.KorisnickoIme))
            {
                return new Dictionary<string, string>
                {
                    ["success"] = "false",
                    ["message"] = "Korisnik nije ulogovan"
                };
            }

            return PosaljiZahtev("BALANCE", korisnik);
        }

        private Dictionary<string, string> ObradiUplatu(Transakcija transakcija)
        {
            if (!_povezaniKorisnici.ContainsKey(transakcija.KorisnickoIme))
            {
                return new Dictionary<string, string>
                {
                    ["success"] = "false",
                    ["message"] = "Korisnik nije ulogovan"
                };
            }

            var validnostOdgovor = ValidirajTransakciju(transakcija);

            if (validnostOdgovor["success"] != "true")
            {
                return validnostOdgovor;
            }

            var odgovor = PosaljiZahtev("DEPOSIT", transakcija);

            if (odgovor["success"] == "true")
            {
                _maxBudzet += transakcija.Kolicina;
                _transakcije.Add(transakcija);
            }

            return odgovor;
        }

        private Dictionary<string, string> ObradiIsplatu(Transakcija transakcija)
        {
            if (!_povezaniKorisnici.ContainsKey(transakcija.KorisnickoIme))
            {
                return new Dictionary<string, string>
                {
                    ["success"] = "false",
                    ["message"] = "Korisnik nije ulogovan"
                };
            }

            if (transakcija.Kolicina > _maxBudzet)
            {
                return new Dictionary<string, string>
                {
                    ["success"] = "false",
                    ["message"] = "Nedovoljan budzet filijale"
                };
            }

            var validnostOdgovor = ValidirajTransakciju(transakcija);

            if (validnostOdgovor["success"] != "true")
            {
                return validnostOdgovor;
            }

            var response = PosaljiZahtev("WITHDRAW", transakcija);

            if (response["success"] == "true")
            {
                _maxBudzet -= transakcija.Kolicina;
                _transakcije.Add(transakcija);
            }

            return response;
        }

        private Dictionary<string, string> ValidirajTransakciju(Transakcija transakcija)
        {
            return PosaljiZahtev("VALIDATE_TRANSACTION", transakcija);
        }

        private Dictionary<string, string> ObradiValidnostTransfera(TransferTransakcija transfer)
        {
            return PosaljiZahtev("VALIDATE_TRANSFER", transfer);
        }

        private Dictionary<string, string> ObradiTransfer(TransferTransakcija transfer)
        {
            if (!_povezaniKorisnici.ContainsKey(transfer.PosiljalacKorisnickoIme))
            {
                return new Dictionary<string, string>
                {
                    ["success"] = "false",
                    ["message"] = "Pošiljalac nije prijavljen"
                };
            }

            var validnostOdgovor = ObradiValidnostTransfera(transfer);

            if (validnostOdgovor["success"] != "true")
            {
                return validnostOdgovor;
            }

            var odgovor = PosaljiZahtev("TRANSFER", transfer);

            if (odgovor["success"] == "true")
            {
                _transferTransakcije.Add(transfer);
            }

            return odgovor;
        }

        private void SendResponse(Socket socket, Dictionary<string, string> odgovor)
        {
            byte[] podaciOdgovora = SerialazujObjekat(odgovor);
            socket.Send(podaciOdgovora);
        }

        private Dictionary<string, string> PosaljiZahtev(string tip, object podaci)
        {
            try
            {
                var zahtev = Tuple.Create(tip, podaci);
                byte[] podaciZahteva = SerialazujObjekat(zahtev);
                _serverSocket.SendTo(podaciZahteva, _serverEndPoint);

                byte[] buffer = new byte[2048];
                EndPoint tempEP = new IPEndPoint(IPAddress.Any, 0);
                _serverSocket.ReceiveFrom(buffer, ref tempEP);

                var odgovor = (Dictionary<string, string>)DeserialazujObjekat(buffer);
                Console.WriteLine(odgovor["message"]);
                return odgovor;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error pri slanju odgovora: {ex.Message}");
                return new Dictionary<string, string>
                {
                    ["success"] = "false",
                    ["message"] = ex.Message
                };
            }
        }

        private void ZatvoriKlijentskiSoket(Socket socket)
        {
            socket.Close();
            _klijentSockets.Remove(socket);
 
        }

        private void PocistiKonekcije()
        {
            foreach (var socket in _klijentSockets)
            {
                try
                {
                    socket.Close();
                }
                catch
                {
                }
            }
            _klijentSockets.Clear();
            _povezaniKorisnici.Clear();

            try
            {
                _serverSocket.Close();
                _klijentListener.Close();
            }
            catch
            {
            }
        }

        private static byte[] SerialazujObjekat(object obj)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                BinaryFormatter bf = new BinaryFormatter();
                bf.Serialize(ms, obj);
                return ms.ToArray();
            }
        }

        private static object DeserialazujObjekat(byte[] data)
        {
            using (MemoryStream ms = new MemoryStream(data))
            {
                BinaryFormatter bf = new BinaryFormatter();
                return bf.Deserialize(ms);
            }
        }
    }

}

