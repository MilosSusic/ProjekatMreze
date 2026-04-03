# Projekat Mreže - Bankarski Sistem

Ovaj repozitorijum sadrži projekat iz predmeta Računarske mreže / Mrežno programiranje. Sistem predstavlja distribuiranu aplikaciju za simulaciju rada banke i njenih filijala korišćenjem klijent-server arhitekture u C#-u.

## 📁 Struktura projekta

Projekat je podeljen u nekoliko logičkih celina u okviru `ProjekatBanka.sln` rešenja (solution):

- **Server** - Centralna serverska aplikacija koja osluškuje zahteve klijenata i filijala, obrađuje ih i komunicira sa bazom podataka.
- **Client** - Klijentska aplikacija namenjena krajnjim korisnicima za pristup bankarskim uslugama.
- **Filijala** - Zasebna aplikacija namenjena radnicima u filijali za upravljanje lokalnim operacijama i komunikaciju sa centralnim serverom.
- **Domain** - Deljena biblioteka (Class Library) koja sadrži zajedničke modele, domenske klase i protokole za komunikaciju između klijenta, filijale i servera.
- **Slike** - Direktorijum koji sadrži grafičke resurse i dijagrame arhitekture.
- **postavka** - Originalna postavka zadatka i specifikacija zahteva projekta.

## 🚀 Pokretanje projekta

Da biste uspešno pokrenuli projekat na lokalnoj mašini, pratite sledeće korake:

1. **Kloniranje repozitorijuma:**
   ```bash
   git clone https://github.com/MilosSusic/ProjekatMreze.git
   ```
2. **Otvaranje rešenja:**
   Otvorite `ProjekatBanka.sln` pomoću Visual Studio-a (preporučuje se Visual Studio 2022).

3. **Pokretanje Servera:**
   - Postavite `Server` projekat kao *Startup Project* (Desni klik na projekat `Server` -> `Set as Startup Project`).
   - Pokrenite server i uverite se da uspešno osluškuje na zadatom portu.

4. **Pokretanje Filijale / Klijenta:**
   - Kako biste testirali mrežnu komunikaciju, pokrenite višestruke instance.
   - Možete konfigurisati Visual Studio da pokreće više projekata odjednom:
     - Desni klik na Solution -> `Properties` -> `Startup Project` -> Izaberite `Multiple startup projects`.
     - Podesite `Server`, `Client` (i po potrebi `Filijala`) na akciju **Start**.

## 🛠️ Tehnologije
- **Jezik:** C# (.NET)
- **Komunikacija:** TCP/IP Sockets (pretpostavljeno za mrežne projekte)
- **Arhitektura:** Klijent-Server

## 📄 Dokumentacija
Detaljnija specifikacija sistema i zahteva može se naći u folderu `postavka`. Pre početka rada, preporučuje se upoznavanje sa domenskim klasama definisanim u modulu `Domain`.
