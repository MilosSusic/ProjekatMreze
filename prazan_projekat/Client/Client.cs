using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Runtime.Serialization;

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

            while (true)
            {
                Console.WriteLine("Unesite id korisnika");
                string id = Console.ReadLine();
                Console.WriteLine("Unesite ime korisnika");
                string ime = Console.ReadLine();
                Console.WriteLine("Unesite prezime korisnika");
                string prezime = Console.ReadLine();


              

             

            }
        }
    }
}
