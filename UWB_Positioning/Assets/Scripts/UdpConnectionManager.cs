using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using UnityEngine;
using UnityEngine.UI;

// Simple UDP server class that opens a socket, uses a background thread to place 
// all incoming messages onto a queue, and then processes them in Update() 
// https://forum.unity.com/threads/simple-udp-implementation-send-read-via-mono-c.15900/

public class UdpConnectionManager : MonoBehaviour
{
    [Tooltip("Port on which to listen for incoming UDP messages")]
    [SerializeField] private int receivePort = 50000;
    // UI References to visualise incoming UDP data for debugging
    [SerializeField] private Transform logWindow;
    [SerializeField] private ScrollRect m_ServerLoggerScrollRect = null;
    [SerializeField] private RectTransform m_ServerLoggerRectTransform = null;
    [SerializeField] private Text m_ServerLoggerText = null;
    // Reference to the UdpConnection class
    private UdpConnection connection;

    protected void ServerLog(string msg) {
        m_ServerLoggerText.text += "\n" + msg;
        // Ensure ScrollBar shows last message
        LayoutRebuilder.ForceRebuildLayoutImmediate(m_ServerLoggerRectTransform);
        m_ServerLoggerScrollRect.verticalNormalizedPosition = 0f;
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

    void Start() {
        connection = new UdpConnection();
        connection.StartConnection(receivePort);
    }

    void Update() {
        foreach (string message in connection.getMessages()) {
            // Display the message received
            ServerLog("Received message " + System.Uri.EscapeUriString(message));
            // Call the positioning script to update the tag position based on range data
            GetComponent<Positioning>().DecodeJSONUpdate(message);
        }
    }

    // Be sure to close the connection and stop the background thread when done 
    void OnDestroy() {
        connection.Stop();
    }
}