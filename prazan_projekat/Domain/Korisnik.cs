﻿using System;
using System.Collections.Generic;

namespace Domain
{

    [Serializable]
    public class Korisnik
    {
        public string KorisnickoIme { get; set; }
        public string Sifra { get; set; }
        public string Ime { get; set; }
        public string Prezime { get; set; }
        public decimal Stanje { get; set; }
        public decimal MaxSumaZaIsplatu { get; set; }
        public string BrojRacuna { get; set; }

        public Korisnik()
        {
            KorisnickoIme = string.Empty;
            Sifra = string.Empty;
            Ime = string.Empty;
            Prezime = string.Empty;
            BrojRacuna = GenerisiBrojRacuna();
        }

        private static string GenerisiBrojRacuna()
        {
            return "ACC-" + Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();
        }
    }
}
