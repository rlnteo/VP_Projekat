using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Service
{
    public class TransferEventArgs : EventArgs
    {
        public string VehicleId { get; set; }
        public DateTime Time { get; }

        public TransferEventArgs(string vehicleId)
        {
            VehicleId = vehicleId;
            Time = DateTime.Now;
        }
    }

    public class SampleEventArgs : EventArgs
    {
        public string VehicleId { get; }
        public int RowIndex { get; }
        public DateTime Time { get; }

        public SampleEventArgs(string vehicleId, int rowIndex)
        {
            VehicleId = vehicleId;
            RowIndex = rowIndex;
            Time = DateTime.Now;
        }
    }

    public class WarningEventArgs : EventArgs
    {
       public string VehicleId { get; }
        public int RowIndex { get; }
        public string Message { get; }
        public WarningType Type { get; }
        public double ValueBefore { get; }
        public double ValueAfter { get; }

        public WarningEventArgs (string vehicleId, int rowIndex, string message, WarningType type, double before = 0, double after = 0)
        {
            VehicleId = vehicleId;
            RowIndex = rowIndex;
            Message = message;
            Type = type;
            ValueBefore = before;
            ValueAfter = after;
        }
    }

    public enum WarningType
    {
        VoltageSpike, 
        CurrentSpike,
        PowerFactorWarning, 
        ValidationFailed
    }
}
