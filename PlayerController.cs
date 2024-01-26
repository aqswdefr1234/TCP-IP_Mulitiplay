using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    const int _moveSpeed = 10;
    const int _jumpPower = 10;

    public int _mouseSpeed = 10;

    private Transform _body;
    private Transform _playerCam;
    private float _xRotate = 0f;
    private float _yRotate = 0f;
    private float _raySize = 1.1f;
    private Rigidbody _rigid;
    private RaycastHit _hit;

    //LimitState
    internal bool canMove = true;

    void Start()
    {
        _body = FindChildTrans(transform, "PlayerBody");
        _rigid = _body.GetComponent<Rigidbody>();
        _playerCam = GameObject.Find("PlayerCamera").transform;
    }
    void Update()
    {
        bool isFloor = IsLocateFloor();
        Move();
        Jump(isFloor);
        CameraPos();
        Look();
    }
    private Transform FindChildTrans(Transform target, string targetName)
    {
        foreach (Transform child in target)
        {
            if (child.name == targetName)
                return child;
        }
        return null;
    }
    private void Move()
    {
        if (canMove == false)
            return;

        float horizon = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        float distance = _moveSpeed * Time.deltaTime;
        _body.Translate(new Vector3(horizon * distance, 0, vertical * distance));
    }
    private void Jump(bool isFloor)
    {
        if (Input.GetKeyDown(KeyCode.Space) && isFloor)
        {
            _rigid.AddForce(Vector3.up * _jumpPower, ForceMode.Impulse);
        }
    }
    private bool IsLocateFloor()
    {
        if (Physics.Raycast(_body.position, -_body.up, out _hit, _raySize))
        {
            return true;
        }
        return false;
    }
    private void CameraPos()
    {
        _playerCam.position = _body.position + new Vector3(0, 0.5f, 0.5f);
    }
    private void Look()
    {
        float horizontal = Input.GetAxis("Mouse X") * _mouseSpeed;
        float vertical = -Input.GetAxis("Mouse Y") * _mouseSpeed;

        _xRotate = Mathf.Clamp(_xRotate + vertical, -45, 80);
        _yRotate = _body.eulerAngles.y + horizontal;

        _playerCam.eulerAngles = new Vector3(_xRotate, _yRotate, 0);
        _body.eulerAngles = new Vector3(0, _yRotate, 0);
    }
}
