using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OtherPlayerController : MonoBehaviour
{
    public int clientNum = -1;
    private Transform body;
    void Start()
    {
        body = transform.GetChild(0);
    }

    // Update is called once per frame
    void Update()
    {
        if (clientNum == -1)
            return;
        body.position = Client_Manager.playersDict[clientNum].Item1;
        body.eulerAngles = Client_Manager.playersDict[clientNum].Item2;

    }
}
