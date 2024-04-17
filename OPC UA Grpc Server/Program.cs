using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace OPC_UA_Grpc_Server
{
  internal class Program
  {
    static OPCUAGRPCServer OPCUAGRPCServer;
    static void Main(string[] args)
    {
      OPCUAGRPCServer = new OPCUAGRPCServer();
      ConsoleKeyInfo consoleKeyInfo =  Console.ReadKey();
      if(consoleKeyInfo.Key == ConsoleKey.Escape)
      {
        return;
      }
    }
  }
}
