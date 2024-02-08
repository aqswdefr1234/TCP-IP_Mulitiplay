using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DetectGround : MonoBehaviour//Only MyPlayerObject
{
    // Start is called before the first frame update
    static public bool isAttach = false;
    void OnTriggerStay() { isAttach = true; }
    void OnTriggerExit() { isAttach = false; }
}
