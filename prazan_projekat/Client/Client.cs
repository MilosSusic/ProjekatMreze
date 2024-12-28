using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Runtime.Serialization;
using Domain;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Serialization.Formatters.Binary;


namespace Client
{
    internal class Client
    {
        static void Main(string[] args)
        {

            #region Povezivanje

            Socket clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            IPEndPoint serverEP = new IPEndPoint(IPAddress.Loopback, 50001);
            byte[] buffer = new byte[1024];


            clientSocket.Connect(serverEP);
            Console.WriteLine("Klijent je uspesno povezan sa serverom!");
            #endregion

            BinaryFormatter formatter = new BinaryFormatter();

            #region Slanje testova
            while (true)
            {
                Console.WriteLine("Unesite id korisnika");
                string id = Console.ReadLine();
                Console.WriteLine("Unesite ime korisnika");
                string ime = Console.ReadLine();
                Console.WriteLine("Unesite prezime korisnika");
                string prezime = Console.ReadLine();

                Korisnik korisnik = new Korisnik
                {
                    IdKorisnik = id,
                    Ime = ime,
                    Prezime = prezime
                };


                using (MemoryStream ms = new MemoryStream())
                {
                    formatter.Serialize(ms, korisnik);
                    byte[] data = ms.ToArray();
                    clientSocket.Send(data);
                }

                clientSocket.Receive(buffer);
                string poruka = Encoding.UTF8.GetString(buffer);
                Console.WriteLine("Odgovor je:"+poruka);

            }
            #endregion


            #region Zatvaranje

            Console.WriteLine("Klijent zavrsava sa radom");
            Console.ReadKey();
            clientSocket.Close();
            #endregion
        }
    }
}
