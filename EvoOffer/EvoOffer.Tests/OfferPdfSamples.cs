using EvoOffer.Models;
using EvoOffer.Services;

internal sealed record OfferPdfSample(string Name, OfferPdfData Offer, OfferPdfOptions Options);

internal static class OfferPdfSamples
{
    public static IReadOnlyList<OfferPdfSample> Create(OfferPdfOptions defaults)
    {
        var referenceIssuer = new AppSettings
        {
            IssuerName = "NEXORA SOLUTIONS",
            AddressLine1 = "Str. Aviației 23, Clădirii NEXA, et. 4",
            AddressLine2 = "030012 București, România",
            Email = "contact@nexora.ro",
            PhoneNumber = "+40 721 234 567",
            Language = AppSettings.Romanian
        };
        var referenceItems = new[]
        {
            new OfferLineItem(1, new CatalogItem("Procesor Intel® Core™ i7, 16GB RAM, 512GB SSD", "Laptop profesional NEXORA Pro 15", 3250m), 2m, 19m),
            new OfferLineItem(2, new CatalogItem("IPS, 2560 × 1440, 75Hz", "Monitor 27\" UltraSharp MS2721QS", 1150m), 3m, 19m),
            new OfferLineItem(3, new CatalogItem("Abonament anual (per utilizator)", "Licență software NEXORA Office Suite", 420m), 10m, 19m),
            new OfferLineItem(4, new CatalogItem("Instalare, configurare și training", "Servicii implementare și configurare", 1800m), 1m, 19m),
            new OfferLineItem(5, new CatalogItem("Abonament anual", "Suport tehnic dedicat", 960m), 1m, 19m)
        };
        var reference = new OfferPdfData("Compania Exemplu S.R.L.",
            "Vă mulțumim pentru interesul acordat serviciilor și produselor companiei noastre.\n\n" +
            "În baza solicitării primite, vă prezentăm oferta comercială detaliată mai jos.\n\n" +
            "Suntem dedicați să oferim soluții de încredere, la cele mai înalte standarde de calitate și competitivitate.",
            referenceItems, referenceIssuer);

        var english = new OfferPdfData("Example Company Ltd.",
            "Thank you for your interest in our products and services.\n\n" +
            "Please find the proposed equipment and implementation services below. This offer includes item-specific VAT rates.",
            new[]
            {
                new OfferLineItem(1, new CatalogItem("Hardware and accessories", "Professional workstation", 3250m), 2m, 21m),
                new OfferLineItem(2, new CatalogItem("Annual subscription", "Technical support", 960m), 1m, 9.5m),
                new OfferLineItem(3, new CatalogItem("Training", "On-site implementation workshop", 1800m), 1.5m, 0m)
            },
            new AppSettings
            {
                IssuerName = "NEXORA SOLUTIONS", AddressLine1 = "23 Aviatiei Street", AddressLine2 = "Bucharest, Romania",
                Email = "contact@example.com", PhoneNumber = "+40 721 234 567", VatNumber = "12345678", Language = AppSettings.English
            });

        var multipage = new OfferPdfData("Compania Exemplu — proiect de modernizare",
            "Lucrări, echipamente și materiale pentru modernizarea sediului. Cantitățile și cotele TVA sunt specifice fiecărui reper.",
            Enumerable.Range(1, 150).Select(number => new OfferLineItem(number,
                new CatalogItem("Instalații, materiale și accesorii", $"Reper {number}: țeavă, îmbinări și accesorii pentru încălzire", 12.34m),
                2.5m, number % 3 == 0 ? 9.5m : 21m)), referenceIssuer);

        var stressIssuer = new AppSettings
        {
            IssuerName = "Compania de Soluții Tehnologice și Consultanță pentru Transformare Digitală S.R.L.",
            AddressLine1 = "Bulevardul Independenței nr. 123, Clădirea Centrului de Cercetare și Dezvoltare, etaj 12",
            AddressLine2 = "Sector 6, București, România, cod poștal 060042",
            Email = "departamentul.comercial.si.consultanta@example-company.ro",
            PhoneNumber = "+40 721 234 567, interior 1234", VatNumber = "1234567890", Language = AppSettings.Romanian
        };
        var stress = new OfferPdfData(
            "Asociația pentru Dezvoltarea Infrastructurii și Implementarea Proiectelor Tehnologice — Filiala București",
            string.Join("\n\n", Enumerable.Repeat(
                "Prezenta propunere include analiza, proiectarea, instalarea și configurarea soluțiilor solicitate. " +
                "Vom coordona etapele de implementare împreună cu echipa beneficiarului, conform cerințelor proiectului.", 12)),
            new[]
            {
                new OfferLineItem(123456, new CatalogItem("Echipamente speciale și servicii de integrare",
                    "Sistem profesional pentru procesarea și arhivarea informațiilor în cadrul proiectului de modernizare", 1234567.89m),
                    QuantityValue.Maximum, 21m),
                new OfferLineItem(123457, new CatalogItem("Cantitate fracționară", "Serviciu calculat proporțional", 0.05m),
                    1.1234567890123456789012345678m, 9.5m),
                new OfferLineItem(123458, new CatalogItem("Documentație detaliată", string.Join(" ", Enumerable.Repeat(
                    "Analiză și implementare, documentație tehnică și instruirea echipei beneficiarului.", 90)), 120m), 1m, 0m)
            }, stressIssuer);

        return new[]
        {
            new OfferPdfSample("sample-reference", reference, defaults with
                { Tagline = "SOLUȚII. SIMPLU. EFICIENT.", ValidUntil = new DateOnly(2026, 10, 22) }),
            new OfferPdfSample("sample-english", english, defaults with
                { Tagline = "SOLUTIONS. SIMPLE. EFFICIENT.", ValidUntil = new DateOnly(2026, 10, 22) }),
            new OfferPdfSample("sample-multipage", multipage, defaults with
                { Tagline = "SOLUȚII. SIMPLU. EFICIENT." }),
            new OfferPdfSample("sample-stress", stress, defaults with
                { ValidUntil = new DateOnly(2026, 10, 22) })
        };
    }
}
