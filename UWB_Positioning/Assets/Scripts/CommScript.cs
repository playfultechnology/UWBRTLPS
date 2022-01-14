using UnityEngine;
using System.Collections;
using System.Net.Sockets;
using System;
using System.Text;
using System.Net;
using System.Threading;

// https://gamedev.stackexchange.com/questions/124227/unity-and-thread-for-reading-udp

public class CommScript : MonoBehaviour {
    UdpClient client;
    IPEndPoint endPoint;
    public int port = 7777;
    public string hostName = "localhost";
    public GameObject car;
    public int stepNum;
    Thread listener;
    Queue pQueue = Queue.Synchronized(new Queue()); // holds the packet queue

    void Start() {

        IPAddress ip = IPAddress.Parse(hostName);
        endPoint = new IPEndPoint(ip, port);
        client = new UdpClient(endPoint);
        Debug.Log("Listening for Data...");
        //Debug.Log(Dns.GetHostAddresses(hostName)[0]);
        listener = new Thread(new ThreadStart(translater));
        listener.IsBackground = true;
        listener.Start();
    }
    void Update() {
        lock (pQueue.SyncRoot) {
            if (pQueue.Count > 0) {
                //Packet p = (Packet)pQueue.Dequeue();
                UnityEngine.Object p = (UnityEngine.Object)pQueue.Dequeue();
                // stepNum = p.step;
                //car.GetComponent<CarMover>().moveMe(p);
                Debug.Log("Yay");
            }
        }
    }

    void OnApplicationQuit() {
        client.Close();
    }

    void translater() {
        Byte[] data = new byte[0];
        while (true) {
            try {
                data = client.Receive(ref endPoint);
                Debug.Log("T!");
            }
            catch (Exception err) {
               // Tools.LogDebugThread("Comm.translater", "recieve data error " + err, -1, -1);
                client.Close();
                return;
            }
            string json = Encoding.ASCII.GetString(data);
            pQueue.Enqueue((json));
        }
    }
}