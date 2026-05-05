using Common;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.ServiceModel;

namespace Service
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single)]
    public class ChargingService : IChargingService
    {
        private readonly Dictionary<string, StreamWriter> _writers = new Dictionary<string, StreamWriter>();
        private readonly Dictionary<string, string> _rejectPaths = new Dictionary<string, string>();
        private readonly Dictionary<string, ChargingData> _lastSample = new Dictionary<string, ChargingData>();

        private readonly double _voltageThreshold;
        private readonly double _currentThreshold;
        private readonly double _powerFactorThreshold;

        public delegate void TransferHandler(object sender, TransferEventArgs e);
        public delegate void SampleHandler(object sender, SampleEventArgs e);
        public delegate void WarningHandler(object sender, WarningEventArgs e);

        public event TransferHandler OnTransferStarted;
        public event SampleHandler OnSampleReceived;
        public event TransferHandler OnTransferCompleted;
        public event WarningHandler OnWarningRaised;

        public ChargingService()
        {
            _voltageThreshold = ReadDouble("VoltageThreshold", 50.0);
            _currentThreshold = ReadDouble("CurrentThreshold", 10.0);
            _powerFactorThreshold = ReadDouble("PowerFactorThreshold", 0.85);
        }

        private static double ReadDouble(string key, double fallback)
        {
            return double.TryParse(ConfigurationManager.AppSettings[key],
                                   NumberStyles.Any, CultureInfo.InvariantCulture,
                                   out double v) ? v : fallback;
        }

        public void StartSession(string vehicleId)
        {
            try
            {
                string basePath = ConfigurationManager.AppSettings["DataPath"] ?? "Data";
                string dateFolder = DateTime.Now.ToString("yyyy-MM-dd");
                string sessionDir = Path.Combine(basePath, vehicleId, dateFolder);

                if (!Directory.Exists(sessionDir))
                    Directory.CreateDirectory(sessionDir);

                string sessionFile = Path.Combine(sessionDir, "session.csv");
                string rejectFile = Path.Combine(sessionDir, "rejects.csv");

                bool isNew = !File.Exists(sessionFile);
                StreamWriter writer = new StreamWriter(sessionFile, append: true);
                if (isNew)
                    writer.WriteLine(ChargingData.CsvHeader());

                _writers[vehicleId] = writer;
                _rejectPaths[vehicleId] = rejectFile;

                Console.WriteLine($"[SERVER] Sesija zapoceta: {vehicleId}");
                OnTransferStarted?.Invoke(this, new TransferEventArgs(vehicleId));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SERVER] Greska: {ex.Message}");
            }
        }

        public bool PushSample(ChargingData data)
        {
            if (!Validate(data, out string reason))
            {
                AppendReject(data, reason);
                OnWarningRaised?.Invoke(this, new WarningEventArgs(
                    data.VehicleId, data.RowIndex,
                    $"Validacija neuspesna: {reason}",
                    WarningType.ValidationFailed));

                throw new FaultException<ChargingFault>(
                    new ChargingFault { Reason = reason, RowIndex = data.RowIndex },
                    new FaultReason(reason));
            }

            RunAnalytics(data);

            if (_writers.ContainsKey(data.VehicleId))
            {
                _writers[data.VehicleId].WriteLine(data.ToCsvLine());
                _writers[data.VehicleId].Flush();
                Console.WriteLine($"[SERVER] Prenos u toku - vozilo: {data.VehicleId}, red: {data.RowIndex}");
            }

            _lastSample[data.VehicleId] = data;
            OnSampleReceived?.Invoke(this, new SampleEventArgs(data.VehicleId, data.RowIndex));
            return true;
        }

        public void EndSession(string vehicleId)
        {
            if (_writers.TryGetValue(vehicleId, out StreamWriter writer))
            {
                try
                {
                    writer.Flush();
                    writer.Close();
                    writer.Dispose();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SERVER] Greska pri zatvaranju: {ex.Message}");
                }
                finally
                {
                    _writers.Remove(vehicleId);
                }
            }

            _lastSample.Remove(vehicleId);
            _rejectPaths.Remove(vehicleId);

            Console.WriteLine($"[SERVER] Prenos zavrsen - vozilo: {vehicleId}");
            OnTransferCompleted?.Invoke(this, new TransferEventArgs(vehicleId));
        }

        private bool Validate(ChargingData data, out string reason)
        {
            reason = null;
            if (string.IsNullOrWhiteSpace(data.Timestamp))
            { reason = "Timestamp nedostaje"; return false; }
            if (data.VoltageAvg <= 0)
            { reason = $"Napon mora biti > 0 (dobijeno: {data.VoltageAvg})"; return false; }
            if (data.FrequencyAvg <= 0)
            { reason = $"Frekvencija mora biti > 0 (dobijeno: {data.FrequencyAvg})"; return false; }
            return true;
        }

        private void RunAnalytics(ChargingData data)
        {
            if (_lastSample.TryGetValue(data.VehicleId, out ChargingData prev))
            {
                double deltaV = Math.Abs(data.VoltageAvg - prev.VoltageAvg);
                if (deltaV > _voltageThreshold)
                {
                    string msg = $"VoltageSpike: dV={deltaV:F2}V (pre={prev.VoltageAvg:F2}, posle={data.VoltageAvg:F2})";
                    Console.WriteLine($"[WARNING] {msg}");
                    OnWarningRaised?.Invoke(this, new WarningEventArgs(
                        data.VehicleId, data.RowIndex, msg,
                        WarningType.VoltageSpike, prev.VoltageAvg, data.VoltageAvg));
                }

                double deltaI = Math.Abs(data.CurrentAvg - prev.CurrentAvg);
                if (deltaI > _currentThreshold)
                {
                    string msg = $"CurrentSpike: dI={deltaI:F2}A (pre={prev.CurrentAvg:F2}, posle={data.CurrentAvg:F2})";
                    Console.WriteLine($"[WARNING] {msg}");
                    OnWarningRaised?.Invoke(this, new WarningEventArgs(
                        data.VehicleId, data.RowIndex, msg,
                        WarningType.CurrentSpike, prev.CurrentAvg, data.CurrentAvg));
                }
            }

            if (data.ApparentPowerAvg > 0)
            {
                double pf = data.RealPowerAvg / data.ApparentPowerAvg;
                if (pf < _powerFactorThreshold)
                {
                    string msg = $"PowerFactorWarning: PF={pf:F4} (prag={_powerFactorThreshold})";
                    Console.WriteLine($"[WARNING] {msg}");
                    OnWarningRaised?.Invoke(this, new WarningEventArgs(
                        data.VehicleId, data.RowIndex, msg,
                        WarningType.PowerFactorWarning, _powerFactorThreshold, pf));
                }
            }
        }

        private void AppendReject(ChargingData data, string reason)
        {
            if (!_rejectPaths.TryGetValue(data.VehicleId, out string path)) return;
            try
            {
                using (StreamWriter sw = new StreamWriter(path, append: true))
                {
                    sw.WriteLine($"RowIndex={data.RowIndex},Timestamp={data.Timestamp},Razlog={reason}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SERVER] Greska pri pisanju rejects.csv: {ex.Message}");
            }
        }
    }
}