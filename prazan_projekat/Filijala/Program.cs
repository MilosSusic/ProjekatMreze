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
        private const string ServerIp = "192.168.56.1";
        private const int ServerPort = 5000;
        private const int FilijalaPort = 5001;

        private readonly Socket _socketServera;
        private readonly Socket _slušalacKlijenata;
        private readonly List<Socket> _soketiKlijenata;
        private decimal _maksimalniBudzet;
        private int _maksimalanBrojKonekcija;
        private bool _radi;
        private readonly Dictionary<string, Korisnik> _povezaniKorisnici;
        private readonly List<Transakcija> _transakcije;
        private EndPoint _krajnaAdresaServera;

        public Program()
        {
            _socketServera = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _slušalacKlijenata = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _soketiKlijenata = new List<Socket>();
            _povezaniKorisnici = new Dictionary<string, Korisnik>();
            _transakcije = new List<Transakcija>();
            _krajnaAdresaServera = new IPEndPoint(IPAddress.Parse(ServerIp), ServerPort);
        }

        static void Main(string[] args)
        {
            Console.WriteLine("Pokretanje filijale...");

            Program filijala = new Program();
            filijala.Pokreni();
        }

        public void Pokreni()
        {
            try
            {
                // Inicijalizacija i preuzimanje parametara od servera
                if (!InicijalizujSaServerom())
                {
                    Console.WriteLine("Inicijalizacija sa serverom nije uspela.");
                    return;
                }

                // Pokreni slušanje dolaznih konekcija klijenata
                _slušalacKlijenata.Bind(new IPEndPoint(IPAddress.Any, FilijalaPort));
                _slušalacKlijenata.Listen(_maksimalanBrojKonekcija);
                Console.WriteLine($"Filijala pokrenuta na portu {FilijalaPort}, prihvata do {_maksimalanBrojKonekcija} klijenata");

                _radi = true;

                while (_radi)
                {
                    var soketiZaCitanje = new List<Socket> { _slušalacKlijenata };
                    soketiZaCitanje.AddRange(_soketiKlijenata);

                    Socket.Select(soketiZaCitanje, null, null, 1000000); // timeout 1 sekunda

                    foreach (var socket in soketiZaCitanje)
                    {
                        if (socket == _slušalacKlijenata)
                        {
                            PrihvatiNovogKlijenta();
                        }
                        else
                        {
                            ObradiKlijentskuPoruku(socket);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Greška: {ex.Message}");
            }
            finally
            {
                OčistiKonekcije();
            }
        }

        private bool InicijalizujSaServerom()
        {
            try
            {
                // Pošalji zahtev za inicijalizaciju
                var zahtevInit = Tuple.Create("INIT", (object)null);
                byte[] podaciZahteva = SerializujObjekat(zahtevInit);
                _socketServera.SendTo(podaciZahteva, _krajnaAdresaServera);

                // Prihvati odgovor servera
                byte[] buffer = new byte[2048];
                EndPoint privremeniEP = new IPEndPoint(IPAddress.Any, 0);
                _socketServera.ReceiveFrom(buffer, ref privremeniEP);

                var odgovor = (Dictionary<string, string>)DeserializujObjekat(buffer);

                if (odgovor["success"] == "true")
                {
                    _maksimalniBudzet = decimal.Parse(odgovor["maxBudget"]);
                    _maksimalanBrojKonekcija = int.Parse(odgovor["maxConnections"]);
                    Console.WriteLine($"Inicijalizovano: maksimalni budžet = {_maksimalniBudzet}, maksimalni broj konekcija = {_maksimalanBrojKonekcija}");
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Greška: {ex.Message}");
                return false;
            }
        }

        private void PrihvatiNovogKlijenta()
        {
            if (_soketiKlijenata.Count >= _maksimalanBrojKonekcija)
            {
                Console.WriteLine("Dostignut maksimalan broj konekcija, odbijam novog klijenta");
                Socket privremeniSocket = _slušalacKlijenata.Accept();
                PosaljiOdgovor(privremeniSocket, new Dictionary<string, string>
                {
                    ["success"] = "false",
                    ["message"] = "Filijala je dostigla maksimalni kapacitet"
                });
                privremeniSocket.Close();
                return;
            }

            Socket noviKlijent = _slušalacKlijenata.Accept();
            _soketiKlijenata.Add(noviKlijent);
            Console.WriteLine($"Klijent povezan: {noviKlijent.RemoteEndPoint}");

            PosaljiOdgovor(noviKlijent, new Dictionary<string, string>
            {
                ["success"] = "true",
                ["message"] = "Uspešno povezan na filijalu"
            });
        }

        private void ObradiKlijentskuPoruku(Socket klijentSocket)
        {
            try
            {
                byte[] buffer = new byte[2048];
                int procitano = klijentSocket.Receive(buffer);

                if (procitano == 0)
                {
                    ZatvoriKlijentskiSocket(klijentSocket);
                    return;
                }

                var zahtev = (Tuple<string, object>)DeserializujObjekat(buffer);
                Dictionary<string, string> odgovor = ObradiZahtevKlijenta(zahtev.Item1, zahtev.Item2, klijentSocket);
                PosaljiOdgovor(klijentSocket, odgovor);
            }
            catch (SocketException)
            {
                ZatvoriKlijentskiSocket(klijentSocket);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Greška pri obradi klijenta: {ex.Message}");
                PosaljiOdgovor(klijentSocket, new Dictionary<string, string>
                {
                    ["success"] = "false",
                    ["message"] = "Došlo je do interne greške"
                });
            }
        }

        private Dictionary<string, string> ObradiZahtevKlijenta(string tipZahteva, object podaci, Socket klijentSocket)
        {
            switch (tipZahteva)
            {
                case "REGISTER":
                    return ObradiRegistraciju((Korisnik)podaci);
                case "LOGIN":
                    return ObradiPrijavu((Korisnik)podaci, klijentSocket);
                case "BALANCE":
                    return ObradiProveruStanja((Korisnik)podaci);
                case "DEPOSIT":
                    return ObradiUplatu((Transakcija)podaci);
                case "WITHDRAW":
                    return ObradiIsplatu((Transakcija)podaci);
                default:
                    return new Dictionary<string, string>
                    {
                        ["success"] = "false",
                        ["message"] = "Nevažeći tip zahteva"
                    };
            }
        }

        private Dictionary<string, string> ObradiRegistraciju(Korisnik korisnik)
        {
            return PosaljiZahtev("REGISTER", korisnik);
        }

        private Dictionary<string, string> ObradiPrijavu(Korisnik korisnik, Socket klijentSocket)
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
                    ["message"] = "Korisnik nije prijavljen"
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
                    ["message"] = "Korisnik nije prijavljen"
                };
            }

            var validacija = ValidirajTransakciju(transakcija);

            if (validacija["success"] != "true")
            {
                return validacija;
            }

            var odgovor = PosaljiZahtev("DEPOSIT", transakcija);

            if (odgovor["success"] == "true")
            {
                _maksimalniBudzet += transakcija.Kolicina;
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
                    ["message"] = "Korisnik nije prijavljen"
                };
            }

            if (transakcija.Kolicina > _maksimalniBudzet)
            {
                return new Dictionary<string, string>
                {
                    ["success"] = "false",
                    ["message"] = "Nema dovoljno sredstava u filijali"
                };
            }

            var validacija = ValidirajTransakciju(transakcija);

            if (validacija["success"] != "true")
            {
                return validacija;
            }

            var odgovor = PosaljiZahtev("WITHDRAW", transakcija);

            if (odgovor["success"] == "true")
            {
                _maksimalniBudzet -= transakcija.Kolicina;
                _transakcije.Add(transakcija);
            }

            return odgovor;
        }

        private Dictionary<string, string> ValidirajTransakciju(Transakcija transakcija)
        {
            return PosaljiZahtev("VALIDATE_TRANSACTION", transakcija);
        }

        private void PosaljiOdgovor(Socket socket, Dictionary<string, string> odgovor)
        {
            byte[] podaciOdgovora = SerializujObjekat(odgovor);
            socket.Send(podaciOdgovora);
        }

        private Dictionary<string, string> PosaljiZahtev(string tip, object podaci)
        {
            try
            {
                var zahtev = Tuple.Create(tip, podaci);
                byte[] podaciZahteva = SerializujObjekat(zahtev);
                _socketServera.SendTo(podaciZahteva, _krajnaAdresaServera);

                byte[] buffer = new byte[2048];
                EndPoint privremeniEP = new IPEndPoint(IPAddress.Any, 0);
                _socketServera.ReceiveFrom(buffer, ref privremeniEP);

                var odgovor = (Dictionary<string, string>)DeserializujObjekat(buffer);
                Console.WriteLine(odgovor["message"]);
                return odgovor;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Greška pri slanju zahteva: {ex.Message}");
                return new Dictionary<string, string>
                {
                    ["success"] = "false",
                    ["message"] = ex.Message
                };
            }
        }

        private void ZatvoriKlijentskiSocket(Socket socket)
        {
            socket.Close();
            _soketiKlijenata.Remove(socket);

        }

        private void OčistiKonekcije()
        {
            foreach (var socket in _soketiKlijenata)
            {
                try
                {
                    socket.Close();
                }
                catch
                {
                }
            }
            _soketiKlijenata.Clear();
            _povezaniKorisnici.Clear();

            try
            {
                _socketServera.Close();
                _slušalacKlijenata.Close();
            }
            catch
            {
            }
        }

        private static byte[] SerializujObjekat(object obj)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                BinaryFormatter bf = new BinaryFormatter();
                bf.Serialize(ms, obj);
                return ms.ToArray();
            }
        }

        private static object DeserializujObjekat(byte[] podaci)
        {
            using (MemoryStream ms = new MemoryStream(podaci))
            {
                BinaryFormatter bf = new BinaryFormatter();
                return bf.Deserialize(ms);
            }
        }
    }

}

