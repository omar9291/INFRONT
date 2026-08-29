using Unity.Netcode;
using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Server-autoritativer Third-Person-Charakter mit Zielen hoch/runter.
    ///
    /// Ablauf:
    ///  - Der besitzende Client liest jeden Frame seine Eingaben und schickt sie
    ///    als <see cref="PlayerInputCommand"/> an den Server (SubmitCommandRpc).
    ///  - Nur der Server bewegt den CharacterController, dreht den Koerper (Yaw)
    ///    und neigt den Ziel-Drehpunkt (Pitch).
    ///  - NetworkTransform (server-autoritativ) verteilt Position und Yaw.
    ///  - Der Pitch wird ueber eine eigene NetworkVariable verteilt, damit auch
    ///    andere Clients sehen, wohin gezielt wird.
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

        [Header("Zielen")]
        [SerializeField] Transform _aimPivot;
        [SerializeField] float _maxPitch = 80f;

        CharacterController _controller;
        IPlayerInputSource _input;

        // Nur Client-Besitzer
        PlayerInputCommand _pending;
        bool _jumpLatched;

        // Nur Server
        PlayerInputCommand _serverCommand;
        float _verticalVelocity;
        bool _movementEnabled = true;

        readonly NetworkVariable<float> _aimPitch = new(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public float VerticalVelocity => _verticalVelocity;
        public float AimPitch => _aimPitch.Value;

        /// <summary>Die Eingabequelle dieses Spielers. Auch die Waffe liest hier.</summary>
        public IPlayerInputSource Input => _input;

        /// <summary>Ursprung und Richtung fuer Schuesse. Vom Server geneigt.</summary>
        public Transform AimPivot => _aimPivot;

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
                    shoulder.SetTarget(transform, this);
            }

            if (IsServer)
                _verticalVelocity = 0f;
        }

        /// <summary>Setzt eine andere Eingabequelle. Wird von PlayMode-Tests genutzt.</summary>
        public void SetInputSource(IPlayerInputSource source)
        {
            _input = source;
        }

        /// <summary>Nur Server: Bewegung an/aus (z.B. waehrend Tod).</summary>
        public void SetMovementEnabled(bool enabled)
        {
            if (IsServer)
                _movementEnabled = enabled;
        }

        void Update()
        {
            if (IsOwner && _input != null)
            {
                _pending.Move = _input.Move;
                _pending.Yaw = _input.LookYaw;
                _pending.Pitch = Mathf.Clamp(_input.LookPitch, -_maxPitch, _maxPitch);
                _pending.Sprint = _input.Sprint;
                if (_input.JumpPressed)
                    _jumpLatched = true;
            }

            // Nicht-Server-Instanzen: Ziel-Drehpunkt aus der NetworkVariable neigen
            if (!IsServer && _aimPivot != null)
                _aimPivot.localRotation = Quaternion.Euler(_aimPitch.Value, 0f, 0f);
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
                _serverCommand.Jump = false;
            }
        }

        [Rpc(SendTo.Server)]
        void SubmitCommandRpc(PlayerInputCommand command)
        {
            bool keepJump = _serverCommand.Jump || command.Jump;
            _serverCommand = command;
            _serverCommand.Jump = keepJump;
        }

        void Simulate(PlayerInputCommand command, float dt)
        {
            Quaternion yawRotation = Quaternion.Euler(0f, command.Yaw, 0f);

            // Ziel-Drehpunkt neigen (Server ist die Wahrheit)
            float pitch = Mathf.Clamp(command.Pitch, -_maxPitch, _maxPitch);
            _aimPitch.Value = pitch;
            if (_aimPivot != null)
                _aimPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);

            if (!_movementEnabled)
            {
                _verticalVelocity = 0f;
                return;
            }

            Vector3 wish = yawRotation * new Vector3(command.Move.x, 0f, command.Move.y);
            wish = Vector3.ClampMagnitude(wish, 1f);
            float speed = command.Sprint ? _sprintSpeed : _walkSpeed;

            if (_controller.isGrounded)
            {
                _verticalVelocity = -2f;
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
