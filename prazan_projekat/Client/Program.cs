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
        private const string IpFilijale = "127.0.0.1";
        private Socket _filijalaSocket;
        private bool _jePovezan;
        private Korisnik _trenutniKorisnik;
        private int _portFilijale;

        public Program()
        {
            _filijalaSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _trenutniKorisnik = null;
        }

        private void UnesiPortFilijale()
        {
            while (true)
            {
                Console.Write("Unesite port filijale (podrazumevano 5001): ");
                var unos = Console.ReadLine();

                if (string.IsNullOrEmpty(unos))
                {
                    _portFilijale = 5001;
                    Console.WriteLine($"Koristi se podrazumevani port: {_portFilijale}");
                    break;
                }

                if (int.TryParse(unos, out int port) && port > 0 && port <= 65535)
                {
                    _portFilijale = port;
                    Console.WriteLine($"Povezivanje na port: {_portFilijale}");
                    break;
                }

                Console.WriteLine("Pogrešan unos. Port mora biti broj između 1 i 65535.");
            }
        }

        static void Main(string[] args)
        {
            Console.WriteLine("Pokretanje klijenta...");

            Program klijent = new Program();
            klijent.Start();
        }

        public void Start()
        {
            try
            {
                UnesiPortFilijale();
                PoveziSeNaFilijalu();

                while (true)
                {
                    if (!_jePovezan)
                    {
                        Console.WriteLine("Niste povezani sa granom. Pritisnite Enter da biste pokušali ponovo da se povežete ili „exit“ da biste izašli.");
                        if (Console.ReadLine()?.ToLower() == "exit")
                            break;
                        PoveziSeNaFilijalu();
                        continue;
                    }

                    ShowMenu();
                    string izbor = Console.ReadLine();

                    switch (izbor)
                    {
                        case "1":
                            Registracija();
                            break;
                        case "2":
                            Login();
                            break;
                        case "3":
                            if (_trenutniKorisnik != null)
                                ProveraStanja();
                            else
                                Console.WriteLine("Molimo vas da se prvo prijavite.");
                            break;
                        case "4":
                            if (_trenutniKorisnik != null)
                                Uplata();
                            else
                                Console.WriteLine("Molimo vas da se prvo prijavite.");
                            break;
                        case "5":
                            if (_trenutniKorisnik != null)
                                Isplata();
                            else
                                Console.WriteLine("Molimo vas da se prvo prijavite.");
                            break;
                        case "6":
                            if (_trenutniKorisnik != null)
                                Transfer();
                            else
                                Console.WriteLine("Molimo vas da se prvo prijavite.");
                            break;
                        case "7":
                            _jePovezan = false;
                            break;
                        default:
                            Console.WriteLine("Invalid choice.");
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
            finally
            {
                if (_filijalaSocket.Connected)
                    _filijalaSocket.Close();
            }
        }

        private void ShowMenu()
        {
            Console.WriteLine("\n=== Klient ===");
            Console.WriteLine("1. Registracija");
            Console.WriteLine("2. Login");
            Console.WriteLine("3. Provera Stanja");
            Console.WriteLine("4. Uplata");
            Console.WriteLine("5. Isplata");
            Console.WriteLine("6. Transfer");
            Console.WriteLine("7. Izlaz");
            Console.Write("Izaberi: ");
        }

        private void PoveziSeNaFilijalu()
        {
            try
            {
                if (_filijalaSocket.Connected)
                    _filijalaSocket.Close();

                _filijalaSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                _filijalaSocket.Connect(new IPEndPoint(IPAddress.Parse(IpFilijale), _portFilijale));

                byte[] buffer = new byte[2048];
                _filijalaSocket.Receive(buffer);
                var odgovor = (Dictionary<string, string>)DeserialazujObjekat(buffer);

                if (odgovor["success"] != "true")
                {
                    Console.WriteLine($"Filijala odbila: {odgovor["message"]}");
                    _jePovezan = false;
                    return;
                }

                _jePovezan = true;
                Console.WriteLine("Povezan na filijalu uspesno.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                _jePovezan = false;
            }
        }

        private void Registracija()
        {
            Console.Write("Korisnicko ime: ");
            var korisnickoIme = Console.ReadLine();
            Console.Write("Sifra: ");
            var sifra = Console.ReadLine();
            Console.Write("Ime: ");
            var ime = Console.ReadLine();
            Console.Write("Prezime: ");
            var prezime = Console.ReadLine();
            Console.Write("Maksimalna suma za isplatu: ");
            if (!decimal.TryParse(Console.ReadLine(), out decimal maxSumaZaIsplatu))
            {
                Console.WriteLine("Pogresna suma.");
                return;
            }

            var korisnik = new Korisnik
            {
                KorisnickoIme = korisnickoIme,
                Sifra = sifra,
                Ime = ime,
                Prezime = prezime,
                MaxSumaZaIsplatu = maxSumaZaIsplatu
            };

            PosaljiZahtev("REGISTER", korisnik);
        }

        private void Login()
        {
            Console.Write("Korisnicko ime: ");
            var username = Console.ReadLine();
            Console.Write("Sifra: ");
            var password = Console.ReadLine();

            var korisnik = new Korisnik
            {
                KorisnickoIme = username,
                Sifra = password
            };

            var odgovor = PosaljiZahtev("LOGIN", korisnik);
            if (odgovor["success"] == "true")
            {
                _trenutniKorisnik = korisnik;
            }
        }

        private void ProveraStanja()
        {
            PosaljiZahtev("BALANCE", _trenutniKorisnik);
        }

        private void Uplata()
        {
            Console.Write("Suma za uplatu: ");
            if (!decimal.TryParse(Console.ReadLine(), out decimal kolicina))
            {
                Console.WriteLine("Pogresna suma.");
                return;
            }

            var transakcija = new Transakcija(_trenutniKorisnik.KorisnickoIme, kolicina, TipTransakcije.Uplata);

            PosaljiZahtev("DEPOSIT", transakcija);
        }

        private void Isplata()
        {
            Console.Write("Suma za isplatu: ");
            if (!decimal.TryParse(Console.ReadLine(), out decimal kolicina))
            {
                Console.WriteLine("Pogresna suma.");
                return;
            }

            var transakcija = new Transakcija(_trenutniKorisnik.KorisnickoIme, kolicina, TipTransakcije.Isplata);

            PosaljiZahtev("WITHDRAW", transakcija);
        }

        private void Transfer()
        {
            Console.Write("Korisničko ime primaoca: ");
            var primalacKorisnickoIme = Console.ReadLine();

            if (string.IsNullOrEmpty(primalacKorisnickoIme))
            {
                Console.WriteLine("Korisničko ime primaoca ne može biti prazno.");
                return;
            }

            Console.Write("Suma za transfer: ");
            if (!decimal.TryParse(Console.ReadLine(), out decimal kolicina))
            {
                Console.WriteLine("Pogrešna suma.");
                return;
            }

            var transfer = new TransferTransakcija(_trenutniKorisnik.KorisnickoIme, primalacKorisnickoIme, kolicina);

            PosaljiZahtev("TRANSFER", transfer);
        }

        private Dictionary<string, string> PosaljiZahtev(string type, object data)
        {
            try
            {
                var zahtev = Tuple.Create(type, data);
                byte[] requestData = SerialazujObjekat(zahtev);
                _filijalaSocket.Send(requestData);

                byte[] buffer = new byte[2048];
                _filijalaSocket.Receive(buffer);
                var odgovor = (Dictionary<string, string>)DeserialazujObjekat(buffer);
                Console.WriteLine(odgovor["message"]);
                return odgovor;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error pri slanju zahteva: {ex.Message}");
                _jePovezan = false;
                return new Dictionary<string, string>
                {
                    ["success"] = "false",
                    ["message"] = ex.Message
                };
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


