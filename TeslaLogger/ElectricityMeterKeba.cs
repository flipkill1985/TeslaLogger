using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Exceptionless;
using Newtonsoft.Json;

namespace TeslaLogger
{
    [SuppressMessage("Design", "CA1031:Keine allgemeinen Ausnahmetypen abfangen", Justification = "<Pending>")]
    internal class ElectricityMeterKeba : ElectricityMeterBase
    {
        private readonly UdpClient listener;
        private readonly UdpClient sender;

        public ElectricityMeterKeba(IPAddress address, int port)
        {
            sender = new UdpClient();
            sender.Connect(address, port);

            listener = new UdpClient(port);
            listener.Client.ReceiveTimeout = 2000;
        }

        public ElectricityMeterKeba(string host, string parameter)
            : this(Dns.GetHostAddresses(host).First(), int.Parse(parameter))
        {
        }

        public void Send(string cmd)
        {
            var bytes = Encoding.ASCII.GetBytes(cmd);
            sender.Send(bytes, bytes.Length);
        }

        public string Receive()
        {
            var remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
            byte[] bytes = listener.Receive(ref remoteEndPoint);
            return Encoding.ASCII.GetString(bytes);
        }

        public override bool? IsCharging()
        {
            try
            {
                Send("report 2");
                dynamic reportJson;
                do
                {
                    string reply = Receive();
                    reportJson = JsonConvert.DeserializeObject(reply);
                } while (reportJson.ID != 2);

                return (int) reportJson.State == 3;
            }
            catch (Exception ex)
            {
                ex.ToExceptionless().FirstCarUserID().Submit();
                Logfile.Log(ex.ToString());
            }

            return null;
        }

        public override double? GetUtilityMeterReading_kWh()
        {
            return null;
        }

        public override double? GetVehicleMeterReading_kWh()
        {
            try
            {
                Send("report 3");
                dynamic reportJson;
                do
                {
                    string reply = Receive();
                    reportJson = JsonConvert.DeserializeObject(reply);
                } while (reportJson.ID != 3);

                return (double) reportJson["E total"] / 10000.0;
            }
            catch (Exception ex)
            {
                ex.ToExceptionless().FirstCarUserID().Submit();
                Logfile.Log(ex.ToString());
            }

            return null;
        }

        public override string GetVersion()
        {
            try
            {
                Send("report 1");
                dynamic reportJson;
                do
                {
                    string reply = Receive();
                    reportJson = JsonConvert.DeserializeObject(reply);
                } while (reportJson.ID != 1);

                return reportJson.Product + " / fw:" + reportJson.Firmware;
            }
            catch (Exception ex)
            {
                ex.ToExceptionless().FirstCarUserID().Submit();
                Logfile.Log(ex.ToString());
            }

            return null;
        }
    }
}