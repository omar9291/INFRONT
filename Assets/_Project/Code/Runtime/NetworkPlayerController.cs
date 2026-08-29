using Unity.Netcode;
using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Server-autoritativer Third-Person-Charakter.
    ///
    /// Ablauf:
    ///  - Der besitzende Client liest jeden Frame seine Eingaben und schickt sie
    ///    als <see cref="PlayerInputCommand"/> an den Server (SubmitCommandRpc).
    ///  - Nur der Server bewegt den CharacterController und wendet Schwerkraft an.
    ///  - Die NetworkTransform-Komponente (server-autoritativ, Standard) verteilt
    ///    die vom Server berechnete Position an alle.
    ///
    /// Der Client entscheidet nichts selbst. Das ist Absicht: so kann ein
    /// manipulierter Client sich keinen Vorteil verschaffen.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class NetworkPlayerController : NetworkBehaviour
    {
        [Header("Bewegung")]
        [SerializeField] float _walkSpeed = 6f;
        [SerializeField] float _sprintSpeed = 10f;
        [SerializeField] float _jumpHeight = 1.5f;
        [SerializeField] float _gravity = 20f;
        [SerializeField] float _turnLerp = 15f;

        CharacterController _controller;
        IPlayerInputSource _input;

        // Nur Client-Besitzer
        PlayerInputCommand _pending;
        bool _jumpLatched;

        // Nur Server
        PlayerInputCommand _serverCommand;
        float _verticalVelocity;

        /// <summary>Vom Server berechnete Vertikalgeschwindigkeit. Fuer Tests und HUD.</summary>
        public float VerticalVelocity => _verticalVelocity;

        void Awake()
        {
            _controller = GetComponent<CharacterController>();
        }

        public override void OnNetworkSpawn()
        {
            if (IsOwner)
            {
                _input ??= new KeyboardMouseInputSource(transform.eulerAngles.y);

                var cam = Camera.main;
                if (cam != null && cam.TryGetComponent(out ShoulderCamera shoulder))
                    shoulder.SetTarget(transform);
            }

            if (IsServer)
                _verticalVelocity = 0f;
        }

        /// <summary>Setzt eine andere Eingabequelle. Wird von PlayMode-Tests genutzt.</summary>
        public void SetInputSource(IPlayerInputSource source)
        {
            _input = source;
        }

        void Update()
        {
            if (!IsOwner || _input == null)
                return;

            _pending.Move = _input.Move;
            _pending.Yaw = _input.LookYaw;
            _pending.Sprint = _input.Sprint;
            if (_input.JumpPressed)
                _jumpLatched = true;
        }

        void FixedUpdate()
        {
            if (IsOwner)
            {
                _pending.Jump = _jumpLatched;
                _jumpLatched = false;
                SubmitCommandRpc(_pending);
            }

            if (IsServer)
            {
                Simulate(_serverCommand, Time.fixedDeltaTime);
                _serverCommand.Jump = false; // Sprung nur einmal pro empfangenem Kommando
            }
        }

        [Rpc(SendTo.Server)]
        void SubmitCommandRpc(PlayerInputCommand command)
        {
            // Sprung nicht ueberschreiben, falls er noch nicht verarbeitet wurde
            bool keepJump = _serverCommand.Jump || command.Jump;
            _serverCommand = command;
            _serverCommand.Jump = keepJump;
        }

        void Simulate(PlayerInputCommand command, float dt)
        {
            Quaternion yawRotation = Quaternion.Euler(0f, command.Yaw, 0f);
            Vector3 wish = yawRotation * new Vector3(command.Move.x, 0f, command.Move.y);
            wish = Vector3.ClampMagnitude(wish, 1f);
            float speed = command.Sprint ? _sprintSpeed : _walkSpeed;

            if (_controller.isGrounded)
            {
                _verticalVelocity = -2f; // leicht an den Boden druecken
                if (command.Jump)
                    _verticalVelocity = Mathf.Sqrt(2f * _gravity * _jumpHeight);
            }
            else
            {
                _verticalVelocity -= _gravity * dt;
            }

            Vector3 velocity = wish * speed + Vector3.up * _verticalVelocity;
            _controller.Move(velocity * dt);

            Vector3 face = wish.sqrMagnitude > 0.001f ? wish : (yawRotation * Vector3.forward);
            face.y = 0f;
            if (face.sqrMagnitude > 0.001f)
            {
                Quaternion target = Quaternion.LookRotation(face, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, target, _turnLerp * dt);
            }
        }
    }
}
