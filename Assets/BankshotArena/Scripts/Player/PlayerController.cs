using UnityEngine;

/// <summary>
/// First-person movement + mouse look using a CharacterController.
/// Yaw rotates the body, pitch rotates only the camera pivot (so the body never tilts).
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("References")]
    public Transform cameraPivot;

    [Header("Movement")]
    public float moveSpeed = 6f;
    public float sprintMultiplier = 1.6f;
    public float jumpHeight = 1.2f;
    public float gravity = -20f;

    [Header("Look")]
    public float mouseSensitivity = 2f;
    public float maxPitch = 85f;

    CharacterController controller;
    float verticalVelocity;
    float pitch;

    /// <summary>Real velocity from the last frame — enemies read this to lead their shots.</summary>
    public Vector3 Velocity => controller != null ? controller.velocity : Vector3.zero;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsGameOver) return;

        if (Cursor.lockState == CursorLockMode.Locked) Look();
        Move();
    }

    void Look()
    {
        // Mouse axes are already "per frame" deltas, so no Time.deltaTime here.
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        transform.Rotate(0f, mouseX, 0f);                         // yaw: whole body
        pitch = Mathf.Clamp(pitch - mouseY, -maxPitch, maxPitch); // pitch: clamped so you can't flip over
        if (cameraPivot != null) cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    void Move()
    {
        Vector3 input = new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));
        // Without this, diagonal input has length ~1.41 and you move 41% faster diagonally.
        input = Vector3.ClampMagnitude(input, 1f);

        float speed = moveSpeed * (Input.GetKey(KeyCode.LeftShift) ? sprintMultiplier : 1f);
        // Local -> world: "forward" means wherever the player is facing.
        Vector3 horizontal = transform.TransformDirection(input) * speed;

        if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f; // keep snapped to ground

        if (controller.isGrounded && Input.GetButtonDown("Jump"))
        {
            // From v² = u² + 2as  ->  initial speed needed to reach jumpHeight
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        verticalVelocity += gravity * Time.deltaTime;

        Vector3 velocity = horizontal + Vector3.up * verticalVelocity;
        controller.Move(velocity * Time.deltaTime); // * deltaTime = frame-rate independent
    }
}
