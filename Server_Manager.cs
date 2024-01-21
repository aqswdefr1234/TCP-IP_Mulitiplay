using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class Server_Manager : MonoBehaviour
{
    Dictionary<int, (TcpClient, NetworkStream)> userDict = new Dictionary<int, (TcpClient, NetworkStream)>();
    Dictionary<int, (TcpClient, NetworkStream)> transDict = new Dictionary<int, (TcpClient, NetworkStream)>();
    List<(TcpClient, NetworkStream)> chatList = new List<(TcpClient, NetworkStream)>();
    Dictionary<int, string> clientNumDict = new Dictionary<int, string>();
    
    private TcpListener mainServer = null;
    private TcpListener transServer = null;
    private TcpListener chatServer = null;
    private Thread userThread;
    private Thread transThread;
    private Thread chatThread;

    private string ipAddress = "";
    private int _port = 0;
    private void Awake()
    {
        ipAddress = UIController.ip;
        _port = UIController.port;

        userThread = new Thread(new ThreadStart(() => MainServer(ipAddress, _port, mainServer)));
        transThread = new Thread(new ThreadStart(() => TransServer(ipAddress, _port + 1, transServer)));
        chatThread = new Thread(new ThreadStart(() => ChatServer(ipAddress, _port + 2, chatServer)));
        userThread.Start();
        transThread.Start();
        chatThread.Start();
        StartCoroutine(PosDataCoroutine());
        StartCoroutine(ChatCoroutine());
    }
    IEnumerator ChatCoroutine()
    {
        List<byte[]> bytesList = new List<byte[]>();
        List<int> countList = new List<int>();
        while (true)
        {
            yield return null;
            if (chatList.Count > 0)
            {
                ReadWriteChat(bytesList, countList);
            }
        }
    }
    IEnumerator PosDataCoroutine()
    {
        List<byte> byteList = new List<byte>();
        byte[] bytes = new byte[28];
        while (true)
        {
            yield return null;
            if (transDict.Count > 0)
            {
                ReadWriteTrans(byteList, bytes);
                byteList.Clear();
            }
        }
    }
    private void ReadWriteChat(List<byte[]> bytesList, List<int> countList)
    {
        int count = 0;
        try
        {
            foreach ((TcpClient, NetworkStream) item in chatList)
            {
                if (item.Item2 == null)
                    Debug.Log("비어있음.");
                if (!item.Item2.DataAvailable)
                    continue;
                byte[] chatBytes = new byte[256];
                count = item.Item2.Read(chatBytes, 0, chatBytes.Length);
                Debug.Log("읽기");
                bytesList.Add(chatBytes);
                countList.Add(count);
            }
            if (bytesList.Count == 0)
                return;
            foreach((TcpClient, NetworkStream) item in chatList)
            {
                for(int i = 0; i < bytesList.Count; i++)
                {
                    Debug.Log("Write");
                    item.Item2.Write(bytesList[i], 0, countList[i]);
                }
            }
            countList.Clear();
            bytesList.Clear();
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            foreach ((TcpClient, NetworkStream) item in chatList)
            {
                item.Item1.Close();
                item.Item2.Close();
                chatList.Remove(item);
            }
        }
    }
    private void ReadWriteTrans(List<byte> byteList, byte[] bytes)
    {
        int count = 0;
        int number = -1;
        try
        {
            foreach (KeyValuePair<int, (TcpClient, NetworkStream)> pair in transDict)
            {
                number = pair.Key;
                count = pair.Value.Item2.Read(bytes, 0, bytes.Length);
                if (count == 0)
                {
                    RemoveDict(pair.Key);
                    continue;
                }
                byteList.AddRange(bytes);
            }
            SendAll(transDict, byteList.ToArray(), byteList.Count);
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            RemoveDict(number);
        }
    }
    private void ServerStart(string ip, int port, TcpListener server, Action<TcpClient> enterClient)
    {
        try
        {
            IPAddress localAddr = IPAddress.Parse(ipAddress);
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
        catch (SocketException ex)
        {
            Debug.LogError("SetupServer: " + ex);
            server.Stop();
        }
    }
    private void MainServer(string ip, int port, TcpListener server)
    {
        int clientNum = 0;
        ServerStart(ip, port, server, client => {
            clientNum++;
            EnterClient(client, clientNum);
        });
    }
    private void TransServer(string ip, int port, TcpListener server)
    {
        ServerStart(ip, port, server, client => {
            EnterClient_Trans(client);
        });
    }
    private void ChatServer(string ip, int port, TcpListener server)
    {
        ServerStart(ip, port, server, client => {
            EnterClient_Chat(client);
        });
    }
    private void EnterClient(TcpClient client, int clientNum)
    {
        NetworkStream stream = null; int count = 0; byte[] buffer = new byte[1024];
        try
        {
            stream = client.GetStream();

            //닉네임 받기
            if ((count = ReadStream(client, stream, buffer)) == 0)
                return;
            string name = Encoding.UTF8.GetString(buffer, 0, count);//반드시 buffer.Length가 아닌 count만큼 읽어야함.buffer.Length만큼 읽으면 빈공간때문에 에러생김
            clientNumDict[clientNum] = name;
            userDict[clientNum] = (client, stream);

            //클라이언트 넘버 전송
            byte[] nameBytes = Encoding.UTF8.GetBytes($"YourNum:{clientNum}");
            stream.Write(nameBytes, 0, nameBytes.Length);

            //클라이언트가 넘버 잘 받았다면 대답 읽기
            if (ReadStream(client, stream, buffer) == 0)
            {
                clientNumDict.Remove(clientNum);
                userDict.Remove(clientNum);
                return;
            }

            //단체 메시징
            string json = TypeConverter.SerializeClientDict_String(clientNumDict);
            byte[] data = Encoding.UTF8.GetBytes(json);
            SendAll(userDict, data, data.Length);
        }
        catch (SocketException e)
        {
            Debug.LogError("EnterClient: " + e);
            client.Close();
            stream.Close();
            userDict.Remove(clientNum);
        }
    }
    
    private void EnterClient_Trans(TcpClient client)
    {
        NetworkStream stream = null; int count; byte[] numBytes = new byte[4]; int clientNum = -1;
        try
        {
            stream = client.GetStream();
            if ((count = ReadStream(client, stream, numBytes)) == 0)
                return;
            clientNum = (int)BitConverter.ToInt32(numBytes);
            transDict[clientNum] = (client, stream);
        }
        catch (SocketException e)
        {
            Debug.LogError(e);
        }
    }
    private void EnterClient_Chat(TcpClient client)
    {
        NetworkStream stream = null;
        try
        {
            stream = client.GetStream();
            chatList.Add((client, stream));
        }
        catch (SocketException e)
        {
            Debug.LogError("EnterClient: " + e);
            client.Close();
            stream.Close();
            chatList.Remove((client, stream));
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
        foreach ((TcpClient, NetworkStream) item in dict.Values)
            item.Item2.Write(data, 0, length);
    }
    private void RemoveDict(int clientNum)
    {
        if (userDict.ContainsKey(clientNum))
        {
            userDict[clientNum].Item1.Close();//TcpClient
            userDict[clientNum].Item2.Close();//NetworkStream
            userDict.Remove(clientNum);
        }
        if (transDict.ContainsKey(clientNum))
        {
            transDict[clientNum].Item1.Close();//TcpClient
            transDict[clientNum].Item2.Close();//NetworkStream
            transDict.Remove(clientNum);
        }
        if (clientNumDict.ContainsKey(clientNum))
        {
            clientNumDict.Remove(clientNum);
        }
        //나머지 클라이언트는 포지션값이 해당 벡터 값보다 작아지면 대상 플레이어의 오브젝트를 파괴한다.
        byte[] RmUserBytes = new byte[28];
        Buffer.BlockCopy(BitConverter.GetBytes(clientNum), 0, RmUserBytes, 0, 4);
        TypeConverter.ConverToBytes(new Vector3(-10000f, -10000f, -10000f), RmUserBytes, 4);
        TypeConverter.ConverToBytes(new Vector3(0f, 0f, 0f), RmUserBytes, 16);
        SendAll(transDict, RmUserBytes, 28);
    }
}