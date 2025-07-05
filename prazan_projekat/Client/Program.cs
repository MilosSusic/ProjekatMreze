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
        public class Klijent
        {
            private const int PortFilijale = 5001;
            private const string IpFilijale = "127.0.0.1";
            private Socket _socketFilijale;
            private bool _povezan;
            private Korisnik _trenutniKorisnik;

            public Klijent()
            {
                _socketFilijale = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                _trenutniKorisnik = null;
            }

            static void Main(string[] args)
            {
                Console.WriteLine("Pokretanje klijentskog dela bankarskog sistema...");

                Klijent klijent = new Klijent();
                klijent.Pokreni();
            }

            public void Pokreni()
            {
                try
                {
                    PoveziSeNaFilijalu();

                    while (true)
                    {
                        if (!_povezan)
                        {
                            Console.WriteLine("Niste povezani na filijalu. Pritisnite Enter da pokušate ponovo ili unesite 'exit' za izlaz.");
                            if (Console.ReadLine()?.ToLower() == "exit")
                                break;
                            PoveziSeNaFilijalu();
                            continue;
                        }

                        PrikaziMeni();
                        string izbor = Console.ReadLine();

                        switch (izbor)
                        {
                            case "1":
                                RegistrujSe();
                                break;
                            case "2":
                                PrijaviSe();
                                break;
                            case "3":
                                if (_trenutniKorisnik != null)
                                    ProveriStanje();
                                else
                                    Console.WriteLine("Prvo se morate prijaviti.");
                                break;
                            case "4":
                                if (_trenutniKorisnik != null)
                                    Uplati();
                                else
                                    Console.WriteLine("Prvo se morate prijaviti.");
                                break;
                            case "5":
                                if (_trenutniKorisnik != null)
                                    Podigni();
                                else
                                    Console.WriteLine("Prvo se morate prijaviti.");
                                break;
                            case "6":
                                _povezan = false;
                                break;
                            default:
                                Console.WriteLine("Nepoznata opcija.");
                                break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Greška klijenta: {ex.Message}");
                }
                finally
                {
                    if (_socketFilijale.Connected)
                        _socketFilijale.Close();
                }
            }

            private void PrikaziMeni()
            {
                Console.WriteLine("\n=== Klijent bankarskog sistema ===");
                Console.WriteLine("1. Registracija");
                Console.WriteLine("2. Prijava");
                Console.WriteLine("3. Provera stanja");
                Console.WriteLine("4. Uplata");
                Console.WriteLine("5. Isplata");
                Console.WriteLine("6. Izlaz");
                Console.Write("Izaberite opciju: ");
            }

            private void PoveziSeNaFilijalu()
            {
                try
                {
                    if (_socketFilijale.Connected)
                        _socketFilijale.Close();

                    _socketFilijale = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    _socketFilijale.Connect(new IPEndPoint(IPAddress.Parse(IpFilijale), PortFilijale));

                    byte[] bafer = new byte[2048];
                    int primljeno = _socketFilijale.Receive(bafer);
                    var odgovor = (Dictionary<string, string>)Deserijalizuj(bafer);

                    if (odgovor["success"] != "true")
                    {
                        Console.WriteLine($"Filijala odbila konekciju: {odgovor["message"]}");
                        _povezan = false;
                        return;
                    }

                    _povezan = true;
                    Console.WriteLine("Uspešno povezano sa filijalom.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Greška pri povezivanju: {ex.Message}");
                    _povezan = false;
                }
            }

            private void RegistrujSe()
            {
                Console.Write("Unesite korisničko ime: ");
                string korisnickoIme = Console.ReadLine();
                Console.Write("Unesite lozinku: ");
                string lozinka = Console.ReadLine();
                Console.Write("Unesite ime: ");
                string ime = Console.ReadLine();
                Console.Write("Unesite prezime: ");
                string prezime = Console.ReadLine();
                Console.Write("Unesite maksimalni iznos za isplatu: ");

                if (!decimal.TryParse(Console.ReadLine(), out decimal maxIsplata))
                {
                    Console.WriteLine("Neispravan unos iznosa.");
                    return;
                }

                Korisnik korisnik = new Korisnik
                {
                    KorisnickoIme = korisnickoIme,
                    Lozinka = lozinka,
                    Ime = ime,
                    Prezime = prezime,
                    MaksimalniIznosZaPodizanje = maxIsplata
                };

                PosaljiZahtev("REGISTER", korisnik);
            }

            private void PrijaviSe()
            {
                Console.Write("Unesite korisničko ime: ");
                string korisnickoIme = Console.ReadLine();
                Console.Write("Unesite lozinku: ");
                string lozinka = Console.ReadLine();

                Korisnik korisnik = new Korisnik
                {
                    KorisnickoIme = korisnickoIme,
                    Lozinka = lozinka
                };

                var odgovor = PosaljiZahtev("LOGIN", korisnik);
                if (odgovor["success"] == "true")
                {
                    _trenutniKorisnik = korisnik;
                }
            }

            private void ProveriStanje()
            {
                PosaljiZahtev("BALANCE", _trenutniKorisnik);
            }

            private void Uplati()
            {
                Console.Write("Unesite iznos za uplatu: ");
                if (!decimal.TryParse(Console.ReadLine(), out decimal iznos))
                {
                    Console.WriteLine("Neispravan iznos.");
                    return;
                }

                Transakcija transakcija = new Transakcija(_trenutniKorisnik.KorisnickoIme, iznos, TipTransakcije.Uplata);
                PosaljiZahtev("DEPOSIT", transakcija);
            }

            private void Podigni()
            {
                Console.Write("Unesite iznos za isplatu: ");
                if (!decimal.TryParse(Console.ReadLine(), out decimal iznos))
                {
                    Console.WriteLine("Neispravan iznos.");
                    return;
                }

                Transakcija transakcija = new Transakcija(_trenutniKorisnik.KorisnickoIme, iznos, TipTransakcije.Isplata);
                PosaljiZahtev("WITHDRAW", transakcija);
            }

            private Dictionary<string, string> PosaljiZahtev(string tip, object podaci)
            {
                try
                {
                    var zahtev = Tuple.Create(tip, podaci);
                    byte[] podaciZaSlanje = Serijalizuj(zahtev);
                    _socketFilijale.Send(podaciZaSlanje);

                    byte[] bafer = new byte[2048];
                    int primljeno = _socketFilijale.Receive(bafer);
                    var odgovor = (Dictionary<string, string>)Deserijalizuj(bafer);
                    Console.WriteLine(odgovor["message"]);
                    return odgovor;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Greška pri slanju zahteva: {ex.Message}");
                    _povezan = false;
                    return new Dictionary<string, string>
                    {
                        ["success"] = "false",
                        ["message"] = ex.Message
                    };
                }
            }

            private static byte[] Serijalizuj(object obj)
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    BinaryFormatter bf = new BinaryFormatter();
                    bf.Serialize(ms, obj);
                    return ms.ToArray();
                }
            }

            private static object Deserijalizuj(byte[] podaci)
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


