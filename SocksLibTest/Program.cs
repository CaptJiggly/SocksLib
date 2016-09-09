using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SocksLib;
using System.Net.Sockets;
using System.Net;
using System.Threading;
using System.Diagnostics;

namespace SocksLibTest
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.Write("Select Type (s = server, c = client): ");
            var type = Console.ReadLine().ToLower();

            switch (type)
            {
                case "c":
                    client();
                    break;
                case "s":
                    server();
                    break;
            }
            Process.GetCurrentProcess().WaitForExit();
        }

        static void client()
        {
            Console.Title = "Client";
            TcpSocket socket = new TcpSocket();
            socket.AsyncConnectionResult += (s, e) =>
            {
                if (e.Connected)
                {
                    Console.WriteLine("Client Connected!");
                    beginChat(false, socket);

                    while (true)
                    {
                        string msg = Console.ReadLine();

                        if (socket.Connected)
                        {
                            if (msg.ToLower() != "disconnect")
                            {
                                byte[] msgPayload = Encoding.ASCII.GetBytes(msg);

                                socket.SendAsync(msgPayload);
                            }
                            else
                            {
                                socket.Disconnect();
                                break;
                            }
                        }
                        else
                        {
                            break;
                        }
                    }
                }
                else
                {
                    Console.WriteLine("Connection could not be made... Retrying in 2 seconds...");
                    client();
                }
            };
            socket.ConnectAsync(IPAddress.Loopback, 1000);
        }

        static void server()
        {
            Console.Title = "Server";
            Console.WriteLine("Waiting for client...");
            TcpSocketListener l = new TcpSocketListener(1000);
            TcpSocket client = null;
            l.Accepted += (s, e) =>
            {
                Console.WriteLine("Client accepted!");
                client = e.AcceptedSocket;
                beginChat(true, client);
                l.Stop();
            };
            l.Start();

            while (true)
            {
                string msg = Console.ReadLine();

                if (client.Connected)
                {
                    if (msg.ToLower() != "disconnect")
                    {
                        byte[] msgPayload = Encoding.ASCII.GetBytes(msg);

                        client.SendAsync(msgPayload);
                    }
                    else
                    {
                        client.Disconnect();
                        break;
                    }
                }
                else
                {
                    break;
                }
            }
            server();
        }

        private static void beginChat(bool isServer, TcpSocket client)
        {
            Console.WriteLine("Chat ready!");
            string name = isServer ? "Client" : "Server";
            client.DataReceived += (ds, de) =>
            {
                Console.WriteLine("{0}: {1}", name, Encoding.ASCII.GetString(de.Payload));
            };

            client.Disconnected += (ds, de) =>
            {
                Console.WriteLine("{0} has disconnected!", name);
            };
        }
    }
}
