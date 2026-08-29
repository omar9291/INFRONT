using Unity.Netcode;
using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Server-autoritativer First-Person-Charakter mit Zielen hoch/runter.
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
    public sealed class NetworkPlayerController : NetworkBehaviour, IAimSource
    {
        [Header("Bewegung")]
        [SerializeField] float _walkSpeed = 6f;
        [SerializeField] float _sprintSpeed = 10f;
        [SerializeField] float _jumpHeight = 1.5f;
        [SerializeField] float _gravity = 20f;
        [SerializeField] float _turnLerp = 20f;

        [Header("Zielen")]
        [SerializeField] Transform _aimPivot;
        [SerializeField] float _maxPitch = 80f;

        [Header("First Person")]
        [SerializeField] GameObject[] _hideForOwner;

        CharacterController _controller;
        IPlayerInputSource _input;
        FirstPersonCamera _camera;
        float _viewYaw;
        float _viewPitch;

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

        // IAimSource: die Waffe holt sich hier Ursprung und Richtung
        public Vector3 AimOrigin => _aimPivot != null ? _aimPivot.position : transform.position + Vector3.up * 1.6f;
        public Vector3 AimDirection => _aimPivot != null ? _aimPivot.forward : transform.forward;

        void Awake()
        {
            _controller = GetComponent<CharacterController>();
        }

        public override void OnNetworkSpawn()
        {
            if (IsOwner)
            {
                _input ??= new KeyboardMouseInputSource(transform.eulerAngles.y, GameSettings.MouseSensitivity);

                _viewYaw = transform.eulerAngles.y;
                _viewPitch = 0f;

                var cam = Camera.main;
                if (cam != null && cam.TryGetComponent(out _camera))
                    _camera.SetTarget(_aimPivot != null ? _aimPivot : transform);

                // Eigenen Koerper ausblenden - sonst schaut man in die eigene Kapsel
                foreach (var part in _hideForOwner)
                    if (part != null)
                        foreach (var r in part.GetComponentsInChildren<Renderer>())
                            r.enabled = false;
            }

            if (IsServer)
                _verticalVelocity = 0f;
            else
                _controller.enabled = false; // nur der Server simuliert Bewegung
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
                bool paused = PauseMenu.IsPaused;

                if (!paused)
                {
                    _viewYaw = _input.LookYaw;
                    _viewPitch = Mathf.Clamp(_input.LookPitch, -_maxPitch, _maxPitch);
                }

                _pending.Move = paused ? Vector2.zero : _input.Move;
                _pending.Yaw = _viewYaw;
                _pending.Pitch = _viewPitch;
                _pending.Sprint = !paused && _input.Sprint;
                if (!paused && _input.JumpPressed)
                    _jumpLatched = true;

                // Kamera SOFORT lokal fuehren - kein Netzwerk, keine Verzoegerung
                if (_camera != null)
                    _camera.SetView(_viewYaw, _viewPitch);
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

            // Koerper schaut dorthin, wo gezielt wird (Kamera-Yaw), nicht in die
            // Laufrichtung. Schnelle, aber weiche Drehung.
            transform.rotation = Quaternion.Slerp(transform.rotation, yawRotation, _turnLerp * dt);
        }
    }
}
