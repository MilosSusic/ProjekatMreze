using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Domain;

namespace Server
{
    public class Server
    {    

        static void Main(string[] args)
        {
            byte[] buffer = new byte[4096];

            List<Korisnik> rezultati = new List<Korisnik>()
            {
                new Korisnik("123747","Milos","Susic",0),
                new Korisnik("214532","Bozana","Todorovic",0)
            };



        }
    }
}
