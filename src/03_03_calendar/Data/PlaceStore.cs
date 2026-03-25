using System.Collections.Generic;
using FourthDevs.Calendar.Models;

namespace FourthDevs.Calendar.Data
{
    public static class PlaceStore
    {
        public static readonly List<Place> Places = new List<Place>
        {
            new Place
            {
                Id = "p-home",
                Name = "Adam's Apartment",
                Type = "home",
                Address = "ul. Floriańska 15, 31-019 Kraków",
                Coordinates = new Coordinates { Lat = 50.0637, Lng = 19.9390 },
                Tags = new List<string> { "home", "stare miasto", "old town" },
                Description = "Home address in the heart of Old Town Kraków.",
            },
            new Place
            {
                Id = "p-office",
                Name = "TechVolt Office",
                Type = "office",
                Address = "ul. Przemysłowa 12, 30-701 Kraków",
                Coordinates = new Coordinates { Lat = 50.0440, Lng = 19.9530 },
                OpeningHours = new Dictionary<string, string>
                {
                    { "mon", "08:00-18:00" }, { "tue", "08:00-18:00" }, { "wed", "08:00-18:00" },
                    { "thu", "08:00-18:00" }, { "fri", "08:00-18:00" },
                },
                Tags = new List<string> { "office", "work", "techvolt", "podgórze" },
                Phone = "+48 12 345 6700",
                Website = "https://techvolt.io",
                Description = "TechVolt HQ in Podgórze. Open-plan office with 2 meeting rooms and a small kitchen.",
            },
            new Place
            {
                Id = "p-trattoria",
                Name = "Trattoria Milano",
                Type = "restaurant",
                Address = "ul. Limanowskiego 24, 30-551 Kraków",
                Coordinates = new Coordinates { Lat = 50.0455, Lng = 19.9495 },
                OpeningHours = new Dictionary<string, string>
                {
                    { "mon", "11:00-22:00" }, { "tue", "11:00-22:00" }, { "wed", "11:00-22:00" },
                    { "thu", "11:00-22:00" }, { "fri", "11:00-23:00" },
                    { "sat", "12:00-23:00" }, { "sun", "12:00-21:00" },
                },
                Tags = new List<string> { "italian", "restaurant", "lunch", "pasta", "pizza", "podgórze", "near office" },
                Phone = "+48 12 345 6780",
                Website = "https://trattoria-milano.pl",
                Description = "Authentic Italian trattoria 5 minutes from TechVolt office. " +
                              "Good vegetarian options. Lunch menu 35–55 zł. Reservations recommended Thu–Sat.",
            },
            new Place
            {
                Id = "p-sakura",
                Name = "Sakura Sushi",
                Type = "restaurant",
                Address = "ul. Józefa 8, 31-056 Kraków",
                Coordinates = new Coordinates { Lat = 50.0510, Lng = 19.9465 },
                OpeningHours = new Dictionary<string, string>
                {
                    { "mon", "12:00-22:00" }, { "tue", "12:00-22:00" }, { "wed", "12:00-22:00" },
                    { "thu", "12:00-22:00" }, { "fri", "12:00-23:00" },
                    { "sat", "12:00-23:00" }, { "sun", "13:00-21:00" },
                },
                Tags = new List<string> { "sushi", "japanese", "restaurant", "dinner", "kazimierz" },
                Phone = "+48 12 430 2200",
                Website = "https://sakura-sushi.krakow.pl",
                Description = "Top-rated sushi in Kazimierz. Omakase sets, sake bar, intimate atmosphere. " +
                              "Dinner for two: 180–280 zł. Booking essential on weekends.",
            },
            new Place
            {
                Id = "p-cowork",
                Name = "CoWork Kazimierz",
                Type = "coworking",
                Address = "ul. Meiselsa 6, 31-058 Kraków",
                Coordinates = new Coordinates { Lat = 50.0505, Lng = 19.9450 },
                OpeningHours = new Dictionary<string, string>
                {
                    { "mon", "08:00-20:00" }, { "tue", "08:00-20:00" }, { "wed", "08:00-20:00" },
                    { "thu", "08:00-20:00" }, { "fri", "08:00-20:00" }, { "sat", "09:00-16:00" },
                },
                Tags = new List<string> { "coworking", "meetings", "kazimierz", "meeting room" },
                Phone = "+48 12 307 1500",
                Website = "https://cowork-kazimierz.pl",
                Description = "Modern coworking hub in Kazimierz. Meeting rooms bookable by the hour (60 zł/h). " +
                              "Good coffee, fast Wi-Fi, projector available.",
            },
            new Place
            {
                Id = "p-botanica",
                Name = "Café Botanica",
                Type = "cafe",
                Address = "al. Słowackiego 1, 31-159 Kraków",
                Coordinates = new Coordinates { Lat = 50.0660, Lng = 19.9370 },
                OpeningHours = new Dictionary<string, string>
                {
                    { "mon", "08:00-18:00" }, { "tue", "08:00-18:00" }, { "wed", "08:00-18:00" },
                    { "thu", "08:00-18:00" }, { "fri", "08:00-18:00" },
                    { "sat", "09:00-17:00" }, { "sun", "09:00-16:00" },
                },
                Tags = new List<string> { "café", "coffee", "planty", "creative", "quiet", "work-friendly" },
                Phone = "+48 12 290 8800",
                Description = "Quiet café on the edge of Planty park. Specialty coffee, good pastries. " +
                              "Popular with creatives — big communal table, power outlets everywhere.",
            },
            new Place
            {
                Id = "p-galeria",
                Name = "Galeria Krakowska",
                Type = "mall",
                Address = "ul. Pawia 5, 31-154 Kraków",
                Coordinates = new Coordinates { Lat = 50.0680, Lng = 19.9450 },
                OpeningHours = new Dictionary<string, string>
                {
                    { "mon", "09:00-21:00" }, { "tue", "09:00-21:00" }, { "wed", "09:00-21:00" },
                    { "thu", "09:00-21:00" }, { "fri", "09:00-21:00" },
                    { "sat", "09:00-21:00" }, { "sun", "10:00-20:00" },
                },
                Tags = new List<string> { "mall", "shopping", "central", "train station" },
                Website = "https://galeriakrakowska.pl",
                Description = "Main shopping mall next to Kraków Główny train station. Food court on level 2.",
            },
        };
    }
}
