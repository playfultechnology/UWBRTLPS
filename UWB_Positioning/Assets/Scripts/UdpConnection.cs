using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

// https://forum.unity.com/threads/simple-udp-implementation-send-read-via-mono-c.15900/

public class UdpConnection {
    private UdpClient udpClient;
    private readonly Queue<string> incomingQueue = new Queue<string>();
    Thread receiveThread;
    private bool threadRunning = false;

    public static string GetLocalIPAddress() {
        var host = Dns.GetHostEntry(Dns.GetHostName());
        foreach (var ip in host.AddressList) {
            if (ip.AddressFamily == AddressFamily.InterNetwork) {
                return ip.ToString();
            }
        }
        throw new Exception("No network adapters with an IPv4 address in the system!");
    }

    public void StartConnection(int receivePort) {
        try {
            udpClient = new UdpClient(receivePort);
        }
        catch (Exception e) {
            Debug.Log("Failed to create UDP client" + ": " + e.Message);
            return;
        }
        Debug.Log("Created UDP client on " + GetLocalIPAddress() + ":" + receivePort);

        // Start separate thread to listen for incoming UDP messages
        receiveThread = new Thread(() => ListenForMessages(udpClient));
        receiveThread.IsBackground = true;
        threadRunning = true;
        receiveThread.Start();
    }

    private void ListenForMessages(UdpClient client) {
        // Create a IPEndPoint to record the details of the sender
        IPEndPoint remoteIpEndPoint = new IPEndPoint(IPAddress.Any, 0);

        while (threadRunning) {
            try {
                // Blocks until a message is received, at which point reads receiveBytes
                // and also populates remoteIpEndPoint with details of the sender
                Byte[] receiveBytes = client.Receive(ref remoteIpEndPoint); 
                string returnData = Encoding.UTF8.GetString(receiveBytes);
                // Push the data received onto the message queue
                lock (incomingQueue) {
                    incomingQueue.Enqueue(returnData);
                }
            }
            catch (SocketException e) {
                // 10004 thrown when socket is closed
                if (e.ErrorCode != 10004) Debug.Log("Socket exception receiving data from UDP client: " + e.Message);
            }
            catch (Exception e) {
                Debug.Log("Error receiving data from UDP client: " + e.Message);
            }
            Thread.Sleep(1);
        }
    }

    // Return all the messages that have been placed in the queue
    public string[] getMessages() {
        string[] pendingMessages = new string[0];
        lock (incomingQueue) {
            pendingMessages = new string[incomingQueue.Count];
            int i = 0;
            while (incomingQueue.Count != 0) {
                pendingMessages[i] = incomingQueue.Dequeue();
                i++;
            }
        }
        return pendingMessages;
    }

    // Send a UDP message to specified recipient IP/port
    // (not required - included for completeness) 
    public void Send(string message, string destIP, int destPort) {
        Debug.Log(String.Format("Send msg to ip:{0} port:{1} msg:{2}", destIP, destPort, message));
        IPEndPoint serverEndpoint = new IPEndPoint(IPAddress.Parse(destIP), destPort);
        Byte[] sendBytes = Encoding.UTF8.GetBytes(message);
        udpClient.Send(sendBytes, sendBytes.Length, serverEndpoint);
    }

    // Close the client and tidy up
    public void Stop() {
        threadRunning = false;
        receiveThread.Abort();
        udpClient.Close();
    }
}