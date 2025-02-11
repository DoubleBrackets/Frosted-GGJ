using System;
using Bubble;
using Global;
using UnityEditor;
using UnityEngine;
using Util;
using Random = UnityEngine.Random;

namespace NPC
{
    public class NPC : MonoBehaviour
    {
        public enum TetherTypes
        {
            Friendly,
            Negative
        }

        private enum State
        {
            Wandering,
            MovingTowardsPlayer,
            TetheredToPlayer
        }

        [Header("Depends")]

        [SerializeField]
        private SimpleMovement _simpleMovement;

        [SerializeField]
        private Bubble.Bubble _bubble;

        [SerializeField]
        private GlobalState _globalState;

        [SerializeField]
        private Transform _body;

        [SerializeField]
        private Animator _anim;

        [SerializeField]
        private SpriteRenderer _spriteRenderer;

        [Header("Movement Config")]

        [SerializeField]
        private MovementConfig _wanderingMovement;

        [SerializeField]
        private MovementConfig _followMovementConfig;

        [SerializeField]
        private MovementConfig _tetheredMovementConfig;

        [SerializeField]
        private float _bounceAwayVelocity;

        [Header("Tether Config")]

        [SerializeField]
        private float _followDistance;

        [SerializeField]
        private TetherTypes _tetherBehavior;

        [Header("Wander Config")]

        [SerializeField]
        private float _wanderRadius;

        [SerializeField]
        private Vector2 _wanderChangeTimeRange;

        private State _state = State.Wandering;

        private Vector2 _wanderCenter;
        private Vector2 _targetPosition;

        private float _timeToNextWanderChange;

        private void Awake()
        {
            _bubble.OnBecomeIndividual.AddListener(OnBecomeIndividual);
            _bubble.OnAbsorbedByOther.AddListener(OnAbsorbedByOther);
            _bubble.OnAbsorbedOther.AddListener(OnAbsorbOther);
            _bubble.OnBumpIntoHardened.AddListener(OnBumpIntoHardened);
        }

        private void Start()
        {
            _wanderCenter = _body.position;
        }

        private void Update()
        {
            switch (_state)
            {
                case State.Wandering:
                    Wander();
                    FlipTowardsVel();

                    break;
                case State.TetheredToPlayer:
                    Tethered();
                    break;
                case State.MovingTowardsPlayer:
                    MoveTowardsPlayer();
                    FlipTowardsVel();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void OnDestroy()
        {
            _bubble.OnAbsorbedByOther.RemoveListener(OnAbsorbedByOther);
            _bubble.OnBecomeIndividual.RemoveListener(OnBecomeIndividual);
            _bubble.OnAbsorbedOther.RemoveListener(OnAbsorbOther);
            _bubble.OnBumpIntoHardened.RemoveListener(OnBumpIntoHardened);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = _tetherBehavior == TetherTypes.Friendly ? Color.yellow : Color.red;

#if UNITY_EDITOR
            Handles.Label(_body.position + Vector3.right, _state.ToString());
#endif

            if (Application.isPlaying)
            {
                Gizmos.DrawLine(_body.position, _targetPosition);
                Gizmos.DrawWireSphere(_wanderCenter, _wanderRadius);
            }
            else
            {
                Gizmos.DrawWireSphere(_body.position, _wanderRadius);
            }
        }

        private void FlipTowardsVel()
        {
            if (_simpleMovement.Rb.linearVelocity.x > 0)
            {
                _spriteRenderer.flipX = false;
            }
            else if (_simpleMovement.Rb.linearVelocity.x < 0)
            {
                _spriteRenderer.flipX = true;
            }
        }

        private void OnBumpIntoHardened(Bubble.Bubble hardenedBubble)
        {
            Vector2 dirAway = (Vector2)_body.position - hardenedBubble.RealPosition;

            _simpleMovement.Rb.linearVelocity = dirAway.normalized * _bounceAwayVelocity;
        }

        private void OnBecomeIndividual()
        {
            // Enemies can break away; friendlys follow forever
            if (_tetherBehavior == TetherTypes.Negative)
            {
                _state = State.Wandering;
            }
        }

        private void MoveTowardsPlayer()
        {
            if (!IsPlayerWithinFollowDistance())
            {
                _state = State.Wandering;
            }

            _targetPosition = _globalState.PlayerPos;
            Vector2 direction = _targetPosition - (Vector2)_body.position;

            _simpleMovement.Move(_followMovementConfig, direction, Time.deltaTime);
            _anim.Play(WalkAnim());
        }

        private void OnAbsorbedByOther(Bubble.Bubble other)
        {
            if (other.BubbleType == BubbleType.Player)
            {
                _state = State.TetheredToPlayer;
            }
        }

        private void OnAbsorbOther(Bubble.Bubble other)
        {
            if (other.BubbleType == BubbleType.Player && _tetherBehavior == TetherTypes.Negative)
            {
                _state = State.TetheredToPlayer;
            }
        }

        private void Wander()
        {
            if (_timeToNextWanderChange <= 0)
            {
                _timeToNextWanderChange = Random.Range(_wanderChangeTimeRange.x, _wanderChangeTimeRange.y);
                _targetPosition = _wanderCenter + Random.insideUnitCircle * _wanderRadius;
            }
            else
            {
                _timeToNextWanderChange -= Time.deltaTime;
            }

            // Move towards wander target
            Vector2 direction = _targetPosition - (Vector2)_body.position;
            if (direction.magnitude > 0.5f)
            {
                _simpleMovement.Move(_wanderingMovement, direction, Time.deltaTime);
                _anim.Play(WalkAnim());
            }
            else
            {
                _simpleMovement.StandStill(_wanderingMovement, Time.deltaTime);
                _anim.Play(WalkAnim());
            }

            // Distance from wander center
            if (IsPlayerWithinFollowDistance())
            {
                _state = State.MovingTowardsPlayer;
            }
        }

        private bool IsPlayerWithinFollowDistance()
        {
            return Vector2.Distance(_wanderCenter, _globalState.PlayerPos) < _wanderRadius;
        }

        private void Tethered()
        {
            if (_tetherBehavior == TetherTypes.Friendly)
            {
                _targetPosition = _globalState.PlayerPos;

                Vector2 direction = _targetPosition - (Vector2)_body.position;
                if (direction.magnitude > _followDistance)
                {
                    _simpleMovement.Move(_tetheredMovementConfig, direction, Time.deltaTime);
                    _anim.Play(WalkAnim());
                }
                else
                {
                    _simpleMovement.StandStill(_tetheredMovementConfig, Time.deltaTime);
                    _anim.Play(WalkAnim());
                }

                FlipTowardsVel();
            }
            else
            {
                _targetPosition = _globalState.PlayerPos;

                Vector2 direction = _targetPosition - (Vector2)_body.position;
                _simpleMovement.StandStill(_tetheredMovementConfig, Time.deltaTime);
                _anim.Play("Shout");

                _spriteRenderer.flipX = direction.x < 0;
            }
        }

        private string WalkAnim()
        {
            Vector2 velocity = _simpleMovement.Rb.linearVelocity;
            if (velocity.magnitude < 1f)
            {
                return "Idle";
            }

            if (velocity.y > Mathf.Abs(velocity.x))
            {
                return "Walk Up R";
            }

            if (velocity.y < Mathf.Abs(velocity.x))
            {
                return "Walk Down R";
            }

            return "Walk Straight R";
        }
    }
}