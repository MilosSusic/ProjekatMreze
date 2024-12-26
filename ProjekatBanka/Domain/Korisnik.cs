﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain
{
    public class Korisnik
    {
        public string Id { get; set; }

        public string Ime { get; set; }

        public string Prezime { get; set; }

        public double StanjeNaRacunu { get; set; }

        public Korisnik() { }

        public Korisnik(string id, string ime, string prezime, double stanjeNaRacunu)
        {
            Id = id;
            Ime = ime;
            Prezime = prezime;
            StanjeNaRacunu = stanjeNaRacunu;
        }

        public override string ToString()
        {
            return base.ToString();
        }
    }
}
