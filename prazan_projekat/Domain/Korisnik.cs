using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain
{
    [Serializable]
    public class Korisnik
    {
        List<Korisnik> korisnici = new List<Korisnik>();
        public string IdKorisnik { get; set; }

        public string Ime { get; set; }

        public string Prezime { get; set; }

        public double StanjeNaRacunu { get; set; }

        public Korisnik() { }

        public Korisnik(string id, string ime, string prezime, double stanjeNaRacunu)
        {
            IdKorisnik = id;
            Ime = ime;
            Prezime = prezime;
            StanjeNaRacunu = stanjeNaRacunu;
        }
        public bool Uspjesnost(List<Korisnik> korisnici)
        {
            Korisnik korisnik = new Korisnik();
             foreach(var k in korisnici)
            {
                if(k.IdKorisnik == korisnik.IdKorisnik)
                {
                    return true;
                }
            }
            return false;
        }
        public override string ToString()
        {
            return base.ToString();
        }
    }
}
