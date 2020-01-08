using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;
using Microsoft.Azure.Devices.Provisioning.Client;
using Microsoft.Azure.Devices.Provisioning.Client.Transport;

using Newtonsoft.Json;

namespace pisample
{
  class Program
  {
    static string ScopeID = "{your Scope ID}";
    static string DeviceID = "{your Device ID}";
    static string PrimaryKey = "{your Device Primary Key}";
    static string GlobalDeviceEndpoint = "global.azure-devices-provisioning.net";
    static DeviceClient Client = null;
    static TwinCollection reportedProperties = new TwinCollection();
    static CancellationTokenSource cts;
    static double baseTemperature = 60;
    static double basePressure = 500;
    static double baseHumidity = 50;

    static async Task Main(string[] args)
    {
      Console.WriteLine("== Raspberry Pi Azure IoT Central example ==");

      try
      {

        using (var security = new SecurityProviderSymmetricKey(DeviceID, PrimaryKey, null))
        {
          DeviceRegistrationResult result = await RegisterDeviceAsync(security);
          if (result.Status != ProvisioningRegistrationStatusType.Assigned) {
            Console.WriteLine("Failed to register device");
            return;
          }
          IAuthenticationMethod auth = new DeviceAuthenticationWithRegistrySymmetricKey(result.DeviceId, (security as SecurityProviderSymmetricKey).GetPrimaryKey());
          Client = DeviceClient.Create(result.AssignedHub, auth, TransportType.Mqtt);
        }

        await SendDevicePropertiesAsync();

        Console.Write("Register settings changed handler...");
        await Client.SetDesiredPropertyUpdateCallbackAsync(HandleSettingChanged, null);
        Console.WriteLine("Done");

        cts = new CancellationTokenSource();
        Task task = SendTelemetryAsync(cts.Token);

        remoteMethod(); //added handa

        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();
        cts.Cancel();
        await task;
      }
      catch (Exception ex)
      {
        Console.WriteLine();
        Console.WriteLine(ex.Message);
      }
    }

    public static async Task<DeviceRegistrationResult> RegisterDeviceAsync(SecurityProviderSymmetricKey security)
    {
        Console.WriteLine("Register device...");

        using (var transport = new ProvisioningTransportHandlerMqtt(TransportFallbackType.TcpOnly))
        {
          ProvisioningDeviceClient provClient =
                    ProvisioningDeviceClient.Create(GlobalDeviceEndpoint, ScopeID, security, transport);

          Console.WriteLine($"RegistrationID = {security.GetRegistrationID()}");


          Console.Write("ProvisioningClient RegisterAsync...");
          DeviceRegistrationResult result = await provClient.RegisterAsync();

          Console.WriteLine($"{result.Status}");
          Console.WriteLine($"ProvisioningClient AssignedHub: {result.AssignedHub}; DeviceID: {result.DeviceId}");

          return result;
        }
    }

    public static async Task SendDevicePropertiesAsync()
    {
        Console.WriteLine("Send device properties...");
        Random random = new Random();
        TwinCollection telemetryConfig = new TwinCollection();
        reportedProperties["dieNumber"] = random.Next(1, 6);
        Console.WriteLine(JsonConvert.SerializeObject(reportedProperties));

        await Client.UpdateReportedPropertiesAsync(reportedProperties);
    }

    private static async Task SendTelemetryAsync(CancellationToken token)
    {
      Random rand = new Random();

      while (true)
      {
        double currentTemperature = baseTemperature + rand.NextDouble() * 20;
        double currentPressure = basePressure + rand.NextDouble() * 100;
        double currentHumidity = baseHumidity + rand.NextDouble() * 20;

        var telemetryDataPoint = new
        {
          humidity = currentHumidity,
          pressure = currentPressure,
          temp = currentTemperature
        };
        var messageString = JsonConvert.SerializeObject(telemetryDataPoint);
        var message = new Message(Encoding.ASCII.GetBytes(messageString));

        token.ThrowIfCancellationRequested();
        await Client.SendEventAsync(message);

        Console.WriteLine("{0} > Sending telemetry: {1}", DateTime.Now, messageString);

        await Task.Delay(1000);
      }
    }


    private static async Task HandleSettingChanged(TwinCollection desiredProperties, object userContext)
    {
      Console.WriteLine("Received settings change...");
      Console.WriteLine(JsonConvert.SerializeObject(desiredProperties));

      string setting = "fanSpeed";
      if (desiredProperties.Contains(setting))
      {
        // Act on setting change, then
        BuildAcknowledgement(desiredProperties, setting);
      }
      setting = "setVoltage";
      if (desiredProperties.Contains(setting))
      {
        // Act on setting change, then
        BuildAcknowledgement(desiredProperties, setting);
      }
      setting = "setCurrent";
      if (desiredProperties.Contains(setting))
      {
        // Act on setting change, then
        BuildAcknowledgement(desiredProperties, setting);
      }
      setting = "activateIR";
      if (desiredProperties.Contains(setting))
      {
        // Act on setting change, then
        BuildAcknowledgement(desiredProperties, setting);
      }
      Console.WriteLine("Send settings changed acknowledgement...");
      await Client.UpdateReportedPropertiesAsync(reportedProperties);
    }

    private static void BuildAcknowledgement(TwinCollection desiredProperties, string setting)
    {
      reportedProperties[setting] = new
      {
        value = desiredProperties[setting]["value"],
        status = "completed",
        desiredVersion = desiredProperties["$version"],
        message = "Processed"
      };
    }

    // (追加) IoT Central からのダイレクトメソッド使用時に呼び出されるメソッド
    private static async void remoteMethod()
    {
        await Client.SetMethodHandlerAsync(nameof(WriteToConsole), WriteToConsole, null).ConfigureAwait(false);
    }

    private static Task<MethodResponse> WriteToConsole(MethodRequest methodRequest, object userContext)

    {
        Console.WriteLine($"\t *** {nameof(WriteToConsole)} was called.");
        Console.WriteLine();
        Console.WriteLine("\t{0}", methodRequest.DataAsJson);
        Console.WriteLine();

        return Task.FromResult(new MethodResponse(new byte[0], 200));

    }

  }
}