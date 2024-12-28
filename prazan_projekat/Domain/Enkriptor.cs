using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain
{
    public class Enkriptor
    {
        public string Enkriptuj(string podaci)
        {
            // Konvertovanje podataka u bajtove
            byte[] podaciBajtovi = Encoding.UTF8.GetBytes(podaci);

            // Konvertovanje bajtova u Base64 string
            string enkriptovaniPodaci = Convert.ToBase64String(podaciBajtovi);

            return enkriptovaniPodaci;
        }


        // Metoda za dekripciju (dekodiranje iz Base64)
        public string Dekriptuj(string enkriptovaniPodaci)
        {
            // Konvertovanje Base64 string-a u bajtove
            byte[] podaciBajtovi = Convert.FromBase64String(enkriptovaniPodaci);

            // Konvertovanje bajtova nazad u string
            string dekriptovaniPodaci = Encoding.UTF8.GetString(podaciBajtovi);

            return dekriptovaniPodaci;
        }
    }
}
