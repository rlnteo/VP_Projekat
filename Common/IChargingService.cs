using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ServiceModel;

namespace Common
{
    [ServiceContract]
    public interface IChargingService
    {
        [OperationContract]
        void StartSession(string vehicleId);

        [OperationContract]
        [FaultContract(typeof(ChargingFault))]
        bool PushSample(ChargingData data);
        
        [OperationContract]
        void EndSession(string vehicleId);
    }
}
