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
    public class Program
    {
        private const int PortFilijale = 5001;
        private const string IpFilijale = "127.0.0.1";

        private Socket _soketFilijale;
        private bool _jePovezan;
        private Korisnik _trenutniKorisnik;

        public Program()
        {
            _soketFilijale = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _trenutniKorisnik = null;
        }

        static void Main(string[] args)
        {
            Console.WriteLine("Pokretanje klijenta...");

            Program klijent = new Program();
            klijent.Pokreni();
        }

        public void Pokreni()
        {
            try
            {
                PoveziSeNaFilijalu();

                while (true)
                {
                    if (!_jePovezan)
                    {
                        Console.WriteLine("Niste povezani sa filijalom. Pritisnite Enter za ponovno povezivanje ili 'exit' za izlaz.");
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
                                IzvrsiUplatu();
                            else
                                Console.WriteLine("Prvo se morate prijaviti.");
                            break;
                        case "5":
                            if (_trenutniKorisnik != null)
                                IzvrsiIsplatu();
                            else
                                Console.WriteLine("Prvo se morate prijaviti.");
                            break;
                        case "6":
                            _jePovezan = false;
                            break;
                        default:
                            Console.WriteLine("Nepoznata opcija.");
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Greška: {ex.Message}");
            }
            finally
            {
                if (_soketFilijale.Connected)
                    _soketFilijale.Close();
            }
        }

        private void PrikaziMeni()
        {
            Console.WriteLine("\n=== Glavni meni ===");
            Console.WriteLine("1. Registracija");
            Console.WriteLine("2. Prijava");
            Console.WriteLine("3. Provera stanja");
            Console.WriteLine("4. Uplata");
            Console.WriteLine("5. Isplata");
            Console.WriteLine("6. Prekini vezu");
            Console.Write("Izbor: ");
        }

        private void PoveziSeNaFilijalu()
        {
            try
            {
                if (_soketFilijale.Connected)
                    _soketFilijale.Close();

                _soketFilijale = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                _soketFilijale.Connect(new IPEndPoint(IPAddress.Parse(IpFilijale), PortFilijale));

                byte[] bafer = new byte[2048];
                _soketFilijale.Receive(bafer);

                var odgovor = (Dictionary<string, string>)DeserijalizujObjekat(bafer);

                if (odgovor["success"] != "true")
                {
                    Console.WriteLine($"Povezivanje odbijeno: {odgovor["message"]}");
                    _jePovezan = false;
                    return;
                }

                _jePovezan = true;
                Console.WriteLine("Uspešno povezano na filijalu.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Greška pri povezivanju: {ex.Message}");
                _jePovezan = false;
            }
        }

        private void RegistrujSe()
        {
            Console.Write("Korisničko ime: ");
            var korisnickoIme = Console.ReadLine();
            Console.Write("Šifra: ");
            var sifra = Console.ReadLine();
            Console.Write("Ime: ");
            var ime = Console.ReadLine();
            Console.Write("Prezime: ");
            var prezime = Console.ReadLine();
            Console.Write("Maksimalna suma za isplatu: ");

            if (!decimal.TryParse(Console.ReadLine(), out decimal maxIsplata))
            {
                Console.WriteLine("Neispravan unos sume.");
                return;
            }

            var korisnik = new Korisnik
            {
                KorisnickoIme = korisnickoIme,
                Sifra = sifra,
                Ime = ime,
                Prezime = prezime,
                MaxSumaZaIsplatu = maxIsplata
            };

            PosaljiZahtev("REGISTER", korisnik);
        }

        private void PrijaviSe()
        {
            Console.Write("Korisničko ime: ");
            var korisnickoIme = Console.ReadLine();
            Console.Write("Šifra: ");
            var sifra = Console.ReadLine();

            var korisnik = new Korisnik
            {
                KorisnickoIme = korisnickoIme,
                Sifra = sifra
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

        private void IzvrsiUplatu()
        {
            Console.Write("Unesite iznos za uplatu: ");
            if (!decimal.TryParse(Console.ReadLine(), out decimal kolicina))
            {
                Console.WriteLine("Neispravan iznos.");
                return;
            }

            var transakcija = new Transakcija(_trenutniKorisnik.KorisnickoIme, kolicina, TipTransakcije.Uplata);
            PosaljiZahtev("DEPOSIT", transakcija);
        }

        private void IzvrsiIsplatu()
        {
            Console.Write("Unesite iznos za isplatu: ");
            if (!decimal.TryParse(Console.ReadLine(), out decimal kolicina))
            {
                Console.WriteLine("Neispravan iznos.");
                return;
            }

            var transakcija = new Transakcija(_trenutniKorisnik.KorisnickoIme, kolicina, TipTransakcije.Isplata);
            PosaljiZahtev("WITHDRAW", transakcija);
        }

        private Dictionary<string, string> PosaljiZahtev(string tip, object podaci)
        {
            try
            {
                var zahtev = Tuple.Create(tip, podaci);
                byte[] podaciZahteva = SerijalizujObjekat(zahtev);
                _soketFilijale.Send(podaciZahteva);

                byte[] bafer = new byte[2048];
                _soketFilijale.Receive(bafer);
                var odgovor = (Dictionary<string, string>)DeserijalizujObjekat(bafer);

                Console.WriteLine(odgovor["message"]);
                return odgovor;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Greška prilikom slanja zahteva: {ex.Message}");
                _jePovezan = false;
                return new Dictionary<string, string>
                {
                    ["success"] = "false",
                    ["message"] = ex.Message
                };
            }
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


