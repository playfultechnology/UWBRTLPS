using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

// http://www.java2s.com/Code/CSharp/Network/SimpleUdpServer.htm

public class SimpleUdpServer : MonoBehaviour
{
    int recv;
    byte[] data = new byte[1024];
    Socket newsock;
    EndPoint Remote;
    // Start is called before the first frame update
    void Start()
    {
        
        IPEndPoint ipep = new IPEndPoint(IPAddress.Any, 9050);

        newsock = new Socket(AddressFamily.InterNetwork,
                        SocketType.Dgram, ProtocolType.Udp);

        newsock.Bind(ipep);
        Console.WriteLine("Waiting for a client...");

        IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
        Remote = (EndPoint)(sender);

        recv = newsock.ReceiveFrom(data, ref Remote);

        Console.WriteLine("Message received from {0}:", Remote.ToString());
        Console.WriteLine(Encoding.ASCII.GetString(data, 0, recv));

        string welcome = "Welcome to my test server";
        data = Encoding.ASCII.GetBytes(welcome);
        newsock.SendTo(data, data.Length, SocketFlags.None, Remote);
    }

    // Update is called once per frame
    void Update()
    {
        data = new byte[1024];
        recv = newsock.ReceiveFrom(data, ref Remote);

        Console.WriteLine(Encoding.ASCII.GetString(data, 0, recv));
        newsock.SendTo(data, recv, SocketFlags.None, Remote);
    }
}
