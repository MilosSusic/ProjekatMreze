using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain
{
    internal class Transakcija
    {
        public enum Tip_Transakcije { uplata, isplata, transfer };

        public string IDTransakcije { get; set; }

        public Tip_Transakcije tipTransakcije { get; set; }

        public double IznosTransakcije { get; set; }

        public string Datum { get; set; }

        public Transakcija() { }

        public Transakcija(string iDTransakcije, Tip_Transakcije tipTransakcije, double iznosTransakcije, string datum)
        {
            IDTransakcije = iDTransakcije;
            this.tipTransakcije = tipTransakcije;
            IznosTransakcije = iznosTransakcije;
            Datum = datum;
        }

        public Transakcija() { } 

    }
}
