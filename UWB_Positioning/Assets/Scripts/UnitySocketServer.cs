using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Net.Sockets;
using System.IO;
using System.Net;
using System.Text;
using System;
using System.Threading;

// https://docs.microsoft.com/en-us/dotnet/framework/network-programming/using-an-asynchronous-server-socket

public class StateObject {
    public Socket workSocket = null;
    public const int BufferSize = 1024;
    public byte[] buffer = new byte[BufferSize];
    public StringBuilder sb = new StringBuilder();
}

public class UnitySocketServer : MonoBehaviour
{

    static ManualResetEvent allDone;

    // Start is called before the first frame update
    void Start()
    {
        StartListening();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private string getIPAddress() {
        IPHostEntry host;
        string localIP = "";
        host = Dns.GetHostEntry(Dns.GetHostName());
        foreach (IPAddress ip in host.AddressList) {
            if (ip.AddressFamily == AddressFamily.InterNetwork) {
                localIP = ip.ToString();
            }

        }
        return localIP;
    }

    public void StartListening() {
        Debug.Log("Ip " + getIPAddress().ToString());
        IPAddress[] ipArray = Dns.GetHostAddresses(getIPAddress());
        IPEndPoint localEndPoint = new IPEndPoint(ipArray[0], 1755);

        Socket listener = new Socket(localEndPoint.Address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

        try {
            listener.Bind(localEndPoint);
            listener.Listen(10);

            while (true) {
                allDone.Reset();

                Console.WriteLine("Waiting for a connection...");
                listener.BeginAccept(new AsyncCallback(UnitySocketServer.AcceptCallback), listener);

                allDone.WaitOne();
            }
        }
        catch (Exception e) {
            Debug.Log(e.ToString());
        }

        Debug.Log("Closing the listener...");
    }

    public static void AcceptCallback(IAsyncResult ar) {
        // Get the socket that handles the client request.  
        Socket listener = (Socket)ar.AsyncState;
        Socket handler = listener.EndAccept(ar);

        // Signal the main thread to continue.  
        allDone.Set();

        // Create the state object.  
        StateObject state = new StateObject();
        state.workSocket = handler;
        handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
            new AsyncCallback(UnitySocketServer.ReadCallback), state);
    }
    public static void ReadCallback(IAsyncResult ar) {
        StateObject state = (StateObject)ar.AsyncState;
        Socket handler = state.workSocket;

        // Read data from the client socket.  
        int read = handler.EndReceive(ar);

        // Data was read from the client socket.  
        if (read > 0) {
            state.sb.Append(Encoding.ASCII.GetString(state.buffer, 0, read));
            handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                new AsyncCallback(ReadCallback), state);
        }
        else {
            if (state.sb.Length > 1) {
                // All the data has been read from the client;  
                // display it on the console.  
                string content = state.sb.ToString();
                Debug.Log($"Read {content.Length} bytes from socket.\n Data : {content}");
            }
            handler.Close();
        }
    }
}
