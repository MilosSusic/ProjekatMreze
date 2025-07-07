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
        private readonly Socket _serverSoket;
        private readonly Dictionary<string, Korisnik> _korisnici;
        private readonly List<Transakcija> _transakcije;
        private bool _aplikacijaPokrenuta;

        public Program()
        {
            _serverSoket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _korisnici = new Dictionary<string, Korisnik>();
            _transakcije = new List<Transakcija>();
        }

        static void Main(string[] args)
        {
            Console.WriteLine("Pokretanje servera...");

            Program server = new Program();
            server.Pokreni();
        }

        public void Pokreni()
        {
            try
            {
                _serverSoket.Bind(new IPEndPoint(IPAddress.Any, Port));
                Console.WriteLine($"Server pokrenut na portu {Port}");

                _aplikacijaPokrenuta = true;

                while (_aplikacijaPokrenuta)
                {
                    try
                    {
                        byte[] bafer = new byte[2048];
                        EndPoint krajnjaAdresa = new IPEndPoint(IPAddress.Any, 0);

                        int brojBajtova = _serverSoket.ReceiveFrom(bafer, ref krajnjaAdresa);

                        if (brojBajtova > 0)
                        {
                            var zahtev = (Tuple<string, object>)DeserijalizujObjekat(bafer);
                            var odgovor = ObradiZahtevFilijale(zahtev.Item1, zahtev.Item2);
                            PosaljiOdgovor(_serverSoket, odgovor, krajnjaAdresa);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Greška: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Greška: {ex.Message}");
            }
            finally
            {
                _serverSoket?.Close();
            }
        }

        private Dictionary<string, string> ObradiZahtevFilijale(string tip, object podaci)
        {
            try
            {
                switch (tip)
                {
                    case "INIT":
                        return ObradiInicijalizaciju();
                    case "REGISTER":
                        return ObradiRegistraciju((Korisnik)podaci);
                    case "LOGIN":
                        return ObradiPrijavu((Korisnik)podaci);
                    case "BALANCE":
                        return ObradiProveruStanja((Korisnik)podaci);
                    case "VALIDATE_TRANSACTION":
                        return ObradiValidnostTransakcije((Transakcija)podaci);
                    case "DEPOSIT":
                        return ObradiUplatu((Transakcija)podaci);
                    case "WITHDRAW":
                        return ObradiIsplatu((Transakcija)podaci);
                    default:
                        return new Dictionary<string, string>
                        {
                            ["success"] = "false",
                            ["message"] = "Nepoznat tip zahteva"
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
                    ["message"] = "Korisničko ime već postoji"
                };
            }

            _korisnici.Add(korisnik.KorisnickoIme, korisnik);
            Console.WriteLine($"Korisnik registrovan: {korisnik.KorisnickoIme}");

            return new Dictionary<string, string>
            {
                ["success"] = "true",
                ["message"] = "Registracija uspešna",
                ["username"] = korisnik.KorisnickoIme,
                ["accountNumber"] = korisnik.BrojRacuna
            };
        }

        private Dictionary<string, string> ObradiPrijavu(Korisnik korisnik)
        {
            if (!_korisnici.TryGetValue(korisnik.KorisnickoIme, out var sacuvaniKorisnik))
            {
                return new Dictionary<string, string>
                {
                    ["success"] = "false",
                    ["message"] = "Korisnik nije pronađen"
                };
            }

            if (sacuvaniKorisnik.Sifra != korisnik.Sifra)
            {
                return new Dictionary<string, string>
                {
                    ["success"] = "false",
                    ["message"] = "Neispravna lozinka"
                };
            }

            Console.WriteLine($"Korisnik prijavljen: {korisnik.KorisnickoIme}");

            return new Dictionary<string, string>
            {
                ["success"] = "true",
                ["message"] = "Prijava uspešna",
                ["username"] = korisnik.KorisnickoIme,
                ["accountNumber"] = korisnik.BrojRacuna
            };
        }

        private Dictionary<string, string> ObradiProveruStanja(Korisnik korisnik)
        {
            if (!_korisnici.TryGetValue(korisnik.KorisnickoIme, out var korisnikSaStanja))
            {
                return new Dictionary<string, string>
                {
                    ["success"] = "false",
                    ["message"] = "Korisnik nije pronađen"
                };
            }

            return new Dictionary<string, string>
            {
                ["success"] = "true",
                ["message"] = $"Trenutno stanje: {korisnikSaStanja.Stanje:C}",
                ["balance"] = korisnikSaStanja.Stanje.ToString()
            };
        }

        private Dictionary<string, string> ObradiValidnostTransakcije(Transakcija transakcija)
        {
            if (!_korisnici.TryGetValue(transakcija.KorisnickoIme, out var korisnik))
            {
                return new Dictionary<string, string>
                {
                    ["success"] = "false",
                    ["message"] = "Korisnik nije pronađen"
                };
            }

            switch (transakcija.Tip)
            {
                case TipTransakcije.Uplata:
                    return new Dictionary<string, string>
                    {
                        ["success"] = "true",
                        ["message"] = "Uplata validna"
                    };
                case TipTransakcije.Isplata:
                    if (transakcija.Kolicina > korisnik.MaxSumaZaIsplatu)
                    {
                        return new Dictionary<string, string>
                        {
                            ["success"] = "false",
                            ["message"] = "Iznos prelazi maksimalnu dozvoljenu isplatu"
                        };
                    }

                    if (transakcija.Kolicina > korisnik.Stanje)
                    {
                        return new Dictionary<string, string>
                        {
                            ["success"] = "false",
                            ["message"] = "Nedovoljno sredstava"
                        };
                    }

                    return new Dictionary<string, string>
                    {
                        ["success"] = "true",
                        ["message"] = "Isplata validna"
                    };
                default:
                    return new Dictionary<string, string>
                    {
                        ["success"] = "false",
                        ["message"] = "Nepoznat tip transakcije"
                    };
            }
        }

        private Dictionary<string, string> ObradiUplatu(Transakcija transakcija)
        {
            if (!_korisnici.TryGetValue(transakcija.KorisnickoIme, out var korisnik))
            {
                return new Dictionary<string, string>
                {
                    ["success"] = "false",
                    ["message"] = "Korisnik nije pronađen"
                };
            }

            korisnik.Stanje += transakcija.Kolicina;
            _transakcije.Add(transakcija);

            Console.WriteLine($"Uplata: {transakcija.Kolicina:C} za korisnika {transakcija.KorisnickoIme}");

            return new Dictionary<string, string>
            {
                ["success"] = "true",
                ["message"] = $"Uplata uspešna. Novo stanje: {korisnik.Stanje:C}",
                ["balance"] = korisnik.Stanje.ToString()
            };
        }

        private Dictionary<string, string> ObradiIsplatu(Transakcija transakcija)
        {
            if (!_korisnici.TryGetValue(transakcija.KorisnickoIme, out var korisnik))
            {
                return new Dictionary<string, string>
                {
                    ["success"] = "false",
                    ["message"] = "Korisnik nije pronađen"
                };
            }

            if (transakcija.Kolicina > korisnik.MaxSumaZaIsplatu)
            {
                return new Dictionary<string, string>
                {
                    ["success"] = "false",
                    ["message"] = "Iznos prelazi maksimalnu dozvoljenu isplatu"
                };
            }

            if (transakcija.Kolicina > korisnik.Stanje)
            {
                return new Dictionary<string, string>
                {
                    ["success"] = "false",
                    ["message"] = "Nedovoljno sredstava"
                };
            }

            korisnik.Stanje -= transakcija.Kolicina;
            _transakcije.Add(transakcija);

            Console.WriteLine($"Isplata: {transakcija.Kolicina:C} za korisnika {transakcija.KorisnickoIme}");

            return new Dictionary<string, string>
            {
                ["success"] = "true",
                ["message"] = $"Isplata uspešna. Novo stanje: {korisnik.Stanje:C}",
                ["balance"] = korisnik.Stanje.ToString()
            };
        }

        private void PosaljiOdgovor(Socket soket, object odgovor, EndPoint adresa)
        {
            byte[] podaci = SerijalizujObjekat(odgovor);
            soket.SendTo(podaci, adresa);
        }

        private static byte[] SerijalizujObjekat(object objekat)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                BinaryFormatter bf = new BinaryFormatter();
                bf.Serialize(ms, objekat);
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


