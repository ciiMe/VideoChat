using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace CameraServer
{
    class Program
    {
        private const int bufferSize = 32 * 1024;
        private const int ServerPort = 7899;

        static void Main(string[] args)
        {
            Log.Append("System starting...");

            IPEndPoint ipEndPoint = new IPEndPoint(IPAddress.Any, ServerPort);
            Socket server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            server.Bind(ipEndPoint);
            server.Listen(10);
            Console.WriteLine(" 等待客户端的连接 ");

            while (true)
            {
                processAccepteConnection(server.Accept());
            }

            server.Close();
        }

        private static void processAccepteConnection(Socket client)
        {
            FileStream fs = null;
            try
            {
                IPEndPoint clientIp = (IPEndPoint)client.RemoteEndPoint;
                Console.WriteLine("Accept connection :" + clientIp.Address);

                fs = File.OpenWrite("Data" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".jpg");
                ReadConnectionData(client, fs);

                Console.WriteLine("Data Handle complete :" + clientIp.Address);
            }
            finally
            {
                client.Close();
                client.Dispose();

                if (fs != null)
                {
                    fs.Close();
                }
            }
        }

        private static void ReadConnectionData(Socket client, FileStream stream)
        {
            IPEndPoint ep = (IPEndPoint)client.RemoteEndPoint;
            byte[] buffer = new byte[bufferSize];
            int msgSize;
            try
            {
                while (true)
                {
                    msgSize = client.Receive(buffer);
                    if (msgSize == 0)
                    {
                        Log.Append("No data from " + ep.Address + ", thread exits.");
                        break;
                    }

                    Log.Append("Get data length:" + msgSize + " from " + ep.Address);
                    stream.Write(buffer, 0, msgSize);
                }
            }
            catch
            {
                Console.WriteLine("出现异常：连接被迫关闭");
            }
        }
    }
}
