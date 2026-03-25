using System.Collections.Generic;
using System.Linq;
using FourthDevs.Calendar.Models;

namespace FourthDevs.Calendar.Data
{
    public static class WebSearchStore
    {
        private static readonly List<WebSearchEntry> SearchEntries = new List<WebSearchEntry>
        {
            new WebSearchEntry
            {
                Keywords = new List<string> { "italian", "restaurant", "podgórze", "near office", "trattoria" },
                Results = new List<WebSearchResult>
                {
                    new WebSearchResult
                    {
                        Title = "Trattoria Milano — Authentic Italian in Podgórze",
                        Url = "https://trattoria-milano.pl",
                        Snippet = "Cozy Italian restaurant on ul. Limanowskiego 24. Handmade pasta, wood-fired pizza, " +
                                  "excellent vegetarian options. Lunch menu 35–55 zł. Open daily from 11:00.",
                    },
                    new WebSearchResult
                    {
                        Title = "Trattoria Milano – Google Maps Reviews (4.6★)",
                        Url = "https://maps.google.com/trattoria-milano-krakow",
                        Snippet = "4.6 out of 5 stars, 820 reviews. \"Best carbonara in Kraków.\" \"Great lunch spot near Podgórze.\" " +
                                  "\"Vegetarian risotto is amazing.\" Reservations recommended.",
                    },
                    new WebSearchResult
                    {
                        Title = "Best Italian Restaurants in Kraków 2026 — Local Eats",
                        Url = "https://localeats.pl/krakow/italian",
                        Snippet = "Top picks: 1) Trattoria Milano (Podgórze) — classic Roman cuisine, 2) La Campana (Stare Miasto) — upscale, " +
                                  "3) Pasta Fresca (Kazimierz) — casual and cheap.",
                    },
                },
            },
            new WebSearchEntry
            {
                Keywords = new List<string> { "sushi", "japanese", "restaurant", "kraków", "best" },
                Results = new List<WebSearchResult>
                {
                    new WebSearchResult
                    {
                        Title = "Sakura Sushi — Kazimierz, Kraków",
                        Url = "https://sakura-sushi.krakow.pl",
                        Snippet = "Premium sushi bar on ul. Józefa 8 in Kazimierz. Omakase sets, fresh nigiri, sake selection. " +
                                  "Dinner for two: 180–280 zł. Open Tue–Sun from 12:00. Booking essential on weekends.",
                    },
                    new WebSearchResult
                    {
                        Title = "Sakura Sushi – Google Maps Reviews (4.8★)",
                        Url = "https://maps.google.com/sakura-sushi-krakow",
                        Snippet = "4.8 out of 5 stars, 1,240 reviews. \"Best sushi in Kraków, period.\" \"Omakase was incredible.\" " +
                                  "\"Intimate space, perfect for a date.\" Reserve 2–3 days ahead for Friday/Saturday.",
                    },
                    new WebSearchResult
                    {
                        Title = "Top 5 Sushi Spots in Kraków — Foodie Guide 2026",
                        Url = "https://krakowfoodie.com/sushi-2026",
                        Snippet = "1) Sakura Sushi (Kazimierz) — top-tier omakase, 2) Edo Sushi (Nowa Huta) — great value, " +
                                  "3) Youmiko Vegan Sushi (Stare Miasto) — creative plant-based rolls.",
                    },
                },
            },
            new WebSearchEntry
            {
                Keywords = new List<string> { "coworking", "kazimierz", "meeting room", "workspace" },
                Results = new List<WebSearchResult>
                {
                    new WebSearchResult
                    {
                        Title = "CoWork Kazimierz — Flexible Workspace & Meeting Rooms",
                        Url = "https://cowork-kazimierz.pl",
                        Snippet = "Modern coworking on ul. Meiselsa 6. Hot desks, private offices, and bookable meeting rooms (60 zł/h). " +
                                  "Fast Wi-Fi, projector, coffee included. Open Mon–Fri 8–20, Sat 9–16.",
                    },
                    new WebSearchResult
                    {
                        Title = "CoWork Kazimierz Reviews — Google Maps (4.5★)",
                        Url = "https://maps.google.com/cowork-kazimierz",
                        Snippet = "4.5 stars, 310 reviews. \"Perfect for client meetings.\" \"Clean, professional, great coffee.\" " +
                                  "\"Meeting room has a projector and whiteboard.\"",
                    },
                },
            },
            new WebSearchEntry
            {
                Keywords = new List<string> { "café", "cafe", "planty", "coffee", "botanica" },
                Results = new List<WebSearchResult>
                {
                    new WebSearchResult
                    {
                        Title = "Café Botanica — Specialty Coffee on Planty",
                        Url = "https://cafebotanica.pl",
                        Snippet = "Quiet café on al. Słowackiego 1, right by Planty park. Specialty coffee, homemade pastries, " +
                                  "work-friendly atmosphere. Big communal table, plenty of outlets. Open Mon–Fri 8–18.",
                    },
                    new WebSearchResult
                    {
                        Title = "Best Cafés for Remote Work in Kraków — Digital Nomad Guide",
                        Url = "https://remotekrakow.com/cafes",
                        Snippet = "Top picks: Café Botanica (Planty) — quiet, great coffee, power outlets. Bunkier Café (Planty) — " +
                                  "artsy vibe. Wesoła Café (Wesoła) — spacious, good lunch menu.",
                    },
                },
            },
            new WebSearchEntry
            {
                Keywords = new List<string> { "restaurant", "vegetarian", "lunch", "kraków" },
                Results = new List<WebSearchResult>
                {
                    new WebSearchResult
                    {
                        Title = "Best Vegetarian-Friendly Restaurants in Kraków",
                        Url = "https://krakowfoodie.com/vegetarian-2026",
                        Snippet = "Top options: Trattoria Milano (great veggie risotto + pasta), Glonojad (fully vegan), " +
                                  "Café Botanica (good salads and quiche). Most restaurants in Kazimierz have solid veggie options.",
                    },
                },
            },
            new WebSearchEntry
            {
                Keywords = new List<string> { "kasia", "nowak", "techvolt" },
                Results = new List<WebSearchResult>
                {
                    new WebSearchResult
                    {
                        Title = "Kasia Nowak — Engineering Lead at TechVolt | LinkedIn",
                        Url = "https://linkedin.com/in/kasia-nowak",
                        Snippet = "Engineering Lead at TechVolt. Based in Kraków. Manages a team of 6 engineers. " +
                                  "Previously at Allegro and Brainly. Kraków University of Technology, CS.",
                    },
                },
            },
            new WebSearchEntry
            {
                Keywords = new List<string> { "tomek", "brandt", "shopflow" },
                Results = new List<WebSearchResult>
                {
                    new WebSearchResult
                    {
                        Title = "Tomek Brandt — CTO at ShopFlow | LinkedIn",
                        Url = "https://linkedin.com/in/tomek-brandt",
                        Snippet = "CTO at ShopFlow (Berlin). E-commerce platform processing 2M orders/month. " +
                                  "Frequent visitor to Kraków for partner meetings.",
                    },
                },
            },
            new WebSearchEntry
            {
                Keywords = new List<string> { "piotr", "zieliński", "fundwise" },
                Results = new List<WebSearchResult>
                {
                    new WebSearchResult
                    {
                        Title = "Piotr Zieliński — Partner at FundWise VC",
                        Url = "https://fundwise.vc/team/piotr",
                        Snippet = "Seed-stage investor focused on B2B SaaS in CEE. Portfolio includes TechVolt, DataLens, CloudHQ. " +
                                  "Based in Warsaw, takes meetings remotely via Google Meet.",
                    },
                },
            },
        };

        public static List<WebSearchResult> Search(string query)
        {
            string q = query.ToLowerInvariant();
            var scored = SearchEntries
                .Select(entry => new
                {
                    Entry = entry,
                    Score = entry.Keywords.Count(kw => q.Contains(kw.ToLowerInvariant())),
                })
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .ToList();

            var results = new List<WebSearchResult>();
            foreach (var item in scored)
                results.AddRange(item.Entry.Results);
            return results;
        }
    }
}
