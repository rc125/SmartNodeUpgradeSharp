using CsvHelper;
using Renci.SshNet;
using System;
using System.IO;
using System.Threading;

namespace SmartNodeUpgradeSharp
{
    class Program
    {
    private static string LATEST_VERSION = "1020300";

    static void Main(string[] args)
    {
      TextReader reader = new StreamReader(@"nodes.csv");
      var csv = new CsvReader(reader);
      var records = csv.GetRecords<SmartNodeServer>();

      foreach (SmartNodeServer node in records)
      {
        Thread thread = new Thread(() => CheckOrUpdateNode(node));
        thread.Start();
      }

      Console.ReadKey();
    }

    public static void CheckOrUpdateNode(SmartNodeServer node)
    {

      using (SshClient client = new SshClient(node.hostname, node.user, node.pass))
      {
        try
        {
          client.Connect();

          bool upgrade = IsUpgradeRequired(client, node);

          if (upgrade)
          {
            logMessage(node, "Upgrade requested");

            string cmd = "apt update";
            cmd += " && apt update";
            cmd += " && smartcash-cli stop";
            cmd += " && sleep 5";
            cmd += " && apt install smartcashd -y";

            string response = runCommand(cmd, client);

            //if (!String.IsNullOrEmpty(response))
            //{
            //  logMessage(node, "Upgrade completed");
            //}
          }

          client.Disconnect();

        }
        catch (Exception ex)
        {
          logMessage(node, String.Format("ERROR: {0}", ex.Message));
        }
      }
    }

    private static bool IsUpgradeRequired(SshClient client, SmartNodeServer node)
    {
      string version = "UNKNOWN";

      string getInfoResult = runCommand("smartcash-cli getinfo", client);

      if (String.IsNullOrEmpty(getInfoResult))
      {
        logMessage(node, "Not able to get current version");

        //maybe smartcasd crashed, try to run a reindex
        runCommand("smartcashd -reindex > /dev/null 2>&1", client);
      }
      else
      {
        version = returnNodeVersion(getInfoResult);

        if (version.Contains(LATEST_VERSION))
        {
          string smartNodeSatusResult = runCommand("smartcash-cli smartnode status", client);

          string nodeStausMessage = returnNodeStatusMessage(smartNodeSatusResult, version);
          logMessage(node, nodeStausMessage);

          return false;
        }
        else
        {
          return true;
        }

      }


      return false;
    }

    private static string returnNodeStatusMessage(string smartNodeSatusResult, string version)
    {

      string[] lines = smartNodeSatusResult.Split(new[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);

      foreach (string item in lines)
      {
        if (item.ToUpper().Contains("STATUS"))
        {
          string status = item.Replace(" \"status\": ", "").Replace("\"", "").Trim();
          return String.Format("Version: {0} Status: {1}", version, status);
        }
      }

      return "ERROR: Could not get status on smartnode";
    }

    private static string returnNodeVersion(string getInfoResult)
    {
      string[] lines = getInfoResult.Split(new[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);
      string version = lines[1].Replace(" \"version\": ", "").Replace(",", "").Trim();
      return version;
    }

    private static void logMessage(SmartNodeServer node, string msg)
    {
      string consoleMsg = String.Format("[{0}]\t{1}", node.alias, msg);
      Console.WriteLine(consoleMsg);
    }

    private static string runCommand(string command, SshClient client)
    {
      SshCommand sshCommand = client.RunCommand(command);
      return sshCommand.Result.Trim();
    }

  }

}