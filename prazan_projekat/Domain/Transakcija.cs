using System;

namespace Domain
{
    [Serializable]
    public class Transakcija
    {
        public string Id { get; private set; } 
        public string KorisnickoIme { get; set; } 
        public decimal Iznos { get; set; } 
        public TipTransakcije Tip { get; set; } 
        public DateTime Vreme { get; private set; } 

        public Transakcija(string korisnickoIme, decimal iznos, TipTransakcije tip)
        {
            KorisnickoIme = korisnickoIme;
            Iznos = iznos;
            Tip = tip;
            Id = Guid.NewGuid().ToString(); 
            Vreme = DateTime.Now; 
        }
    }

    [Serializable]
    public enum TipTransakcije
    {
        Uplata,     
        Isplata    
    }
}
