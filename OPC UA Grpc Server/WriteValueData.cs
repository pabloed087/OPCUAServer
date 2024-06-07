using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OPC_UA_Grpc_Server
{
  public  class WriteValueData
  {
    public string VariableName { get; set; }

    public string Value { get; set; }
  }
}
