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
    public class TransferTransakcija
    {
        public string PosiljalacKorisnickoIme { get; set; } = string.Empty;
        public string PrimalacKorisnickoIme { get; set; } = string.Empty;
        public decimal Kolicina { get; set; }
        public DateTime VremeTransfera { get; set; }

        public TransferTransakcija(string posiljalac, string primalac, decimal kolicina)
        {
            PosiljalacKorisnickoIme = posiljalac;
            PrimalacKorisnickoIme = primalac;
            Kolicina = kolicina;
            VremeTransfera = DateTime.Now;
        }
    }

    [Serializable]
    public enum TipTransakcije
    {
        Uplata,
        Isplata,
        Transfer
    }

}
