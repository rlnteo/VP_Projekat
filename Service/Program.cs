using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ServiceModel;

namespace Service
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("--- EV punjac : Serverska aplikacija ---\n");
            ChargingService service = new ChargingService();

            service.OnTransferStarted += (s, e) => Console.WriteLine($"[DOGADJAJ] Transfer zapocet - vozilo: {e.VehicleId} ({e.Time:HH:mm:ss})");
            service.OnSampleReceived += (s, e) => Console.WriteLine($"[DOGADJAJ] Uzorak primljen - vozilo: {e.VehicleId}, red: {e.RowIndex}");
            service.OnTransferCompleted += (s, e) => Console.WriteLine($"[DOGADJAJ] Transfer zavrsen - vozilo: {e.VehicleId} ({e.Time:HH:mm:ss})");
            service.OnWarningRaised += (s, e) => Console.WriteLine($"[UPOZORENJE][{e.Type}] vozilo={e.VehicleId}, red={e.RowIndex}: {e.Message}");

            ServiceHost host = null;
            try
            {
                host = new ServiceHost(service);
                host.Open();

                Console.WriteLine("[SERVER] Servis pokrenut. Cekam klijente...");
                Console.WriteLine("[SERVER] Pritisnite Enter za zaustavljanje.");
                Console.ReadLine();
            }
            catch(Exception ex)
            {
                Console.WriteLine($"[SERVER] Greska: {ex.Message}");
            }
            finally
            {
                if (host != null && host.State == CommunicationState.Opened)
                {
                    host.Close();
                }

                Console.WriteLine("[SERVER] Servis zaustavljen.");
            }
        }
    }
}
