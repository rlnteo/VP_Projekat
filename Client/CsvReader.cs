using Common;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.Remoting.Metadata.W3cXsd2001;

namespace Client
{
    public class CsvReader : IDisposable
    {
        private FileStream _fileStream;
        private StreamReader _reader;
        private bool _disposed = false;

        public CsvReader(string filePath)
        {
            _fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            _reader = new StreamReader(_fileStream);
        }

        public IEnumerable<(ChargingData data, bool isValid)> ReadRows(string vehicleId, List<string> errorLog)
        {
            string header = _reader.ReadLine();
            if(header == null)
            {
                yield break;
            }

            int rowIndex = 0;

            while(!_reader.EndOfStream)
            {
                string line = _reader.ReadLine();
                if(string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                rowIndex++;

                if(TryParseLine(line, rowIndex, vehicleId, out ChargingData data, out string error))
                {
                    yield return (data, true);

                }
                else
                {
                    errorLog.Add($"Red {rowIndex}: {error} | Sadrzaj: {line}");
                    yield return (null, false);
                }
            }
        }
        private bool TryParseLine(string line, int rowIndex, string vehicleId,
                                  out ChargingData data, out string error)
        {
            data = null;
            error = null;

            try
            {
                string[] p = line.Split(',');

                if (p.Length < 19)
                {
                    error = $"Premalo kolona: {p.Length}";
                    return false;
                }

                data = new ChargingData
                {
                    Timestamp = p[0].Trim(),
                    VoltageMin = Parse(p[1]),
                    VoltageAvg = Parse(p[2]),
                    VoltageMax = Parse(p[3]),
                    CurrentMin = Parse(p[4]),
                    CurrentAvg = Parse(p[5]),
                    CurrentMax = Parse(p[6]),
                    RealPowerMin = Parse(p[7]),
                    RealPowerAvg = Parse(p[8]),
                    RealPowerMax = Parse(p[9]),
                    ReactivePowerMin = Parse(p[10]),
                    ReactivePowerAvg = Parse(p[11]),
                    ReactivePowerMax = Parse(p[12]),
                    ApparentPowerMin = Parse(p[13]),
                    ApparentPowerAvg = Parse(p[14]),
                    ApparentPowerMax = Parse(p[15]),
                    FrequencyMin = Parse(p[16]),
                    FrequencyAvg = Parse(p[17]),
                    FrequencyMax = Parse(p[18]),
                    RowIndex = rowIndex,
                    VehicleId = vehicleId
                };

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }
        private static double Parse(string s) => double.Parse(s.Trim(), CultureInfo.InvariantCulture);

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                try 
                { 
                    _reader?.Close(); 
                } 
                catch 
                { 
                }
                try 
                { 
                    _reader?.Dispose(); 
                } 
                catch 
                { 
                }
                try 
                { 
                    _fileStream?.Close(); 
                } 
                catch 
                { 
                }
                try 
                { 
                    _fileStream?.Dispose(); 
                } 
                catch 
                { 
                }
            }

            _disposed = true;
        }

        ~CsvReader() 
        { 
            Dispose(false); 
        }
    }
}
