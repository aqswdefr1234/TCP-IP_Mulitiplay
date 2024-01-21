using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;
using UnityEngine.EventSystems;
public class UIController : MonoBehaviour
{
    [Header("Chat Controll")]
    [SerializeField] private GameObject chatView;
    [SerializeField] private Transform chatPrefab;
    private TMP_InputField nameField;
    private TMP_InputField chatField;
    private ScrollRect scrollRect;
    private Transform content;

    [Header("Select Server / Client")] 
    [SerializeField] private GameObject selectPanel;
    [SerializeField] private GameObject server;
    [SerializeField] private GameObject client;
    [SerializeField] private GameObject myPlayer;
    private TMP_InputField ipField;
    private TMP_InputField portField;
    private TMP_InputField nickField;

    [Header("Notification Text")]
    [SerializeField] TMP_Text notificationText;

    //Current State
    private bool isStart = false;
    private bool isChatFocus = false;
    //Static
    public static string notification = "";
    public static string ip = "";
    public static int port = 0;
    public static string nick = "";

    void Start()
    {
        chatField = chatView.transform.GetChild(2).GetComponent<TMP_InputField>();
        scrollRect = chatView.GetComponent<ScrollRect>();
        content = chatView.transform.GetChild(0).GetChild(0);

        ipField = selectPanel.transform.GetChild(2).GetComponent<TMP_InputField>();
        portField = selectPanel.transform.GetChild(3).GetComponent<TMP_InputField>();
        nickField = selectPanel.transform.GetChild(4).GetComponent<TMP_InputField>();

        StartCoroutine(Detection());
    }

    void Update()
    {
        PressKey();
    }
    private void PressKey()
    {
        if (Input.GetKeyDown(KeyCode.Return))
        {
            //ä��â Ȱ��ȭ �Ǿ� �ְ� ��ǲ�ʵ忡 ä���� �Էµ� ����
            if (isStart == true && isChatFocus == true && chatField.text != "" )
            {
                Debug.Log("Send Message");
                Client_Manager.Instance.SendMessageToServer(chatField.text);
                ChangeFocus(null, false);
            }
            //ä��â Ȱ��ȭ �Ǿ� �ְ� ��ǲ�ʵ忡 ä��X
            else if (isStart == true && isChatFocus == true && chatField.text == "")
            {
                Debug.Log("Close Focus");
                ChangeFocus(null, false);
            }
            //ä��â ��Ȱ��ȭ �Ǿ� �ִ� ����
            else if (isStart == true && chatField.isFocused == false && isChatFocus == false)
            {
                Debug.Log("On Focus");
                ChangeFocus(null, true);
            }
        }
    }
    private void ChangeFocus(GameObject chatObject, bool isFocus)
    {
        chatField.text = "";
        isChatFocus = isFocus;
        myPlayer.GetComponent<PlayerController>().canMove = !isFocus;
        EventSystem.current.SetSelectedGameObject(chatObject, null);

        if(isFocus == true)
            chatField.ActivateInputField();
        else
            chatField.DeactivateInputField();
    }
    public void SelectServer()
    {
        if (IsFilledInput() == false)
            return;

        server.SetActive(true);
        selectPanel.SetActive(false);
    }
    
    public void SelectClient()
    {
        if (IsFilledInput() == false)
            return;
        client.SetActive(true);
        selectPanel.SetActive(false);
        myPlayer.SetActive(true);
        isStart = true;
    }
    private bool IsFilledInput()
    {
        if (ipField.text != "" && portField.text != "" && nickField.text != "")
        {
            ip = ipField.text;
            port = Convert.ToInt32(portField.text);
            nick = nickField.text;
            return true;
        }
            
        else
        {
            notification = "IP or Port are empty";
            return false;
        } 
    }
    IEnumerator Detection()
    {
        WaitForSeconds delay = new WaitForSeconds(0.5f);
        while (true)
        {
            yield return delay;

            Notify();//�˸� ����
            scrollRect.verticalNormalizedPosition = 0;//��ũ�ѹ� �Ʒ��� ����
        }
    }
    private void Notify()
    {
        if(notification != "")
        {
            notificationText.text = notification;
            notification = "";
            Invoke("ClearNoti", 5f);
        }
    }
    private void ClearNoti()
    {
        notificationText.text = "";
    }
}
