﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Domain;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;

namespace Server
{
    public class Server
    {    

        static void Main(string[] args)
        {

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

            List<Korisnik> rezultati = new List<Korisnik>()
            {
                new Korisnik("123747","Milos","Susic",0),
                new Korisnik("214532","Bozana","Todorovic",0)
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
                        rezultati.Add(korisnik);

                        Console.WriteLine("Primljen rezultat:");
                        Console.WriteLine($"Id: {korisnik.IdKorisnik}, Ime: {korisnik.Ime}, Prezime: {korisnik.Prezime}");
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
