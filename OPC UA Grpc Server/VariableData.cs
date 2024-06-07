using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OPC_UA_Grpc_Server
{
  public class VariableData
  {
    public string VariableValue { get; set; }

    public string VariableType { get; set; }

    public string VariableName {get; set; } 

  }
}
