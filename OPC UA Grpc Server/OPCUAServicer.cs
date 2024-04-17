using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Grpc.Core;
using Opc.Ua;
using OPC_UA_GRPC_Proto_Files; 

namespace OPC_UA_Grpc_Server
{
  public class OPCUAServicer: OPC_UA_GRPC_Proto_Files.OPCUAGRPC.OPCUAGRPCBase
  {
    #region Events
    public event EventHandler<Tuple<WriteValueData, DiagInfo>> WriteValueEvent;

    public event EventHandler<Tuple<ReadVariableData, VariableData>> ReadVariableEvent;

    public event EventHandler<DiagInfo> HealthCheckEvent;
    #endregion



#region Invoking Events
    private void Invoke_WriteValueEvent(Tuple< WriteValueData, DiagInfo> value)
    {
      if (WriteValueEvent != null)
      {
        EventHandler<Tuple<WriteValueData,DiagInfo>> handler = WriteValueEvent;
        handler?.Invoke(this, value);
      }
    }


    private void Invoke_ReadValueEvent(Tuple<ReadVariableData, VariableData> value)
    {
      if(ReadVariableEvent != null)
      {
        EventHandler<Tuple<ReadVariableData, VariableData>> handler = ReadVariableEvent;
        handler?.Invoke(this, value);
      }
    }

    private void Invoke_HealthCheckEvent(DiagInfo value)
    {
      if(HealthCheckEvent != null)
      {
        EventHandler<DiagInfo> eventHandler = HealthCheckEvent;
        eventHandler?.Invoke(this, value);  
      }
    }
    #endregion

    public override Task<DiagInfo> WriteValue(WriteValueData request, ServerCallContext context)
    {
      Tuple<WriteValueData, DiagInfo> tuple = new Tuple<WriteValueData, DiagInfo>(request, new DiagInfo());
      Invoke_WriteValueEvent(tuple);
      return Task.FromResult(tuple.Item2);
    }

    //PabloF need a way to check if the OPC UA client is still connected and if not a way to reconnect
    public override Task<DiagInfo> HealthCheck(DiagInfo request, ServerCallContext context)
    {
      DiagInfo diagnosticInfo = new DiagInfo();// { Info = "Server Online" };
      Invoke_HealthCheckEvent(diagnosticInfo);
      return Task.FromResult(diagnosticInfo);
    }


    public override Task<VariableData> ReadValue(ReadVariableData request, ServerCallContext context)
    {
      VariableData variableData = new VariableData(); 
      Tuple<ReadVariableData, VariableData> ReadValueTuple = new Tuple<ReadVariableData, VariableData>(request, variableData); 
      Invoke_ReadValueEvent(ReadValueTuple);
      return Task.FromResult(variableData);
    }


  }
}
