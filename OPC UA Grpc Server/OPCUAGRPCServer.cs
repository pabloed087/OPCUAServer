using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Grpc.Core;
using Opc.Ua.Client;
using Opc.Ua;
using System.Configuration;
using Opc.Ua.Configuration;
using static System.Collections.Specialized.BitVector32;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OPC_UA_Grpc_Server
{
  public class OPCUAGRPCServer
  {


    Dictionary<string, ReferenceDescription> NamesAndReferenceDescriptions = new Dictionary<string, ReferenceDescription>(); 
    //This is a list of all refrence descriptions of all variable nodes so we can look each rd for reading and writing in the node

    private Session UASession = null;  //This is our actual conection to the OPC ua server 

   public  OPCUAGRPCServer()
    {
      #region Creating OPC UA client
       
      //This is our in hard codded values for settings in the client when connecting to the client 
      try
      {
        string OPCUAIpAddress = ConfigurationManager.AppSettings["OPCUAIpAddress"];
        EndpointDescription endpointDescription = CoreClientUtils.SelectEndpoint(OPCUAIpAddress, false);
        EndpointConfiguration endpointConfiguration = new EndpointConfiguration() { OperationTimeout = 120000, ChannelLifetime = 300000, MaxArrayLength = 65535, MaxBufferSize = 65535, MaxByteStringLength = 4194304, MaxMessageSize = 4194304, MaxStringLength = 1048576, SecurityTokenLifetime = 360000, UseBinaryEncoding = true };
        ConfiguredEndpoint configuredEndpoint = new ConfiguredEndpoint(null, endpointDescription, endpointConfiguration);// { EndpointUrl = new Uri(@"opc.tcp://192.168.0.1:4840") };
        ApplicationInstance applicationInstance = new ApplicationInstance() { ApplicationName = "VisionTestBench", ApplicationType = ApplicationType.Client };
        ApplicationConfiguration appConfig = new ApplicationConfiguration() { ApplicationName = "VisionTestBench", ApplicationType = ApplicationType.Client, ApplicationUri = @"urn:pablof-5540:OPCFoundation:SampleClient" };
        appConfig.ClientConfiguration = new ClientConfiguration() { DefaultSessionTimeout = 1000 * 30, MinSubscriptionLifetime = 0 };
        appConfig.SecurityConfiguration = new SecurityConfiguration() { AutoAcceptUntrustedCertificates = true, UseValidatedCertificates = false };
        ServiceMessageContext serviceMessageContext = appConfig.CreateMessageContext(true);

        ITransportChannel transportChannel = SessionChannel.Create(appConfig, configuredEndpoint.Description, configuredEndpoint.Configuration, null, null, serviceMessageContext);
        SessionClient sessionClient = new SessionClient(transportChannel);

        UASession = Session.Create(appConfig, transportChannel, configuredEndpoint, null);
        UASession.Open("VisionOPCUAClient" + Guid.NewGuid().ToString(), uint.MaxValue / 100, new UserIdentity(), null, false);
        UASession.KeepAlive -= UASession_KeepAlive;
        UASession.KeepAlive += UASession_KeepAlive;
        GrabAllNodes(UASession);
         IReadOnlyDictionary<NodeId, DataDictionary> keyValuePairs =  UASession.DataTypeSystem;
         ITypeTable typeTable =  UASession.TypeTree;
         
      }
      catch (Exception exxx)
      {
        Console.WriteLine(exxx.Message);
      }


      #endregion

    
   
    }

    private void UASession_KeepAlive(ISession session, KeepAliveEventArgs e)
    {
      if(ServiceResult.IsBad(e.Status)) 
      {
        session.Reconnect();
      }
    }



   

    private VariableData OPCUA_ReadVariabl(ReadVariableData readVariableData)
    {
      DataValue dataValue = null;
      VariableData variableData = null;
      try
      {
        ReferenceDescription rd = GetReferenceDescription(readVariableData.VariableName, NamesAndReferenceDescriptions.Select(x => x.Key).ToList());
        dataValue = UASession.ReadValue((NodeId)rd.NodeId);
        if(dataValue == null)
        {
          variableData.VariableValue = "null";
          variableData.VariableType = "unknown";
        }
        else
        {
          variableData.VariableValue = dataValue.Value.ToString();
          variableData.VariableType = dataValue.Value.GetType().Name;
        }
        
      }
      catch (Exception ex)
      {
        string fullExMessage = ExceptionToString(ex);
        Console.WriteLine(fullExMessage);
        UASession.Reconnect();
        GrabAllNodes(UASession);
      }

      return variableData;
  }

    private ReferenceDescription GetReferenceDescription(string VariableName, List<string> KeyNames)
    {
      try
      {
        if (VariableName.Contains("."))
        {
          int dotIndex = VariableName.IndexOf(".");
          string singleChunk = VariableName.Substring(0,dotIndex);
          List<string> MatchingKeys = KeyNames.Where( s => s.Contains(singleChunk) ).ToList();
          if (MatchingKeys.Count == 1)
          {
            return NamesAndReferenceDescriptions[MatchingKeys[0]];
          }
          else
          {
            string RestOfString = VariableName.Substring(dotIndex + 1);
            return GetReferenceDescription(RestOfString, MatchingKeys);
          }
        }
        else
        {
          string FinalKey = KeyNames.Where(s => s.Contains(VariableName)).First();
          ReferenceDescription referenceDescription = NamesAndReferenceDescriptions[FinalKey];
          return referenceDescription;
        }
      }
      catch (Exception ex)
      {
        string fullExMessage = ExceptionToString(ex);
        Console.WriteLine(fullExMessage);
      }
      return null;
    }

    private bool OPCUA_WriteValue(WriteValueData writeValue)
    {
      Exception exception = null;
      bool success = false;
      Node node = null;
      try
      {
        ReferenceDescription rd = GetReferenceDescription(writeValue.VariableName, NamesAndReferenceDescriptions.Select(x => x.Key).ToList()); //should be changed to look at the OPCUA server every time not dict created at the begning. Both read and wrtie
        node = UASession.ReadNode((NodeId)rd.NodeId);
        Type node_Type = node.GetType();
        Type nodeDataType = null;
        object nodeWritevalue  = null;
        if (node_Type == typeof(VariableNode))
        {
          VariableNode variableNode = (VariableNode)node;
          nodeDataType = GetNodeDataType(variableNode.DataType.Identifier.ToString());
          nodeWritevalue = ConvertValueToNodeDataType(writeValue.Value, nodeDataType);
        }

        if (nodeDataType == null)
        {
          // @"NodeId was not able to be mapped to a Data Type";
          success = false;
        }
        else
        {
          if (nodeWritevalue == null)
          {
            // @"Value pass in was not able to be converted to the Node Data Type  Data Type :  " + nodeDataType.Name + " | Value : " + e.Item1.Value;
            success = false;
          }
          else
          { 
            StatusCodeCollection statusCodes = new StatusCodeCollection();
            DiagnosticInfoCollection diagnosticInfos = new DiagnosticInfoCollection();
            WriteValueCollection writeValues = new WriteValueCollection();
            writeValues.Add(new WriteValue() { NodeId = (NodeId)rd.NodeId, Value = new DataValue() { Value = nodeWritevalue }, AttributeId = Attributes.Value });
            UASession.Write(null, writeValues, out statusCodes, out diagnosticInfos);
            // @"Success";
            success = true;
          }
        }
      }
      catch (Exception ex)
      {
        string fullExMessage = ExceptionToString(ex);
        Console.WriteLine(fullExMessage);
        UASession.Reconnect();
        GrabAllNodes(UASession);
        Console.WriteLine((fullExMessage));
      }
      return success;
    }

    #region Usefull Mehtods 
    public string ExceptionToString(Exception ex)
    {
      Dictionary<string, object> exDictionary = GrabAllException(ex);
      JsonSerializerOptions JsonOptions = new JsonSerializerOptions { MaxDepth = 10, ReferenceHandler = ReferenceHandler.IgnoreCycles, WriteIndented = true };
      string text_object = JsonSerializer.Serialize(exDictionary, JsonOptions);
      return text_object;
    }

    public static Dictionary<string, object> GrabAllException(Exception e)
    {
      Dictionary<string, object> Errors = new Dictionary<string, object>
      {
        { "Type", e.GetType().ToString() },
        { "Message", e.Message }
      };
      if (e.Source != null)
      {
        Errors.Add("Source ", e.Source.ToString());
      }
      if (e.InnerException != null)
      {
        Errors.Add("InnerExption", GrabAllException(e.InnerException));
      }

      return Errors;
    }



    public object ConvertValueToNodeDataType(string Value, Type NodeDataType)
    {
      object NodeValue = null;
      try
      {
        #region Special handeling For bool
        if (NodeDataType == typeof(bool))
        {
          double boolAsDouble;
          if (Value.ToLower() == "false" )
          {
            NodeValue = false;
          }
          else if (Value.ToLower() == "true")
          {
            NodeValue = true;
          }
          else
          {
            if (double.TryParse(Value, out boolAsDouble))

            {
              if (boolAsDouble > 0)
              {
                NodeValue = true;
              }
              else
              {
                NodeValue = false;
              }
            }
            else
            {
              NodeValue = false;
            }
          }
        }
        #endregion
        else
        {
          NodeValue = Convert.ChangeType(Value, NodeDataType);
        }
      }
      catch 
      {
      }
      return NodeValue;
    }


    /// <summary>
    /// This is takes the string of the Node Id and tries to grab the correct Data Type
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public Type GetNodeDataType(string value)
    {
      Type dataType = null;
      switch (value)
      {
        case @"1": //Bool
          dataType = typeof(bool);
          break;
        case @"6": //Int32 | DInt
          dataType = typeof(Int32);
          break;
        case @"10": //Float | Real
          dataType = typeof(Single);
          break;
        case @"12": //String
          dataType = typeof(String);
          break;
        case @"3003": //Dint
          dataType = typeof(Int32);
          break;
        //case @"3012": //char
        //  dataType = typeof(char);
        //  break;
        case @"3014": //String
          dataType = typeof(string);
          break;
          dataType = null;
        default:
          break;
      }
      return dataType;
    }

    #endregion

    #region Getting All Variable Nodes
    //this can be cahnge to grab a node by passing node name / plc db name and the sesion to find and write values dynamically 
    public void GrabAllNodes(Session session)
    {
      ILocalNode root = UASession.NodeCache.Find(Objects.RootFolder) as ILocalNode;
      NodeId nodeId = ReferenceTypeIds.HierarchicalReferences;
      ReferenceDescription reference = new ReferenceDescription();

      reference.ReferenceTypeId = nodeId;
      reference.IsForward = true;
      reference.NodeId = root.NodeId;
      reference.NodeClass = root.NodeClass;
      reference.BrowseName = root.BrowseName;
      reference.DisplayName = root.DisplayName;
      reference.TypeDefinition = root.TypeDefinitionId;


      BrowseDescription nodeToBrowse = new BrowseDescription();

      nodeToBrowse.NodeId = (NodeId)reference.NodeId;
      nodeToBrowse.BrowseDirection = BrowseDirection.Forward;
      nodeToBrowse.ReferenceTypeId = nodeId;
      nodeToBrowse.IncludeSubtypes = true;
      nodeToBrowse.NodeClassMask = 0;
      nodeToBrowse.ResultMask = (uint)(int)BrowseResultMask.All;

      BrowseDescriptionCollection nodesToBrowse = new BrowseDescriptionCollection();
      nodesToBrowse.Add(nodeToBrowse);

      ViewDescription view = new ViewDescription();
      view.ViewId = null;
      view.Timestamp = DateTime.MinValue;
      view.ViewVersion = 0;


      BrowseResultCollection results = null;
      DiagnosticInfoCollection diagnosticInfos = null;

      ResponseHeader responseHeader = session.Browse(
           null,
           view,
           1000,
           nodesToBrowse,
           out results,
           out diagnosticInfos);

      BrowseResult browseRes = results[0];
      ReferenceDescription rd = browseRes.References[0];

      Browser browser = new Browser(session);

      GrabAllChildrenNodes(browser, rd, rd.BrowseName.Name);
    }

    private void GrabAllChildrenNodes(Browser session, ReferenceDescription rd, string Name)
    {

      ReferenceDescriptionCollection referenceDescriptions = new ReferenceDescriptionCollection(session.Browse((NodeId)rd.NodeId)); //browseResult.References); ///Make new copy

      foreach (ReferenceDescription resd in referenceDescriptions)
      {
        if (resd.NodeId != null)
        {
          string nodeIDname = resd.NodeId.Identifier.ToString();
          if (!nodeIDname.Contains("["))
          {
            string name = Name + "." + resd.BrowseName.Name;
            if (resd.NodeClass == NodeClass.Variable)
            {

              if (!NamesAndReferenceDescriptions.ContainsKey(name))
              {
                NamesAndReferenceDescriptions.Add(name, resd);
              }
            }
            if (resd.NodeClass == NodeClass.Object || resd.NodeClass == NodeClass.Variable)
            {
              GrabAllChildrenNodes(session, resd, name);
            }
          }
        }
      }

      return;
    }

    #endregion

  }

}
