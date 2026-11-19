using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Control en primera persona: flechas (o WASD) para mover, raton para mirar.</summary>
[RequireComponent(typeof(CharacterController))]
public class FirstPersonPlayer : MonoBehaviour
{
    public static FirstPersonPlayer Instance { get; private set; }

    [Header("Referencias")]
    public Camera viewCamera;

    [Header("Movimiento")]
    public float moveSpeed = 6f;
    public float lookSensitivity = 0.12f;
    public float jumpSpeed = 6f;
    public float gravity = -20f;

    private CharacterController _controller;
    private float _pitch;
    private float _recoilPitch;
    private float _verticalSpeed;

    private void Awake()
    {
        Instance = this;
        _controller = GetComponent<CharacterController>();
        if (viewCamera == null) viewCamera = GetComponentInChildren<Camera>();
    }

    private void Start() { LockCursor(); }

    private void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsGameOver)
        {
            UnlockCursor();
            return;
        }

        Keyboard kb = Keyboard.current;
        Mouse mouse = Mouse.current;
        if (kb == null || mouse == null) return;

        // --- Mirar con el raton (el recoil se recupera solo)
        _recoilPitch = Mathf.MoveTowards(_recoilPitch, 0f, 28f * Time.deltaTime);
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            Vector2 delta = mouse.delta.ReadValue();
            transform.Rotate(0f, delta.x * lookSensitivity, 0f);
            _pitch = Mathf.Clamp(_pitch - delta.y * lookSensitivity, -80f, 80f);
        }
        if (viewCamera != null)
            viewCamera.transform.localEulerAngles = new Vector3(_pitch - _recoilPitch, 0f, 0f);

        // --- Moverse con flechas (tambien WASD por comodidad)
        float forward = 0f, side = 0f;
        if (kb.upArrowKey.isPressed || kb.wKey.isPressed) forward += 1f;
        if (kb.downArrowKey.isPressed || kb.sKey.isPressed) forward -= 1f;
        if (kb.rightArrowKey.isPressed || kb.dKey.isPressed) side += 1f;
        if (kb.leftArrowKey.isPressed || kb.aKey.isPressed) side -= 1f;

        Vector3 move = (transform.forward * forward + transform.right * side).normalized * moveSpeed;

        if (_controller.isGrounded)
        {
            _verticalSpeed = -2f;
            if (kb.spaceKey.wasPressedThisFrame) _verticalSpeed = jumpSpeed;
        }
        else
        {
            _verticalSpeed += gravity * Time.deltaTime;
        }

        move.y = _verticalSpeed;
        _controller.Move(move * Time.deltaTime);

        if (kb.escapeKey.wasPressedThisFrame) UnlockCursor();
        if (mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
            LockCursor();
    }

    public void TakeDamage(int amount)
    {
        GameManager.Instance?.TakeDamage(amount);
    }

    /// <summary>El arma llama a esto para levantar un poco la camara al disparar.</summary>
    public void AddRecoil(float pitchAmount)
    {
        _recoilPitch = Mathf.Min(10f, _recoilPitch + Mathf.Abs(pitchAmount));
    }

    public void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
