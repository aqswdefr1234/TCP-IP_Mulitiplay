using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Debug = UnityEngine.Debug;
using System.Threading;
using System.Net.Sockets;
using System.Text;
using System;
using TMPro;
public class ClientController : MonoBehaviour
{
    //Set Network
    string ipAddress; int _port; string nickName;
    private List<Thread> threadList = new List<Thread>();
    private Dictionary<int, (TcpClient, NetworkStream)> connectDict = new Dictionary<int, (TcpClient, NetworkStream)>();
    private Dictionary<int, (Vector3, Vector3)> transDict = new Dictionary<int, (Vector3, Vector3)>();
    private Dictionary<int, string> namesDict = new Dictionary<int, string>();
    private Dictionary<int, Transform> playersDict = new Dictionary<int, Transform>();
    private bool isMainStart = false;
    //My Data
    [SerializeField] private GameObject myPlayer;
    private Transform myBody;
    private Vector3 myPos = new Vector3();
    private Vector3 myRot = new Vector3();
    private int myClientNum = -1;
    
    //Chat
    [SerializeField] private Transform chatPrefab;
    [SerializeField] private Transform chatViewContent;
    private List<string> chatList = new List<string>();
    //Player
    [SerializeField] private Transform playerPrefab;
    [SerializeField] private Transform playGround;
    //Etc
    List<int> lastNums = new List<int>();
    //Instance
    public static ClientController instance = null;
    public static ClientController Instance
    {
        get
        {
            if (instance == null)
                return null;
            return instance;
        }
    }
    void Start()
    {
        if (instance == null)
            instance = this;

        ipAddress = UIController.ip;
        _port = UIController.port;
        nickName = UIController.nick;
        myBody = myPlayer.transform.GetChild(0);

        StartThreading(ipAddress, _port, MainConnection);
        StartThreading(ipAddress, _port + 1, TransConnection);
        StartThreading(ipAddress, _port + 2, ChatConnection);
        
        StartCoroutine(ReceiveMesaage(0.5f));
        StartCoroutine(ChangeUsers(1f));

    }
    private void StartThreading(string ip, int port, Action<TcpClient, NetworkStream> serverConnection)//(TcpClient tcp, string ip, int port, Action<TcpClient, NetworkStream> serverConnection)
    {
        Thread thread = new Thread(new ThreadStart(() => ServerConnection(ip, port, serverConnection)));
        thread.Start();
        threadList.Add(thread);
    }
    private void Update()
    {
        myPos = myBody.position;
        myRot = myBody.eulerAngles;
        ChangeTransform();
    }
    public void SendChat(string msg)
    {
        byte[] bytes = Encoding.UTF8.GetBytes($"{nickName} : {msg}");
        connectDict[_port + 2].Item2.Write(bytes, 0, bytes.Length);
    }
    private void ChangeTransform()
    {
        if (myClientNum == -1)
            return;
        try
        {
            foreach (int num in lastNums)
            {
                //나의 클라이언트 데이터라면
                if (num == myClientNum) continue;
                playersDict[num].position = transDict[num].Item1;
                playersDict[num].eulerAngles = transDict[num].Item2;
            }
        }
        catch (Exception e)
        {
            Debug.LogError("ChangeTransform : " + e.ToString());
        }
    }
    IEnumerator ReceiveMesaage(float delaySecond)
    {
        WaitForSeconds delay = new WaitForSeconds(delaySecond);
        while (true)
        {
            yield return null;
            if (isMainStart && myClientNum != -1) break;

        }
        while (true)
        {
            yield return delay;
            if (chatList.Count == 0) continue;
            foreach (string chat in chatList)
                Instantiate(chatPrefab, chatViewContent).GetComponent<TMP_Text>().text = chat;
            chatList.Clear();
        }
    }
    IEnumerator ChangeUsers(float delaySecond)
    {
        WaitForSeconds delay = new WaitForSeconds(delaySecond);
        while (true)
        {
            yield return null;
            if (isMainStart && myClientNum != -1) break;
        }
        while (true)
        {
            CompareUsers(namesDict, lastNums);
            yield return delay;
        }
    }
    private void CompareUsers(Dictionary<int, string> dict, List<int> list)
    {
        //새로운 유저 있는지
        try
        {
            foreach (int num in dict.Keys)
            {
                if (list.Contains(num)) continue;
                if (myClientNum == num) continue;
                Transform newPlayer = Instantiate(playerPrefab, playGround);
                newPlayer.name = num.ToString();
                playersDict[num] = newPlayer.GetChild(0);
                list.Add(num);
            }
            //나간 유저 있는지
            foreach (int ls in list)
            {
                if (dict.ContainsKey(ls)) continue;
                foreach (Transform player in playGround)
                {
                    if (player.name != ls.ToString()) continue;
                    Destroy(player.gameObject);
                    playersDict.Remove(ls);
                    list.Remove(ls);
                }
            }
        }
        catch (InvalidOperationException e) { Debug.LogError(e); }
    }
    private void ServerConnection(string ip, int port, Action<TcpClient, NetworkStream> serverConnection)
    {
        TcpClient tcp = null;
        NetworkStream stream = null;
        tcp = new TcpClient(ip, port);
        stream = tcp.GetStream();

        while (!(port - _port == connectDict.Count)) Debug.Log("Wait");
        connectDict[port] = (tcp, stream);
        serverConnection(tcp, stream);
    }
    private void MainConnection(TcpClient tcp, NetworkStream stream)
    {
        try
        {
            int count = 0;
            byte[] readBytes = new byte[1024];

            //이름보내기
            byte[] writeBytes = Encoding.UTF8.GetBytes(nickName);
            stream.Write(writeBytes, 0, writeBytes.Length);

            //넘버 받기
            if ((count = ReadStream(tcp, stream, readBytes)) == 0) return;
            string numData = Encoding.UTF8.GetString(readBytes, 0, count);
            int num = Convert.ToInt32(numData.Substring(8, numData.Length - 8));//ex , YourNum:9

            //번호 받고 받은 번호를 서버로 다시 전송
            byte[] numBytes = BitConverter.GetBytes(num);
            stream.Write(numBytes, 0, numBytes.Length);
            myClientNum = num; isMainStart = true;

            //모든 유저 데이터 받기
            while ((count = stream.Read(readBytes, 0, readBytes.Length)) != 0)
            {
                string dictData = Encoding.UTF8.GetString(readBytes, 0, count);
                namesDict = TypeConverter.DeserializeClientDict_String(dictData);
            }
            tcp.Close();
            stream.Close();
        }
        catch (SocketException e)
        {
            tcp.Close();
            stream.Close();
            Debug.LogError($"MainConnection : {e}");
        }
        
    }
    private void TransConnection(TcpClient tcp, NetworkStream stream)
    {
        byte[] readBytes = new byte[1024]; byte[] sendTransBytes = new byte[28];
        try
        {
            while (true) if (myClientNum != -1) break;
            SendMyNumber(stream);
            ReceiveResponse(stream);
            while (true)
            {
                WriteTrans(stream, sendTransBytes);
                ReadTrans(stream, readBytes);
            }
        }
        catch (SocketException e)
        {
            tcp.Close();
            stream.Close();
            Debug.LogError($"TransConnection : {e}");
        }
    }
    private void ChatConnection(TcpClient tcp, NetworkStream stream)
    {
        int count = 0;
        byte[] readBytes = new byte[1024];
        try
        {
            while (true) if (myClientNum != -1) break;
            SendMyNumber(stream);
            ReceiveResponse(stream);
            while ((count = stream.Read(readBytes, 0, readBytes.Length)) != 0)
            {
                chatList.Add(Encoding.UTF8.GetString(readBytes, 0, count));
            }
            tcp.Close();
            stream.Close();
        }
        catch (SocketException e)
        {
            tcp.Close();
            stream.Close();
            Debug.LogError($"ChatConnection : {e}");
        }
    }
    private void SendMyNumber(NetworkStream stream)
    {
        byte[] numBytes = BitConverter.GetBytes(myClientNum);
        stream.Write(numBytes, 0, numBytes.Length);
    }
    private void ReceiveResponse(NetworkStream stream)
    {
        byte[] numBytes = new byte[4];
        int count = stream.Read(numBytes, 0, numBytes.Length);
        if (count == 0) OnApplicationQuit();
    }
    private void WriteTrans(NetworkStream stream, byte[] writeBytes)
    {
        Buffer.BlockCopy(BitConverter.GetBytes(myClientNum), 0, writeBytes, 0, 4);
        TypeConverter.ConverToBytes(myPos, writeBytes, 4);
        TypeConverter.ConverToBytes(myRot, writeBytes, 16);
        stream.Write(writeBytes, 0, writeBytes.Length);
    }
    private void ReadTrans(NetworkStream stream, byte[] readBytes)
    {
        int count = 0;
        if((count = stream.Read(readBytes, 0, readBytes.Length)) != 0)
        {
            if (count % 28 != 0)
                return;

            int per = count / 28; //count / 28 => 1 당 1명의 유저 transform 데이터
            for (int i = 0; i < per; i++)
            {
                int playersNum = (int)BitConverter.ToInt32(readBytes, 28 * i);
                Vector3 pos = TypeConverter.ConvertToVec(readBytes, 28 * i + 4);
                Vector3 rot = TypeConverter.ConvertToVec(readBytes, 28 * i + 16);
                transDict[playersNum] = (pos, rot);
            }
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
    void OnApplicationQuit()
    {
        foreach(KeyValuePair<int, (TcpClient, NetworkStream)> pair in connectDict)
        {
            if (pair.Value.Item1 != null) { pair.Value.Item1.Close();}
            if (pair.Value.Item2 != null) { pair.Value.Item2.Close();}
        }
        connectDict.Clear();
        foreach (Thread thread in threadList) thread.Abort();
    }
}
