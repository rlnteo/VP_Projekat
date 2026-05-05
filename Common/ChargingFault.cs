using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Serialization;

namespace Common
{
    [DataContract]
    public class ChargingFault
    {
        [DataMember]
        public string Reason { get; set; }

        [DataMember]
        public int RowIndex { get; set; }
    }
}
