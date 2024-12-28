using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Domain;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using System.Security.Cryptography.X509Certificates;

namespace Server
{     
    public class Server
    {
        
        static void Main(string[] args)
        {
            string info_prijava;
            #region Inicijalizacija i povezivanje

            Socket serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            IPEndPoint serverEP = new IPEndPoint(IPAddress.Any, 50001);

            serverSocket.Bind(serverEP);

            serverSocket.Listen(5);


            //Console.WriteLine($"Server je stavljen u stanje osluskivanja i ocekuje komunikaciju na {serverEP}");

            Socket acceptedSocket = serverSocket.Accept();

            IPEndPoint clientEP = acceptedSocket.RemoteEndPoint as IPEndPoint;
            Console.WriteLine($"Povezao se novi klijent! Njegova adresa je {clientEP}");

            #endregion

            byte[] buffer = new byte[4096];

            List<Korisnik> korisnici = new List<Korisnik>()
            {
                new Korisnik("123747","Milos","Susic",0),
                new Korisnik("214532","Bozana","Todorovic",0),
                new Korisnik("21342","Ime1","Prezime1",0),
                new Korisnik("21532","Ime2","Prezime2",0)
            };

            BinaryFormatter formatter = new BinaryFormatter();

            #region Prijem rezultata

            while (true)
            {
                try
                {
                    int brBajta = acceptedSocket.Receive(buffer);
                    if (brBajta == 0) break;

                    using (MemoryStream ms = new MemoryStream(buffer, 0, brBajta))
                    {
                        Korisnik korisnik = (Korisnik)formatter.Deserialize(ms);
                        korisnici.Add(korisnik);

                        Console.WriteLine("Primljen rezultat:");
                        Console.WriteLine($"Id: {korisnik.IdKorisnik}, Ime: {korisnik.Ime}, Prezime: {korisnik.Prezime}");

                        bool odgovor =korisnik.Uspjesnost(korisnici);

                        if (odgovor == true)
                        {
                            info_prijava = "USPJESAN";
                            acceptedSocket.Send(Encoding.UTF8.GetBytes(info_prijava));
                        }
                        else
                        {
                            info_prijava = "NEUSPJESAN";
                            acceptedSocket.Send(Encoding.UTF8.GetBytes(info_prijava));
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Došlo je do greške: {ex.Message}");
                    break;
                }
            }

            #endregion


            #region Zatvaranje

            Console.WriteLine("Server zavrsava sa radom");
            Console.ReadKey();
            acceptedSocket.Close();
            serverSocket.Close();

            #endregion

        }
    }
}
