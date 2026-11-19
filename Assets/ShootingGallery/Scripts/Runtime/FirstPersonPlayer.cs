using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Control en primera persona: flechas (o WASD) para mover, raton para mirar.
/// Clic derecho activa la mira (ADS) con FOV mas cerrado y movimiento mas lento.</summary>
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

    [Header("Mira ADS y balanceo")]
    public float aimFov = 54f;
    public float aimSpeedMultiplier = 0.62f;
    public float aimLookMultiplier = 0.68f;

    public bool IsAiming { get; private set; }

    private CharacterController _controller;
    private float _pitch;
    private float _recoilPitch;
    private float _verticalSpeed;
    private float _baseFov = 76f;
    private float _bobPhase;
    private Vector3 _cameraBasePosition;

    private void Awake()
    {
        Instance = this;
        _controller = GetComponent<CharacterController>();
        if (viewCamera == null) viewCamera = GetComponentInChildren<Camera>();
        if (viewCamera != null)
        {
            _baseFov = viewCamera.fieldOfView;
            _cameraBasePosition = viewCamera.transform.localPosition;
        }
    }

    private void Start() { LockCursor(); }

    private void Update()
    {
        float dt = Time.deltaTime;

        if (GameManager.Instance != null && GameManager.Instance.IsGameOver)
        {
            IsAiming = false;
            UnlockCursor();
            return;
        }

        Keyboard kb = Keyboard.current;
        Mouse mouse = Mouse.current;
        if (kb == null || mouse == null) return;

        IsAiming = mouse.rightButton.isPressed && Cursor.lockState == CursorLockMode.Locked;
        float sensitivity = lookSensitivity * (IsAiming ? aimLookMultiplier : 1f);

        // --- Mirar con el raton (el recoil se recupera solo)
        _recoilPitch = Mathf.MoveTowards(_recoilPitch, 0f, 28f * dt);
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            Vector2 delta = mouse.delta.ReadValue();
            transform.Rotate(0f, delta.x * sensitivity, 0f);
            _pitch = Mathf.Clamp(_pitch - delta.y * sensitivity, -80f, 80f);
        }

        if (viewCamera != null)
        {
            viewCamera.transform.localEulerAngles = new Vector3(_pitch - _recoilPitch, 0f, 0f);
            viewCamera.fieldOfView = Mathf.Lerp(viewCamera.fieldOfView,
                IsAiming ? aimFov : _baseFov, 12f * dt);
        }

        // --- Moverse con flechas (tambien WASD por comodidad)
        float forward = 0f, side = 0f;
        if (kb.upArrowKey.isPressed || kb.wKey.isPressed) forward += 1f;
        if (kb.downArrowKey.isPressed || kb.sKey.isPressed) forward -= 1f;
        if (kb.rightArrowKey.isPressed || kb.dKey.isPressed) side += 1f;
        if (kb.leftArrowKey.isPressed || kb.aKey.isPressed) side -= 1f;

        float currentSpeed = moveSpeed * (IsAiming ? aimSpeedMultiplier : 1f);
        Vector3 move = (transform.forward * forward + transform.right * side).normalized * currentSpeed;

        if (_controller.isGrounded)
        {
            _verticalSpeed = -2f;
            if (kb.spaceKey.wasPressedThisFrame) _verticalSpeed = jumpSpeed;
        }
        else
        {
            _verticalSpeed += gravity * dt;
        }

        move.y = _verticalSpeed;
        _controller.Move(move * dt);

        // Balanceo sutil de la camara al caminar (casi inexistente al apuntar).
        Vector3 planar = new Vector3(_controller.velocity.x, 0f, _controller.velocity.z);
        if (planar.sqrMagnitude > 0.01f && _controller.isGrounded)
            _bobPhase += planar.magnitude * dt * 1.6f;
        float bobScale = Mathf.Clamp01(planar.magnitude / Mathf.Max(0.01f, moveSpeed)) *
                         (IsAiming ? 0.004f : 0.014f);
        Vector3 bob = new Vector3(Mathf.Cos(_bobPhase * 0.5f) * bobScale * 0.6f,
            Mathf.Sin(_bobPhase) * bobScale, 0f);
        if (viewCamera != null)
            viewCamera.transform.localPosition = Vector3.Lerp(
                viewCamera.transform.localPosition, _cameraBasePosition + bob, 14f * dt);

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
