using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using Debug = UnityEngine.Debug;
using System.Text;
using System.IO;
using System.Collections;
using System.ComponentModel;
public class ServerController : MonoBehaviour
{
    //Set Network
    Thread[] threadArr = new Thread[3];//Server : 3
    Dictionary<int, string> clientNumDict = new Dictionary<int, string>();
    Dictionary<int, (TcpClient, NetworkStream)> mainDict = new Dictionary<int, (TcpClient, NetworkStream)>();
    Dictionary<int, (TcpClient, NetworkStream)> chatDict = new Dictionary<int, (TcpClient, NetworkStream)>();
    Dictionary<int, (TcpClient, NetworkStream)> transDict = new Dictionary<int, (TcpClient, NetworkStream)>();

    //Read and Write Data
    List<byte> transByteList = new List<byte>();
    byte[] transBytes = new byte[28];
    byte[] chatBytes = new byte[256];
    int clientNum = 0;
    private void Awake()
    {
        string ipAddress = UIController.ip;
        int _port = UIController.port;

        ServerThreadStart(ipAddress, _port, threadArr[0], "Main");
        ServerThreadStart(ipAddress, _port + 1, threadArr[1], "Transform");
        ServerThreadStart(ipAddress, _port + 2, threadArr[2], "Chat");

        StartCoroutine(SendCoroutine());
    }
    IEnumerator SendCoroutine()
    {
        while (true)
        {
            yield return null;
            SendTransData();
            SendChatData();
        }
    }
    private void ServerThreadStart(string ip, int port, Thread thread, string serverName)
    {
        thread = new Thread(new ThreadStart(() =>
        {
            TcpListener tcp = null;
            ServerStart(ip, port, tcp, client =>
            {
                if (serverName == "Main")
                {
                    MainServer(client);
                } 
                if (serverName == "Chat")
                    DataServer(client, chatDict);
                if (serverName == "Transform")
                    DataServer(client, transDict);
            });
        }));
        thread.Start();
    }
    private void ServerStart(string ip, int port, TcpListener server, Action<TcpClient> enterClient)
    {
        try
        {
            IPAddress localAddr = IPAddress.Parse(ip);
            server = new TcpListener(localAddr, port);
            server.Start();

            while (true)
            {
                Debug.Log("Waiting for connection...");
                TcpClient client = server.AcceptTcpClient();
                Debug.Log("Connected!");
                enterClient(client);
            }
        }
        catch (SocketException e)
        {
            Debug.LogError("SetupServer: " + e);
            server.Stop();
        }
    }
    private void MainServer(TcpClient client)
    {
        NetworkStream stream = null; int count = 0; byte[] buffer = new byte[1024];
        try
        {
            stream = client.GetStream();
            clientNum++;

            //닉네임 받기
            if ((count = ReadStream(client, stream, buffer)) == 0) return;
            string name = Encoding.UTF8.GetString(buffer, 0, count);

            //클라이언트 넘버 전송
            byte[] nameBytes = Encoding.UTF8.GetBytes($"YourNum:{clientNum}");
            stream.Write(nameBytes, 0, nameBytes.Length);

            //클라이언트가 받은 넘버 다시 받기
            int receiveNum = ReadNumber(stream);
            if (receiveNum == 0)
            {
                FindDisconnectedClient();
                return;
            }
            mainDict[receiveNum] = (client, stream);
            clientNumDict[receiveNum] = name;

            //단체 메시징
            string json = TypeConverter.SerializeClientDict_String(clientNumDict);
            byte[] data = Encoding.UTF8.GetBytes(json);
            SendAll(mainDict, data, data.Length);
        }
        catch (Exception e)
        {
            FindDisconnectedClient();
            Debug.LogError($"MainServer : {e}");
        }
        
    }
    private void DataServer(TcpClient client, Dictionary<int, (TcpClient, NetworkStream)> dict)
    {
        NetworkStream stream = null;
        stream = client.GetStream();
        int clientNum = ReadNumber(stream);
        if (clientNum != 0) 
        {
            dict[clientNum] = (client, stream);
            stream.Write(BitConverter.GetBytes(clientNum), 0, 4);
        }
        else FindDisconnectedClient();
    }
    private int ReadNumber(NetworkStream stream)
    {
        byte[] numBytes = new byte[4];
        int count = stream.Read(numBytes, 0, numBytes.Length);
        if (count != 0) return BitConverter.ToInt32(numBytes);
        else return 0;
    }
    private void FindDisconnectedClient()
    {   //모든 클라이언트 확인하여 이상이 있는 클라이언트 연결 끊기
        foreach(int num in clientNumDict.Keys)
        {
            //정상적으로 연결되었다면 건너뛰기
            if (mainDict.ContainsKey(num) && transDict.ContainsKey(num) && chatDict.ContainsKey(num)) continue;
            else RemoveConnection(num);
        }
    }
    private int ReadStream(TcpClient client, NetworkStream stream, byte[] bytes)
    {
        int length = stream.Read(bytes, 0, bytes.Length);
        if (length == 0)
        {
            client.Close();
            stream.Close();
        }
        return length;
    }
    private void SendAll(Dictionary<int, (TcpClient, NetworkStream)> dict, byte[] data, int length)
    {
        foreach ((TcpClient, NetworkStream) item in dict.Values) item.Item2.Write(data, 0, length);
    }
    private void SendChatData()
    {
        if (chatDict.Count == 0)
            return;
        int count = 0;
        //Read
        foreach (KeyValuePair<int, (TcpClient, NetworkStream)> pair in chatDict)
        {
            try
            {
                if (!pair.Value.Item2.DataAvailable) continue;
                count = pair.Value.Item2.Read(chatBytes, 0, chatBytes.Length);
                break;
            }
            catch (Exception e)
            {
                RemoveConnection(pair.Key);
                Debug.LogError(e);
            }
        }
        //Write
        foreach (KeyValuePair<int, (TcpClient, NetworkStream)> pair in chatDict) pair.Value.Item2.Write(chatBytes, 0, count);
    }
    private void SendTransData()
    {
        if (transDict.Count == 0)
            return;

        int count = 0;
        //Read
        foreach (KeyValuePair<int, (TcpClient, NetworkStream)> pair in transDict)
        {
            try
            {
                count = pair.Value.Item2.Read(transBytes, 0, transBytes.Length);
                if (count == 0)
                {
                    RemoveConnection(pair.Key);
                    continue;
                }
                transByteList.AddRange(transBytes);
            }
            catch (Exception e)
            {
                RemoveConnection(pair.Key);
                Debug.LogError(e);
                break;
            }
        }
        //Write
        foreach (KeyValuePair<int, (TcpClient, NetworkStream)> pair in transDict)
        {
            try { pair.Value.Item2.Write(transByteList.ToArray(), 0, transByteList.Count); }
            catch (Exception e) { RemoveConnection(pair.Key); Debug.LogError(e); }
        }
            
        transByteList.Clear();
    }
    private void RemoveConnection(int clientNum)
    {
        try
        {
            RemoveNetworkDict(mainDict, clientNum);
            RemoveNetworkDict(chatDict, clientNum);
            RemoveNetworkDict(transDict, clientNum);
            clientNumDict.Remove(clientNum);

            //단체 메시징
            string json = TypeConverter.SerializeClientDict_String(clientNumDict);
            byte[] data = Encoding.UTF8.GetBytes(json);
            SendAll(mainDict, data, data.Length);
        }
        catch(Exception e) { Debug.LogError(e);}
    }
    private void RemoveNetworkDict(Dictionary<int, (TcpClient, NetworkStream)> dict, int clientNum)
    {
        if (dict.ContainsKey(clientNum))
        {
            dict[clientNum].Item1.Close();
            dict[clientNum].Item2.Close();
            dict.Remove(clientNum);
        }
    }
    void OnApplicationQuit()
    {
        for (int i = 0; i < threadArr.Length; i++)
        {
            if (threadArr[i] != null)
                threadArr[i].Abort();
        }
        foreach (int num in clientNumDict.Keys)
        {
            RemoveConnection(num);
        } 
    }
}
