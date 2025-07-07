using System;

namespace Domain
{
    [Serializable]
    public class Transakcija
    {
        public string KorisnickoIme { get; set; } = string.Empty;
        public decimal Kolicina { get; set; }
        public TipTransakcije Tip { get; set; }

        public Transakcija(string korisnickoIme, decimal kolicina, TipTransakcije tip)
        {
            KorisnickoIme = korisnickoIme;
            Kolicina = kolicina;
            Tip = tip;
        }
    }

    [Serializable]
    public enum TipTransakcije
    {
        Uplata,
        Isplata
    }
}
