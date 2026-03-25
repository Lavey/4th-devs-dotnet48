using System.Collections.Generic;
using FourthDevs.Calendar.Models;

namespace FourthDevs.Calendar.Data
{
    public static class ContactStore
    {
        public static readonly List<Contact> Contacts = new List<Contact>
        {
            new Contact
            {
                Id = "c-001",
                Name = "Kasia Nowak",
                Email = "kasia.nowak@techvolt.io",
                Phone = "+48 512 300 100",
                Company = "TechVolt",
                Role = "Engineering Lead",
                Relationship = "colleague",
                Preferences = new List<string> { "vegetarian", "prefers lunch meetings", "avoids Mondays" },
                Notes = "Runs sprint planning. Office in Podgórze. Usually free 11:30–13:30 for lunch.",
            },
            new Contact
            {
                Id = "c-002",
                Name = "Tomek Brandt",
                Email = "tomek.brandt@shopflow.de",
                Phone = "+49 170 555 0812",
                Company = "ShopFlow",
                Role = "CTO",
                Relationship = "client",
                Preferences = new List<string> { "formal meetings", "punctual", "prefers coworking over cafés" },
                Notes = "Based in Berlin, visits Kraków monthly. Key client — webhook delivery issue ongoing.",
            },
            new Contact
            {
                Id = "c-003",
                Name = "Anna Wiśniewska",
                Email = "anna.wisniewska@gmail.com",
                Phone = "+48 600 200 300",
                Relationship = "friend",
                Preferences = new List<string> { "sushi", "japanese food", "evening meetups", "lives in Kazimierz" },
                Notes = "Old university friend. Designer at a Wrocław startup, works remotely from Kraków.",
            },
            new Contact
            {
                Id = "c-004",
                Name = "Piotr Zieliński",
                Email = "piotr@fundwise.vc",
                Phone = "+48 510 777 400",
                Company = "FundWise VC",
                Role = "Partner",
                Relationship = "investor",
                Preferences = new List<string> { "remote-only", "morning calls", "max 30 minutes" },
                Notes = "Seed investor in TechVolt. Quarterly check-ins. Uses Google Meet.",
            },
            new Contact
            {
                Id = "c-005",
                Name = "Marta Kowalska",
                Email = "marta@creativespark.co",
                Phone = "+48 660 100 500",
                Company = "CreativeSpark",
                Role = "Senior Designer",
                Relationship = "colleague",
                Preferences = new List<string> { "creative spaces", "morning person", "coffee addict" },
                Notes = "Leads visual direction. Works from cafés around Planty most mornings.",
            },
            new Contact
            {
                Id = "c-006",
                Name = "Luiza Kowalczyk",
                Email = "luiza.kowalczyk@freelance.design",
                Phone = "+48 790 600 800",
                Relationship = "freelancer",
                Preferences = new List<string> { "flexible schedule", "prefers in-person reviews" },
                Notes = "Senior graphic designer, 320 zł/h. Just delivered CreativeSpark brand refresh.",
            },
        };
    }
}
