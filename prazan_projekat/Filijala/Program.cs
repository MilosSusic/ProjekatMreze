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
        [Serializable]
        public class Filijala
        {
            private const int PortServera = 5000;
            private const int PortFilijale = 5001;
            private const string IpAdresaServera = "127.0.0.1";

            private readonly Socket _socketServera;
            private readonly Socket _osluskujuciSocket;
            private readonly List<Socket> _klijentskiSocketi;
            private decimal _maksimalniBudzet;
            private int _maksimalniBrojVeza;
            private bool _radi;
            private readonly Dictionary<string, Korisnik> _prijavljeniKorisnici;
            private readonly List<Transakcija> _transakcije;

            public Filijala()
            {
                _socketServera = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                _osluskujuciSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                _klijentskiSocketi = new List<Socket>();
                _prijavljeniKorisnici = new Dictionary<string, Korisnik>();
                _transakcije = new List<Transakcija>();
            }

            static void Main(string[] args)
            {
                Console.WriteLine("Pokretanje filijale bankarskog sistema...");

                Filijala filijala = new Filijala();
                filijala.Pokreni();
            }

            public void Pokreni()
            {
                try
                {
                    _socketServera.Connect(new IPEndPoint(IPAddress.Parse(IpAdresaServera), PortServera));
                    Console.WriteLine("Povezano sa serverom.");

                    if (!InicijalizujSaServerom())
                    {
                        Console.WriteLine("Inicijalizacija sa serverom nije uspela.");
                        return;
                    }

                    _osluskujuciSocket.Bind(new IPEndPoint(IPAddress.Any, PortFilijale));
                    _osluskujuciSocket.Listen(_maksimalniBrojVeza);
                    Console.WriteLine($"Filijala pokrenuta na portu {PortFilijale}, prihvata do {_maksimalniBrojVeza} klijenata");

                    _radi = true;

                    while (_radi)
                    {
                        List<Socket> zaCitanje = new List<Socket> { _osluskujuciSocket };
                        zaCitanje.AddRange(_klijentskiSocketi);

                        Socket.Select(zaCitanje, null, null, 1000000); // 1 sekunda

                        foreach (Socket socket in zaCitanje)
                        {
                            if (socket == _osluskujuciSocket)
                                PrihvatiKlijenta();
                            else
                                ObradiKlijenta(socket);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Greška u filijali: {ex.Message}");
                }
                finally
                {
                    OslobodiVeze();
                }
            }

            private bool InicijalizujSaServerom()
            {
                try
                {
                    byte[] bafer = new byte[2048];
                    _socketServera.Receive(bafer);

                    var odgovor = (Dictionary<string, string>)DeserijalizujObjekat(bafer);

                    if (odgovor["success"] == "true")
                    {
                        _maksimalniBudzet = decimal.Parse(odgovor["maxBudget"]);
                        _maksimalniBrojVeza = int.Parse(odgovor["maxConnections"]);
                        Console.WriteLine($"Inicijalizovano: maksimalni budžet: {_maksimalniBudzet}, maksimalne veze: {_maksimalniBrojVeza}");
                        return true;
                    }
                    return false;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Greška pri inicijalizaciji: {ex.Message}");
                    return false;
                }
            }

            private void PrihvatiKlijenta()
            {
                if (_klijentskiSocketi.Count >= _maksimalniBrojVeza)
                {
                    Console.WriteLine("Maksimalan broj veza dostignut, novi klijent odbijen.");
                    Socket temp = _osluskujuciSocket.Accept();
                    PosaljiOdgovor(temp, new Dictionary<string, string>
                    {
                        ["success"] = "false",
                        ["message"] = "Filijala je dostigla maksimalni kapacitet"
                    });
                    temp.Close();
                    return;
                }

                Socket klijent = _osluskujuciSocket.Accept();
                _klijentskiSocketi.Add(klijent);
                Console.WriteLine($"Klijent povezan: {klijent.RemoteEndPoint}");

                PosaljiOdgovor(klijent, new Dictionary<string, string>
                {
                    ["success"] = "true",
                    ["message"] = "Uspešno povezan sa filijalom"
                });
            }

            private void ObradiKlijenta(Socket klijent)
            {
                try
                {
                    byte[] bafer = new byte[2048];
                    int primljeno = klijent.Receive(bafer);

                    if (primljeno == 0)
                    {
                        ZatvoriKlijenta(klijent);
                        return;
                    }

                    var zahtev = (Tuple<string, object>)DeserijalizujObjekat(bafer);
                    var odgovor = ObradiZahtevKlijenta(zahtev.Item1, zahtev.Item2, klijent);
                    PosaljiOdgovor(klijent, odgovor);
                }
                catch (SocketException)
                {
                    ZatvoriKlijenta(klijent);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Greška u obradi klijenta: {ex.Message}");
                    PosaljiOdgovor(klijent, new Dictionary<string, string>
                    {
                        ["success"] = "false",
                        ["message"] = "Došlo je do interne greške"
                    });
                }
            }

            private Dictionary<string, string> ObradiZahtevKlijenta(string tip, object podaci, Socket klijent)
            {
                switch (tip)
                {
                    case "REGISTER":
                        return RegistrujKorisnika((Korisnik)podaci);
                    case "LOGIN":
                        return PrijaviKorisnika((Korisnik)podaci, klijent);
                    case "BALANCE":
                        return ProveriStanje((Korisnik)podaci);
                    case "DEPOSIT":
                        return Uplata((Transakcija)podaci);
                    case "WITHDRAW":
                        return Isplata((Transakcija)podaci);
                    default:
                        return new Dictionary<string, string>
                        {
                            ["success"] = "false",
                            ["message"] = "Nepoznat tip zahteva"
                        };
                }
            }

            private Dictionary<string, string> RegistrujKorisnika(Korisnik korisnik)
            {
                return PosaljiZahtev("REGISTER", korisnik);
            }

            private Dictionary<string, string> PrijaviKorisnika(Korisnik korisnik, Socket klijent)
            {
                var odgovor = PosaljiZahtev("LOGIN", korisnik);
                if (odgovor["success"] == "true")
                    _prijavljeniKorisnici[korisnik.KorisnickoIme] = korisnik;

                return odgovor;
            }

            private Dictionary<string, string> ProveriStanje(Korisnik korisnik)
            {
                if (!_prijavljeniKorisnici.ContainsKey(korisnik.KorisnickoIme))
                {
                    return new Dictionary<string, string>
                    {
                        ["success"] = "false",
                        ["message"] = "Korisnik nije prijavljen"
                    };
                }

                return PosaljiZahtev("BALANCE", korisnik);
            }

            private Dictionary<string, string> Uplata(Transakcija transakcija)
            {
                if (!_prijavljeniKorisnici.ContainsKey(transakcija.KorisnickoIme))
                {
                    return new Dictionary<string, string>
                    {
                        ["success"] = "false",
                        ["message"] = "Korisnik nije prijavljen"
                    };
                }

                var validacija = ValidirajTransakciju(transakcija);

                if (validacija["success"] != "true")
                    return validacija;

                var odgovor = PosaljiZahtev("DEPOSIT", transakcija);

                if (odgovor["success"] == "true")
                {
                    _maksimalniBudzet += transakcija.Iznos;
                    _transakcije.Add(transakcija);
                }

                return odgovor;
            }

            private Dictionary<string, string> Isplata(Transakcija transakcija)
            {
                if (!_prijavljeniKorisnici.ContainsKey(transakcija.KorisnickoIme))
                {
                    return new Dictionary<string, string>
                    {
                        ["success"] = "false",
                        ["message"] = "Korisnik nije prijavljen"
                    };
                }

                if (transakcija.Iznos > _maksimalniBudzet)
                {
                    return new Dictionary<string, string>
                    {
                        ["success"] = "false",
                        ["message"] = "Nema dovoljno sredstava u filijali"
                    };
                }

                var validacija = ValidirajTransakciju(transakcija);

                if (validacija["success"] != "true")
                    return validacija;

                var odgovor = PosaljiZahtev("WITHDRAW", transakcija);

                if (odgovor["success"] == "true")
                {
                    _maksimalniBudzet -= transakcija.Iznos;
                    _transakcije.Add(transakcija);
                }

                return odgovor;
            }

            private Dictionary<string, string> ValidirajTransakciju(Transakcija transakcija)
            {
                return PosaljiZahtev("VALIDATE_TRANSACTION", transakcija);
            }

            private void PosaljiOdgovor(Socket soket, Dictionary<string, string> odgovor)
            {
                byte[] podaci = SerijalizujObjekat(odgovor);
                soket.Send(podaci);
            }

            private Dictionary<string, string> PosaljiZahtev(string tip, object podaci)
            {
                try
                {
                    var zahtev = Tuple.Create(tip, podaci);
                    byte[] zahtevPodaci = SerijalizujObjekat(zahtev);
                    _socketServera.Send(zahtevPodaci);

                    byte[] bafer = new byte[2048];
                    int primljeno = _socketServera.Receive(bafer);
                    var odgovor = (Dictionary<string, string>)DeserijalizujObjekat(bafer);
                    Console.WriteLine(odgovor["message"]);
                    return odgovor;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Greška prilikom slanja zahteva: {ex.Message}");
                    return new Dictionary<string, string>
                    {
                        ["success"] = "false",
                        ["message"] = ex.Message
                    };
                }
            }

            private void ZatvoriKlijenta(Socket soket)
            {
                soket.Close();
                _klijentskiSocketi.Remove(soket);

                var korisnikZaUklanjanje = _prijavljeniKorisnici.FirstOrDefault(x =>
                    _klijentskiSocketi.All(s => s.RemoteEndPoint?.ToString() != soket.RemoteEndPoint?.ToString()));

                if (!string.IsNullOrEmpty(korisnikZaUklanjanje.Key))
                    _prijavljeniKorisnici.Remove(korisnikZaUklanjanje.Key);

                Console.WriteLine("Klijent je prekinuo vezu");
            }

            private void OslobodiVeze()
            {
                foreach (var soket in _klijentskiSocketi)
                {
                    try { soket.Close(); } catch { }
                }
                _klijentskiSocketi.Clear();
                _prijavljeniKorisnici.Clear();

                try
                {
                    _socketServera.Close();
                    _osluskujuciSocket.Close();
                }
                catch { }
            }

            private static byte[] SerijalizujObjekat(object obj)
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    BinaryFormatter bf = new BinaryFormatter();
                    bf.Serialize(ms, obj);
                    return ms.ToArray();
                }
            }

            private static object DeserijalizujObjekat(byte[] podaci)
            {
                using (MemoryStream ms = new MemoryStream(podaci))
                {
                    BinaryFormatter bf = new BinaryFormatter();
                    return bf.Deserialize(ms);
                }
            }
        }


    }
}

