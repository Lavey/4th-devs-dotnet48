using System.Collections.Generic;
using System.Linq;
using FourthDevs.Calendar.Models;

namespace FourthDevs.Calendar.Data
{
    public static class RouteStore
    {
        private static readonly List<Route> Routes = new List<Route>
        {
            // From Home (Stare Miasto)
            new Route
            {
                From = "p-home", To = "p-office",
                Walking = new TravelOption { DurationMin = 35, DistanceKm = 2.9 },
                Driving = new TravelOption { DurationMin = 12, DistanceKm = 3.8 },
                Transit = new TransitOption { DurationMin = 20, DistanceKm = 3.5, Line = "Tram #3", Stops = 6, Description = "Tram #3 from Poczta Główna toward Podgórze" },
            },
            new Route
            {
                From = "p-home", To = "p-botanica",
                Walking = new TravelOption { DurationMin = 10, DistanceKm = 0.8 },
                Driving = new TravelOption { DurationMin = 5, DistanceKm = 1.2 },
            },
            new Route
            {
                From = "p-home", To = "p-sakura",
                Walking = new TravelOption { DurationMin = 18, DistanceKm = 1.5 },
                Driving = new TravelOption { DurationMin = 8, DistanceKm = 2.1 },
                Transit = new TransitOption { DurationMin = 12, DistanceKm = 1.8, Line = "Tram #1", Stops = 3, Description = "Tram #1 from Poczta Główna toward Kazimierz" },
            },
            new Route
            {
                From = "p-home", To = "p-cowork",
                Walking = new TravelOption { DurationMin = 20, DistanceKm = 1.6 },
                Driving = new TravelOption { DurationMin = 8, DistanceKm = 2.2 },
                Transit = new TransitOption { DurationMin = 14, DistanceKm = 2.0, Line = "Tram #1", Stops = 4, Description = "Tram #1 from Poczta Główna toward Kazimierz" },
            },
            new Route
            {
                From = "p-home", To = "p-trattoria",
                Walking = new TravelOption { DurationMin = 33, DistanceKm = 2.7 },
                Driving = new TravelOption { DurationMin = 11, DistanceKm = 3.5 },
                Transit = new TransitOption { DurationMin = 18, DistanceKm = 3.2, Line = "Tram #3", Stops = 5, Description = "Tram #3 from Poczta Główna toward Limanowskiego" },
            },
            new Route
            {
                From = "p-home", To = "p-galeria",
                Walking = new TravelOption { DurationMin = 12, DistanceKm = 1.0 },
                Driving = new TravelOption { DurationMin = 5, DistanceKm = 1.5 },
            },

            // From Office (Podgórze)
            new Route
            {
                From = "p-office", To = "p-trattoria",
                Walking = new TravelOption { DurationMin = 6, DistanceKm = 0.5 },
                Driving = new TravelOption { DurationMin = 3, DistanceKm = 0.7 },
            },
            new Route
            {
                From = "p-office", To = "p-cowork",
                Walking = new TravelOption { DurationMin = 15, DistanceKm = 1.2 },
                Driving = new TravelOption { DurationMin = 7, DistanceKm = 1.8 },
                Transit = new TransitOption { DurationMin = 10, DistanceKm = 1.5, Line = "Tram #24", Stops = 3, Description = "Tram #24 from Podgórze toward Kazimierz" },
            },
            new Route
            {
                From = "p-office", To = "p-sakura",
                Walking = new TravelOption { DurationMin = 12, DistanceKm = 1.0 },
                Driving = new TravelOption { DurationMin = 5, DistanceKm = 1.4 },
            },
            new Route
            {
                From = "p-office", To = "p-botanica",
                Walking = new TravelOption { DurationMin = 42, DistanceKm = 3.5 },
                Driving = new TravelOption { DurationMin = 15, DistanceKm = 4.2 },
                Transit = new TransitOption { DurationMin = 25, DistanceKm = 4.0, Line = "Tram #3", Stops = 8, Description = "Tram #3 from Podgórze toward Poczta Główna, then walk" },
            },
            new Route
            {
                From = "p-office", To = "p-home",
                Walking = new TravelOption { DurationMin = 35, DistanceKm = 2.9 },
                Driving = new TravelOption { DurationMin = 12, DistanceKm = 3.8 },
                Transit = new TransitOption { DurationMin = 20, DistanceKm = 3.5, Line = "Tram #3", Stops = 6, Description = "Tram #3 from Podgórze toward Poczta Główna" },
            },

            // Between venues
            new Route
            {
                From = "p-trattoria", To = "p-cowork",
                Walking = new TravelOption { DurationMin = 10, DistanceKm = 0.8 },
                Driving = new TravelOption { DurationMin = 5, DistanceKm = 1.1 },
            },
            new Route
            {
                From = "p-trattoria", To = "p-sakura",
                Walking = new TravelOption { DurationMin = 8, DistanceKm = 0.6 },
                Driving = new TravelOption { DurationMin = 4, DistanceKm = 0.9 },
            },
            new Route
            {
                From = "p-botanica", To = "p-home",
                Walking = new TravelOption { DurationMin = 10, DistanceKm = 0.8 },
                Driving = new TravelOption { DurationMin = 5, DistanceKm = 1.2 },
            },
            new Route
            {
                From = "p-cowork", To = "p-home",
                Walking = new TravelOption { DurationMin = 20, DistanceKm = 1.6 },
                Driving = new TravelOption { DurationMin = 8, DistanceKm = 2.2 },
                Transit = new TransitOption { DurationMin = 14, DistanceKm = 2.0, Line = "Tram #1", Stops = 4, Description = "Tram #1 from Kazimierz toward Poczta Główna" },
            },
            new Route
            {
                From = "p-sakura", To = "p-home",
                Walking = new TravelOption { DurationMin = 18, DistanceKm = 1.5 },
                Driving = new TravelOption { DurationMin = 8, DistanceKm = 2.1 },
                Transit = new TransitOption { DurationMin = 12, DistanceKm = 1.8, Line = "Tram #1", Stops = 3, Description = "Tram #1 from Kazimierz toward Poczta Główna" },
            },
        };

        public static Route FindRoute(string from, string to)
        {
            var direct = Routes.FirstOrDefault(r => r.From == from && r.To == to);
            if (direct != null) return direct;

            var reverse = Routes.FirstOrDefault(r => r.From == to && r.To == from);
            if (reverse == null) return null;

            return new Route
            {
                From = from,
                To = to,
                Walking = reverse.Walking,
                Driving = reverse.Driving,
                Transit = reverse.Transit,
            };
        }
    }
}
