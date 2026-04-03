# Arhitektura Sistema - Projekat Banka

Ovaj dokument opisuje arhitekturu distribuiranog bankarskog sistema razvijenog u C#-u. Sistem je dizajniran korišćenjem **klijent-server arhitekture** i oslanja se na komunikaciju putem TCP/IP mrežnih soketa (Sockets).

## 🏗️ Pregled Arhitekture

Sistem se sastoji od četiri glavna modula:

1. **Centralni Server (`Server`)** - Jezgro sistema.
2. **Klijentska Aplikacija (`Client`)** - Aplikacija za krajnje korisnike.
3. **Aplikacija Filijale (`Filijala`)** - Aplikacija za bankarske službenike/lokalne ekspoziture.
4. **Zajednički Domen (`Domain`)** - Deljena biblioteka (Class Library).

---

## 📦 Detaljan opis komponenti

### 1. Zajednički Domen (`Domain`)
Ovo je `Class Library` projekat koji se referencira u svim ostalim projektima (Server, Klijent, Filijala). Služi za standardizaciju podataka koji putuju kroz mrežu.
* **Modeli podataka (Entities):** Klase koje predstavljaju realne objekte (npr. `Korisnik`, `Racun`, `Transakcija`).
* **Mrežni protokoli (Transfer Objects):** Klase koje se serijalizuju i šalju kroz mrežu (npr. `Zahtev` i `Odgovor`).
* **Zajednički interfejsi i enumeracije:** Operacije koje klijent može da zatraži (npr. `Uplata`, `Isplata`, `ProveraStanja`).

### 2. Centralni Server (`Server`)
Server je multithreaded (višenitna) aplikacija koja obezbeđuje centralizovanu obradu podataka i pristup bazi podataka.
* **TCP Listener:** Server neprekidno osluškuje dolazne TCP konekcije na definisanom portu.
* **Obrada Klijenata (Client Handler):** Za svakog povezanog klijenta (bilo da je to `Client` ili `Filijala`), server pokreće novu nit (Thread/Task) kako bi omogućio istovremenu komunikaciju sa više korisnika.
* **Baza podataka / Repozitorijum:** Sadrži logiku za čuvanje i čitanje podataka. Svi zahtevi koji menjaju stanje (npr. transfer novca) prolaze kroz serversku validaciju pre upisa u bazu.

### 3. Klijent (`Client`)
Klijentska aplikacija komunicira direktno sa Centralnim Serverom.
* **Korisnički interfejs (UI):** Omogućava korisniku interakciju sa sistemom (prijava, pregled računa, transfer sredstava).
* **Mrežni klijent (TCP Client):** Pretvara korisničke akcije u mrežne `Zahtev` objekte, serijalizuje ih i šalje serveru. Zatim čeka `Odgovor` od servera i osvežava UI.

### 4. Filijala (`Filijala`)
Filijala predstavlja specijalizovanog klijenta sa dodatnim privilegijama.
* **Lokalna obrada:** Omogućava službenicima banke da vrše odobravanje kredita, otvaranje novih računa i uvid u transakcije.
* **Sinhronizacija:** Komunicira sa Serverom radi sinhronizacije lokalnih podataka ekspoziture sa centralnom bazom podataka.

---

## 🔄 Komunikacioni Protokol (Tok Podataka)

Celokupna komunikacija u sistemu zasniva se na razmeni objekata tipa `Zahtev` (Request) i `Odgovor` (Response).

1. **Slanje zahteva:** Klijent kreira objekat `Zahtev`, popunjava ga podacima (npr. `Operacija = Operacija.Uplata`, `Objekat = transakcija`), serijalizuje ga (najčešće koristeći `BinaryFormatter` ili `JSON`) i šalje preko mrežnog toka (NetworkStream).
2. **Prijem i obrada:** Server prihvata serijalizovani niz bajtova, deserijalizuje ga u `Zahtev`, prepoznaje operaciju i prosleđuje je odgovarajućem kontroleru na serveru.
3. **Slanje odgovora:** Nakon obrade (i eventualnog upisa u bazu), server kreira objekat `Odgovor` (uspešno/neuspešno, povratni podaci, poruka o grešci), serijalizuje ga i šalje nazad klijentu.
4. **Prikaz klijentu:** Klijent deserijalizuje `Odgovor` i prikazuje rezultat korisniku na ekranu.

## 🔒 Bezbednost i Upravljanje Greškama
* **Višenitna sinhronizacija:** S obzirom na to da više klijenata može istovremeno pristupiti istom bankovnom računu, server mora koristiti mehanizme zaključavanja (npr. `lock` u C#-u) kako bi se sprečio *Race Condition* tokom izmene stanja.
* **Obrada mrežnih prekida:** Ukoliko klijent ili server izgube konekciju, sistem mora graceful-no zatvoriti sokete i osloboditi resurse (pomocu `try-catch-finally` blokova).
