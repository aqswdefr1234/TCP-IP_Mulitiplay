using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DetectGround : MonoBehaviour//Only MyPlayerObject
{
    static public bool isAttach = false;
    void OnTriggerStay() { isAttach = true; }
    void OnTriggerExit() { isAttach = false; }
}
