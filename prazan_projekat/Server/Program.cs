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
        private readonly List<Socket> _soketiFilijala;
        private readonly Dictionary<string, Korisnik> _korisnici;
        private readonly List<Transakcija> _transakcije;
        private bool _radi;

        public Program()
        {
            _serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _soketiFilijala = new List<Socket>();
            _korisnici = new Dictionary<string, Korisnik>();
            _transakcije = new List<Transakcija>();
        }

        static void Main(string[] args)
        {
            Console.WriteLine("Pokretanje serverskog dela bankarskog sistema...");

            Program server = new Program();
            server.Pokreni();
        }

        public void Pokreni()
        {
            try
            {
                _serverSocket.Bind(new IPEndPoint(IPAddress.Any, Port));
                _serverSocket.Listen(10);
                Console.WriteLine($"Server pokrenut na portu {Port}");

                _radi = true;

                while (_radi)
                {
                    List<Socket> zaCitanje = new List<Socket> { _serverSocket };
                    zaCitanje.AddRange(_soketiFilijala);

                    Socket.Select(zaCitanje, null, null, 1000000); // timeout 1s

                    foreach (Socket soket in zaCitanje)
                    {
                        if (soket == _serverSocket)
                        {
                            PrihvatiFilijalu();
                        }
                        else
                        {
                            ObradiZahtevFilijale(soket);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Greška servera: {ex.Message}");
            }
            finally
            {
                foreach (var soket in _soketiFilijala)
                {
                    if (soket.Connected)
                        soket.Close();
                }
                _serverSocket.Close();
            }
        }

        private void PrihvatiFilijalu()
        {
            Socket soketFilijale = _serverSocket.Accept();
            _soketiFilijala.Add(soketFilijale);
            Console.WriteLine($"Filijala povezana: {soketFilijale.RemoteEndPoint}");

            var odgovor = new Dictionary<string, string>
            {
                ["success"] = "true",
                ["maxBudget"] = "1000000",
                ["maxConnections"] = "5"
            };

            PosaljiOdgovor(soketFilijale, odgovor);
        }

        private void ObradiZahtevFilijale(Socket soketFilijale)
        {
            try
            {
                byte[] bafer = new byte[2048];
                int primljeno = soketFilijale.Receive(bafer);

                if (primljeno == 0)
                {
                    ZatvoriSoketFilijale(soketFilijale);
                    return;
                }

                var zahtev = (Tuple<string, object>)Deserijalizuj(bafer);
                var odgovor = ObradiZahtev(zahtev.Item1, zahtev.Item2);
                PosaljiOdgovor(soketFilijale, odgovor);
            }
            catch (SocketException)
            {
                ZatvoriSoketFilijale(soketFilijale);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Greška u obradi zahteva filijale: {ex.Message}");
                PosaljiOdgovor(soketFilijale, new Dictionary<string, string>
                {
                    ["success"] = "false",
                    ["message"] = "Interna greška servera"
                });
            }
        }

        private Dictionary<string, string> ObradiZahtev(string tip, object podaci)
        {
            try
            {
                switch (tip)
                {
                    case "REGISTER":
                        return Registruj((Korisnik)podaci);
                    case "LOGIN":
                        return Prijavi((Korisnik)podaci);
                    case "BALANCE":
                        return ProveriStanje((Korisnik)podaci);
                    case "VALIDATE_TRANSACTION":
                        return ValidirajTransakciju((Transakcija)podaci);
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

        private Dictionary<string, string> Registruj(Korisnik korisnik)
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
            Console.WriteLine($"Registrovan korisnik: {korisnik.KorisnickoIme}");
            return new Dictionary<string, string>
            {
                ["success"] = "true",
                ["message"] = "Registracija uspešna",
                ["username"] = korisnik.KorisnickoIme,
                ["accountNumber"] = korisnik.BrojRacuna
            };
        }

        private Dictionary<string, string> Prijavi(Korisnik korisnik)
        {
            if (!_korisnici.TryGetValue(korisnik.KorisnickoIme, out var sacuvani))
            {
                return new Dictionary<string, string>
                {
                    ["success"] = "false",
                    ["message"] = "Korisnik nije pronađen"
                };
            }

            if (sacuvani.Lozinka != korisnik.Lozinka)
            {
                return new Dictionary<string, string>
                {
                    ["success"] = "false",
                    ["message"] = "Pogrešna lozinka"
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

        private Dictionary<string, string> ProveriStanje(Korisnik korisnik)
        {
            if (!_korisnici.TryGetValue(korisnik.KorisnickoIme, out var sacuvani))
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
                ["message"] = $"Trenutno stanje: {sacuvani.Stanje:C}",
                ["balance"] = sacuvani.Stanje.ToString()
            };
        }

        private Dictionary<string, string> ValidirajTransakciju(Transakcija transakcija)
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
                    if (transakcija.Iznos > korisnik.MaksimalniIznosZaPodizanje)
                        return new Dictionary<string, string>
                        {
                            ["success"] = "false",
                            ["message"] = "Iznos premašuje dozvoljeni maksimum za isplatu"
                        };
                    if (transakcija.Iznos > korisnik.Stanje)
                        return new Dictionary<string, string>
                        {
                            ["success"] = "false",
                            ["message"] = "Nedovoljno sredstava"
                        };
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

            korisnik.Stanje += transakcija.Iznos;
            _transakcije.Add(transakcija);

            Console.WriteLine($"Uplata obrađena: {transakcija.Iznos:C} za korisnika {transakcija.KorisnickoIme}");
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

            if (transakcija.Iznos > korisnik.MaksimalniIznosZaPodizanje)
            {
                return new Dictionary<string, string>
                {
                    ["success"] = "false",
                    ["message"] = "Iznos premašuje maksimalnu dozvoljenu isplatu"
                };
            }

            if (transakcija.Iznos > korisnik.Stanje)
            {
                return new Dictionary<string, string>
                {
                    ["success"] = "false",
                    ["message"] = "Nedovoljno sredstava"
                };
            }

            korisnik.Stanje -= transakcija.Iznos;
            _transakcije.Add(transakcija);

            Console.WriteLine($"Isplata obrađena: {transakcija.Iznos:C} za korisnika {transakcija.KorisnickoIme}");
            return new Dictionary<string, string>
            {
                ["success"] = "true",
                ["message"] = $"Isplata uspešna. Novo stanje: {korisnik.Stanje:C}",
                ["balance"] = korisnik.Stanje.ToString()
            };
        }

        private void PosaljiOdgovor(Socket soket, object odgovor)
        {
            byte[] podaci = Serijalizuj(odgovor);
            soket.Send(podaci);
        }

        private void ZatvoriSoketFilijale(Socket soket)
        {
            soket.Close();
            _soketiFilijala.Remove(soket);
            Console.WriteLine("Filijala prekinula vezu");
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


