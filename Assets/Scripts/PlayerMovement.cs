using UnityEngine;
using Unity.Netcode;
using System.Collections;

namespace HockeyGame.Game
{
    public class PlayerMovement : NetworkBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float sprintSpeed = 8f;
        [SerializeField] private float rotationSpeed = 100f;
        [SerializeField] private float maxStamina = 100f;
        [SerializeField] private float staminaRegenRate = 10f;
        [SerializeField] private float staminaDrainRate = 20f;

        private NetworkVariable<bool> isSkating = new NetworkVariable<bool>();
        private NetworkVariable<bool> isShooting = new NetworkVariable<bool>();
        private float currentStamina;
        private bool canSprint;
        private Puck currentPuck;
        private float currentShootCharge;
        private Rigidbody rb;
        private Animator animator;

        public override void OnNetworkSpawn()
        {
            if (!IsOwner) return;

            rb = GetComponent<Rigidbody>();
            animator = GetComponent<Animator>();
            currentStamina = maxStamina;
            canSprint = true;

            isSkating.OnValueChanged += OnSkatingChanged;
            isShooting.OnValueChanged += OnShootingChanged;
        }

        private void Update()
        {
            if (!IsOwner) return;

            HandleMovementInput();
            HandleShootingInput();
            HandleStamina();
        }

        private void FixedUpdate()
        {
            if (!IsOwner) return;

            ApplyMovement();
        }

        private void HandleMovementInput()
        {
            float horizontal = Input.GetAxisRaw("Horizontal");
            float vertical = Input.GetAxisRaw("Vertical");
            bool isSprinting = Input.GetKey(KeyCode.LeftShift) && canSprint;

            Vector3 movement = new Vector3(horizontal, 0f, vertical).normalized;
            if (movement != Vector3.zero)
            {
                UpdateSkatingServerRpc(true);
                float targetSpeed = isSprinting ? sprintSpeed : moveSpeed;
                rb.linearVelocity = movement * targetSpeed;
                
                Quaternion targetRotation = Quaternion.LookRotation(movement);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }
            else
            {
                UpdateSkatingServerRpc(false);
                rb.linearVelocity = Vector3.zero;
            }
        }

        private void HandleStamina()
        {
            bool isSprinting = Input.GetKey(KeyCode.LeftShift);
            
            if (isSprinting && isSkating.Value)
            {
                currentStamina -= staminaDrainRate * Time.deltaTime;
                if (currentStamina <= 0)
                {
                    canSprint = false;
                    currentStamina = 0;
                }
            }
            else
            {
                currentStamina += staminaRegenRate * Time.deltaTime;
                if (currentStamina >= maxStamina)
                {
                    canSprint = true;
                    currentStamina = maxStamina;
                }
            }
        }

        private void HandleShootingInput()
        {
            if (Input.GetMouseButtonDown(0))
            {
                UpdateShootingServerRpc(true);
                if (currentPuck != null)
                {
                    currentPuck.Shoot(transform.forward * 10f, 1.0f, false);
                    currentPuck = null;
                }
            }
            else if (Input.GetMouseButtonUp(0))
            {
                UpdateShootingServerRpc(false);
            }
        }

        private void OnSkatingChanged(bool previousValue, bool newValue)
        {
            if (animator != null)
            {
                animator.SetBool("IsSkating", newValue);
            }
        }

        private void OnShootingChanged(bool previousValue, bool newValue)
        {
            if (animator != null)
            {
                animator.SetBool("IsShooting", newValue);
            }
        }

        private void ApplyMovement()
        {
            if (rb == null) return;
            rb.constraints = RigidbodyConstraints.FreezeRotation;
        }

        [ServerRpc]
        private void UpdateSkatingServerRpc(bool value)
        {
            isSkating.Value = value;
        }

        [ServerRpc]
        private void UpdateShootingServerRpc(bool value)
        {
            isShooting.Value = value;
            UpdateShootingClientRpc(value);
        }

        [ClientRpc]
        private void UpdateShootingClientRpc(bool value)
        {
            if (!IsOwner)
            {
                isShooting.Value = value;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsOwner) return;

            if (other.CompareTag("Puck") && currentPuck == null)
            {
                currentPuck = other.GetComponent<Puck>();
                if (currentPuck != null)
                {
                    currentPuck.PickUp(transform);
                }
            }
        }
    }
}