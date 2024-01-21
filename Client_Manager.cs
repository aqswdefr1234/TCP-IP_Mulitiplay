using UnityEngine;
using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Debug = UnityEngine.Debug;
using TMPro;
using System.Collections.Generic;
using System.Collections;
using System.ComponentModel;
using System.Net;
using System.IO;
public class Client_Manager : MonoBehaviour
{
    public static Dictionary<int, (Vector3, Vector3)> playersDict = new Dictionary<int, (Vector3, Vector3)>();
    public static Dictionary<int, string> namesDict = new Dictionary<int, string>();
    public static Dictionary<int, Transform> transDict = new Dictionary<int, Transform>();
    private List<int> assignedlist = new List<int>();//새로운 유저 확인
    private List<Thread> threadList = new List<Thread>();
    private List<TcpClient> tcpList = new List<TcpClient>();
    private List<NetworkStream> netList = new List<NetworkStream>();
    //Instance
    public static Client_Manager instance = null;
    public static Client_Manager Instance
    {
        get
        {
            if (instance == null)
                return null;
            return instance;
        }
    }
    //Set Server
    private string nickName = "";
    private string ipAddress = ""; // Set this to your server's IP address.
    private int _port = 0;             // Set this to your server's port.
    private bool startTrans = false;
    //Client Data
    [SerializeField] private Transform myTrans;
    [SerializeField] private Transform otherPrefab;
    private int myClientNum = -1;
    private Vector3 myPos = new Vector3();
    private Vector3 myRot = new Vector3();
    //Chat
    [SerializeField] private Transform chatPrefab;
    [SerializeField] private Transform chatViewContent;
    private NetworkStream chatStream;
    private string newMassage = "";
    void Awake()
    {
        if (instance == null)
            instance = this;

        ipAddress = UIController.ip;
        _port = UIController.port;
        nickName = UIController.nick;
        myTrans = myTrans.GetChild(0);
    }
    void Start()
    {
        TcpClient chatClient = null; TcpClient transClient = null; TcpClient usersClient = null;
        Thread usersThread = new Thread(new ThreadStart(() => UserDataServer(usersClient, ipAddress, _port)));
        Thread transThread = new Thread(new ThreadStart(() => TransServer(transClient, ipAddress, _port + 1)));
        Thread chatThread = new Thread(new ThreadStart(() => ChatServer(chatClient, ipAddress, _port + 2)));
        TreadStart(usersThread);
        TreadStart(transThread);
        TreadStart(chatThread);
        
        StartCoroutine(ReadNamesDict());
        StartCoroutine(ChangeTransform());
        StartCoroutine(ReceiveMesaage());
    }
    void Update()
    {
        myPos = myTrans.position;
        myRot = myTrans.eulerAngles;
    }
    public void SendMessageToServer(string msg)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(msg);
        chatStream.Write(bytes, 0, bytes.Length);
    }
    IEnumerator ReceiveMesaage()
    {
        while (true)
        {
            yield return null;
            if (newMassage == "")
                continue;
            Instantiate(chatPrefab, chatViewContent).GetComponent<TMP_Text>().text = newMassage;
            newMassage = "";
        }
    }
    IEnumerator ChangeTransform()
    {
        while (true)
        {
            yield return null;
            if (myClientNum == -1)
                continue;
            try
            {
                foreach (int num in assignedlist)
                {
                    //나의 클라이언트 데이터라면
                    if (num == myClientNum)
                        continue;
                    transDict[num].position = playersDict[num].Item1;
                    transDict[num].eulerAngles = playersDict[num].Item2;
                    if (playersDict[num].Item1.z < -9000f)
                        RemoveOtherUser(num);
                }
            }
            catch (Exception e)
            {
                Debug.LogError("ChangeTransform : " + e.ToString());
            }
        }
    }
    IEnumerator ReadNamesDict()
    {
        while (true)
        {
            yield return null;

            if (myClientNum == -1)
                continue;
            //새로운 유저가 들어오지 않았다면
            if (namesDict.Count == assignedlist.Count)
                continue;

            //새로운 유저가 있을 때
            foreach (int num in namesDict.Keys)
            {
                if (assignedlist.Contains(num))
                    continue;

                //새로운 유저가 접속시
                if (num != myClientNum)
                {
                    Transform other = Instantiate(otherPrefab);
                    other.name = namesDict[num];
                    transDict[num] = other.GetChild(0);
                }
                else
                    transDict[num] = null;
                assignedlist.Add(num);
            }
        }
    }
    private void ChatServer(TcpClient tcp, string ip, int port)
    {
        NetworkStream stream = null;
        try
        {
            int count = 0; byte[] readBytes = new byte[1024]; string message = "";
            tcp = new TcpClient(ip, port);
            stream = tcp.GetStream();
            tcpList.Add(tcp);
            netList.Add(stream);
            chatStream = stream;
            //모든 유저 데이터 받기
            while ((count = stream.Read(readBytes, 0, readBytes.Length)) != 0)
            {
                message = Encoding.UTF8.GetString(readBytes, 0, count);
                newMassage = message;
                Debug.Log(message);
            }
        }
        catch (SocketException e)
        {
            Debug.LogError("ChatServer: " + e);
            OnApplicationQuit();
        }
    }
    private void UserDataServer(TcpClient tcp, string ip, int port)
    {
        NetworkStream stream = null;
        try
        {
            int count = 0; byte[] readBytes = new byte[1024]; 
            tcp = new TcpClient(ip, port);
            stream = tcp.GetStream();
            tcpList.Add(tcp);
            netList.Add(stream);
            //이름보내기
            byte[] writeBytes = Encoding.UTF8.GetBytes(nickName);
            stream.Write(writeBytes, 0, writeBytes.Length);

            //클라이언트 넘버 받고 보내기
            ReadWriteNum(tcp, stream, readBytes);

            //모든 유저 데이터 받기
            while ((count = stream.Read(readBytes, 0, readBytes.Length)) != 0)
            {
                string dictData = Encoding.UTF8.GetString(readBytes, 0, count);
                Debug.Log($"{dictData} : {dictData.Length}");
                namesDict = TypeConverter.DeserializeClientDict_String(dictData);
            }
        }
        catch (SocketException e)
        {
            Debug.LogError("UserDataServer: " + e);
            OnApplicationQuit();
        }
    }
    private void TransServer(TcpClient tcp, string ip, int port)
    {
        NetworkStream stream = null;
        while (true)
        {
            if(startTrans && myClientNum != -1)
                break;
        }
        try
        {
            tcp = new TcpClient(ip, port);
            stream = tcp.GetStream();
            tcpList.Add(tcp);
            netList.Add(stream);
            //넘버 트랜스폼 서버에 보내기

            byte[] sendNum = BitConverter.GetBytes(myClientNum);
            stream.Write(sendNum, 0, 4);
            
            Thread sendThread = new Thread(new ThreadStart(() => SendTrans(stream)));
            TreadStart(sendThread);
        }
        catch (SocketException e)
        {
            Debug.LogError("ServerStart: " + e);
            OnApplicationQuit();
        }
    }
    private void ReadWriteNum(TcpClient tcp, NetworkStream stream, byte[] readBytes)
    {
        int count = 0;
        //넘버 받기
        if ((count = ReadStream(tcp, stream, readBytes)) == 0)
            return;
        string numData = Encoding.UTF8.GetString(readBytes, 0, count);
        int num = Convert.ToInt32(numData.Substring(8, numData.Length - 8));//ex , YourNum:9

        //번호 받았다고 서버에 연락하기
        byte[] numBytes = Encoding.UTF8.GetBytes($"SuccessReceiveNumber:{num}");
        stream.Write(numBytes, 0, numBytes.Length);
        Array.Clear(readBytes, 0, readBytes.Length);
        myClientNum = num;
        startTrans = true;
    }
    private void ReceiveTrans(NetworkStream stream)
    {
        int count;
        byte[] bytes = new byte[1024];
        if((count = stream.Read(bytes, 0, bytes.Length)) != 0)
        {
            if (count % 28 != 0)
                return;

            int per = count / 28; //count / 28 => 1 당 1명의 유저 transform 데이터
            for (int i = 0; i < per; i++)
            {
                int playersNum = (int)BitConverter.ToInt32(bytes, 28 * i);
                Vector3 pos = TypeConverter.ConvertToVec(bytes, 28 * i + 4);
                Vector3 rot = TypeConverter.ConvertToVec(bytes, 28 * i + 16);
                playersDict[playersNum] = (pos, rot);
            }
        }
    }
    private void SendTrans(NetworkStream stream)
    {
        byte[] bytes = new byte[28];
        //Client 넘버 삽입
        Buffer.BlockCopy(BitConverter.GetBytes(myClientNum), 0, bytes, 0, 4);
        while (true)
        {
            try
            {
                TypeConverter.ConverToBytes(myPos, bytes, 4);
                TypeConverter.ConverToBytes(myRot, bytes, 16);

                stream.Write(bytes, 0, 28);
                ReceiveTrans(stream);
            }
            catch (SocketException socketException)
            {
                Debug.Log("Socket exception: " + socketException);
                OnApplicationQuit();
                break;
            }
        }
    }
    private int ReadStream(TcpClient client, NetworkStream stream, byte[] bytes)
    {
        int length = stream.Read(bytes, 0, bytes.Length);
        if (length == 0)
        {
            OnApplicationQuit();
        }
        return length;
    }
    
    private void TreadStart(Thread thread)
    {
        thread.Start();
        threadList.Add(thread);
    }
    private void RemoveOtherUser(int number)
    {
        Destroy(transDict[number].parent.gameObject);
        playersDict.Remove(number);
        namesDict.Remove(number);
        transDict.Remove(number);
        assignedlist.Remove(number);
    }
    private void LeaveRoom()
    {
        foreach(TcpClient tcp in tcpList)
        {
            if (tcp != null)
                tcp.Close();
        }
        foreach (NetworkStream stream in netList)
        {
            if(stream != null)
                stream.Close();
        }
        tcpList.Clear();
        netList.Clear();
    }
    private void AbortThread()
    {
        foreach (Thread thread in threadList)
        {
            if(thread != null)
                thread.Abort();
        }
        threadList.Clear();
    }
    void OnApplicationQuit()
    {
        LeaveRoom();
        AbortThread();
    }
}