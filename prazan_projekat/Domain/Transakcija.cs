using System;

namespace Domain
{
    [Serializable]
    public class Transakcija
    {
        public string Id { get; private set; } // Jedinstveni identifikator transakcije
        public string KorisnickoIme { get; set; } // Korisnik koji vrši transakciju
        public decimal Iznos { get; set; } // Iznos transakcije
        public TipTransakcije Tip { get; set; } // Tip transakcije (uplata ili isplata)
        public DateTime Vreme { get; private set; } // Vreme kada je transakcija obavljena

        public Transakcija(string korisnickoIme, decimal iznos, TipTransakcije tip)
        {
            KorisnickoIme = korisnickoIme;
            Iznos = iznos;
            Tip = tip;
            Id = Guid.NewGuid().ToString(); // automatski dodeli ID
            Vreme = DateTime.Now; // zabeleži trenutni datum i vreme
        }
    }

    [Serializable]
    public enum TipTransakcije
    {
        Uplata,     // Deposit
        Isplata     // Withdrawal
    }
}
