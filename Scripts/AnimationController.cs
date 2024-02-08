using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Debug = UnityEngine.Debug;
public class AnimationController : MonoBehaviour
{
    private Animator animator;
    private Transform targetTrans;
    Dictionary<string, bool> conditionDict = new Dictionary<string, bool>();
    List<string> conditionList = new List<string>();

    internal bool isGround = false;
    void Start()
    {
        targetTrans = transform;
        animator = transform.GetComponent<Animator>();
        ReadBoolParameter(conditionDict);
        StartCoroutine(AnimationPlay(0.1f));
    }
    IEnumerator AnimationPlay(float delayTime)
    {
        WaitForSeconds delay = new WaitForSeconds(delayTime);
        Vector3 prePos = new Vector3();
        Vector3 nowPos = new Vector3();
        while (true)
        {
            prePos = nowPos;
            nowPos = targetTrans.position;
            float yRotAngle = targetTrans.rotation.eulerAngles.y;
            (float mag, float angleDifference) = CalBetweenDirAndRot(prePos, nowPos, yRotAngle);
            ChangeConditions(mag, angleDifference);
            yield return delay;
        }
    }
    (float, float) CalBetweenDirAndRot(Vector3 prePos, Vector3 nowPos, float rotAngle)
    {
        (float mag, float targetDirAngle) = CalDirAngle(prePos, nowPos);
        float angleBetween = CalAngleBetween(targetDirAngle, rotAngle);
        return (mag, angleBetween);
    }
    (float, float) CalDirAngle(Vector3 vec1, Vector3 vec2)// 0 ~ 360
    {
        Vector3 dir = vec2 - vec1;
        float dirAngle = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        if (dirAngle < 0) dirAngle += 360f;// -180 ~ 180 => 0 ~ 360
        return (dir.magnitude, dirAngle);
    }
    float CalAngleBetween(float angle1, float angle2)//기준 각도를 0도로 재배치
    {
        float newAngle = 360f - angle2;
        angle1 += newAngle;
        if (angle1 > 360f) angle1 -= 360f;
        return angle1;
    }
    void ChangeConditions(float mag, float angle)
    {
        if (!isGround) SetConditions("jump");
        else if(mag < 0.01f) SetConditions("idle");
        else if (angle < 60f || angle > 300f) SetConditions("forward");
        else if (angle < 120f) SetConditions("right");
        else if (angle < 240f) SetConditions("back");
        else if (angle < 300f)SetConditions("left");
    }
    void ReadBoolParameter(Dictionary<string, bool> dict)
    {
        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.type == AnimatorControllerParameterType.Bool)
            {
                dict.Add(parameter.name, parameter.defaultBool);
                conditionList.Add(parameter.name);
            }
        }
    }
    void SetConditions(string keyName)
    {
        if(conditionDict[keyName] != true)
        {
            InitializeAni();
            conditionDict[keyName] = true;
            animator.SetBool(keyName, true);
        }
    }
    void InitializeAni()
    {
        foreach (string aniName in conditionList)
        {
            conditionDict[aniName] = false;
            animator.SetBool(aniName, false);
        }
    }
    void OnApplicationQuit()
    {
        StopCoroutine("AnimationPlay");
    }
    void OnTriggerStay() { isGround = true; }
    void OnTriggerExit() { isGround = false; }
}