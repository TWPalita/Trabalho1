using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Controlador de personagem em primeira pessoa para exploração 3D da espaçonave.
/// Suporta o Novo Input System e fallback para o Input Manager antigo.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movimentação")]
    [SerializeField] private float walkSpeed = 4.5f;
    [SerializeField] private float runSpeed = 7.0f;
    [SerializeField] private float gravity = -9.81f;

    [Header("Câmera e Visão")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private float mouseSensitivity = 2.0f;
    [SerializeField] private float minPitch = -80f;
    [SerializeField] private float maxPitch = 80f;

    private CharacterController characterController;
    private float pitch = 0f;
    private Vector3 verticalVelocity = Vector3.zero;
    private bool cursorLocked = true;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        if (cameraTransform == null)
        {
            Camera cam = GetComponentInChildren<Camera>();
            if (cam != null) cameraTransform = cam.transform;
        }
    }

    private void Start()
    {
        SetCursorLock(true);
    }

    private void Update()
    {
        HandleCursorLockToggle();
        HandleLook();
        HandleMovement();
    }

    private void HandleCursorLockToggle()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            SetCursorLock(!cursorLocked);
        }
        else if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && !cursorLocked)
        {
            SetCursorLock(true);
        }
#else
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            SetCursorLock(!cursorLocked);
        }
        else if (Input.GetMouseButtonDown(0) && !cursorLocked)
        {
            SetCursorLock(true);
        }
#endif
    }

    public void SetCursorLock(bool locked)
    {
        cursorLocked = locked;
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    private void HandleLook()
    {
        if (!cursorLocked) return;

        Vector2 lookDelta = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            lookDelta = Mouse.current.delta.ReadValue() * (mouseSensitivity * 0.1f);
        }
#else
        lookDelta = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * mouseSensitivity;
#endif

        // Rotação horizontal (Yaw) gira o corpo do jogador
        transform.Rotate(Vector3.up * lookDelta.x);

        // Rotação vertical (Pitch) gira apenas a câmera
        pitch -= lookDelta.y;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        if (cameraTransform != null)
        {
            cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }
    }

    private void HandleMovement()
    {
        Vector2 inputMove = Vector2.zero;
        bool isSprinting = false;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) inputMove.y += 1f;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) inputMove.y -= 1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) inputMove.x += 1f;
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) inputMove.x -= 1f;

            isSprinting = Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed;
        }
#else
        inputMove.x = Input.GetAxisRaw("Horizontal");
        inputMove.y = Input.GetAxisRaw("Vertical");
        isSprinting = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
#endif

        inputMove = inputMove.normalized;
        float currentSpeed = isSprinting ? runSpeed : walkSpeed;

        Vector3 move = (transform.right * inputMove.x + transform.forward * inputMove.y) * currentSpeed;

        if (characterController.isGrounded && verticalVelocity.y < 0)
        {
            verticalVelocity.y = -2f; // Mantém no chão
        }

        verticalVelocity.y += gravity * Time.deltaTime;

        characterController.Move((move + verticalVelocity) * Time.deltaTime);
    }
}
