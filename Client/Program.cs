using Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.ServiceModel;

namespace Client
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== EV Punjac - Klijentska aplikacija ===\n");

            string dataRoot = "Data";
            if (!Directory.Exists(dataRoot))
            {
                Console.WriteLine("[GRESKA] Folder 'Data' ne postoji.");
                Console.ReadLine();
                return;
            }

            string[] vehicleFolders = Directory.GetDirectories(dataRoot);

            if (vehicleFolders.Length == 0)
            {
                Console.WriteLine("[GRESKA] Nema foldera sa vozilima.");
                Console.ReadLine();
                return;
            }

            Console.WriteLine($"Dostupna vozila ({vehicleFolders.Length}):\n");
            for (int i = 0; i < vehicleFolders.Length; i++)
            {
                string name = Path.GetFileName(vehicleFolders[i]);
                string csv = Path.Combine(vehicleFolders[i], "Charging_Profile.csv");
                string status = File.Exists(csv) ? "OK" : "Nema CSV fajla!";
                Console.WriteLine($"  [{i + 1}] {name}  ({status})");
            }

            Console.Write("\nUnesite broj vozila: ");
            if (!int.TryParse(Console.ReadLine(), out int choice)
                || choice < 1 || choice > vehicleFolders.Length)
            {
                Console.WriteLine("[GRESKA] Nevazeći izbor.");
                Console.ReadLine();
                return;
            }

            string selectedFolder = vehicleFolders[choice - 1];
            string vehicleId = Path.GetFileName(selectedFolder);
            string csvPath = Path.Combine(selectedFolder, "Charging_Profile.csv");

            if (!File.Exists(csvPath))
            {
                Console.WriteLine($"[GRESKA] Fajl ne postoji: {csvPath}");
                Console.ReadLine();
                return;
            }

            Console.WriteLine($"\n[KLIJENT] Izabrano vozilo: {vehicleId}");
            Console.WriteLine("[KLIJENT] Povezivanje na servis...\n");

            ChannelFactory<IChargingService> factory = null;
            IChargingService proxy = null;
            List<string> errorLog = new List<string>();
            int sentCount = 0;
            int rejectedCount = 0;

            try
            {
                factory = new ChannelFactory<IChargingService>("ChargingService");
                proxy = factory.CreateChannel();

                proxy.StartSession(vehicleId);
                Console.WriteLine($"[KLIJENT] Sesija pokrenuta za: {vehicleId}\n");

                using (CsvReader reader = new CsvReader(csvPath))
                {
                    foreach (var (data, isValid) in reader.ReadRows(vehicleId, errorLog))
                    {
                        if (!isValid) continue;

                        try
                        {
                            proxy.PushSample(data);
                            sentCount++;
                            Console.WriteLine($"[KLIJENT] Red {data.RowIndex} poslan.");
                        }
                        catch (FaultException<ChargingFault> ex)
                        {
                            rejectedCount++;
                            string msg = $"Red {ex.Detail.RowIndex} odbijen: {ex.Detail.Reason}";
                            Console.WriteLine($"[KLIJENT] {msg}");
                            errorLog.Add(msg);
                        }
                        catch (CommunicationException ex)
                        {
                            Console.WriteLine($"[KLIJENT] Prekid konekcije: {ex.Message}");
                            break;
                        }
                    }
                }

                proxy.EndSession(vehicleId);
                Console.WriteLine($"\n[KLIJENT] Sesija zatvorena.");
                Console.WriteLine($"[KLIJENT] Poslano: {sentCount}, Odbijeno: {rejectedCount}");
            }
            catch (EndpointNotFoundException)
            {
                Console.WriteLine("[GRESKA] Servis nije dostupan. Pokreni Service.exe prvo!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GRESKA] {ex.Message}");
            }
            finally
            {
                CloseProxy(proxy);
                CloseFactory(factory);

                if (errorLog.Count > 0)
                {
                    string logFile = $"error_log_{vehicleId}_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
                    File.WriteAllLines(logFile, errorLog);
                    Console.WriteLine($"\n[KLIJENT] Log sacuvan: {logFile}");
                }
            }

            Console.WriteLine("\nPritisnite Enter za izlaz...");
            Console.ReadLine();
        }

        private static void CloseProxy(IChargingService proxy)
        {
            if (proxy == null) return;
            try { ((IClientChannel)proxy).Close(); }
            catch { ((IClientChannel)proxy).Abort(); }
        }

        private static void CloseFactory(ChannelFactory<IChargingService> factory)
        {
            if (factory == null) return;
            try { factory.Close(); }
            catch { factory.Abort(); }
        }
    }
}