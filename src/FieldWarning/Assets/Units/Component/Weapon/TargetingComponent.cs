/**
 * Copyright (c) 2017-present, PFW Contributors.
 *
 * Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in
 * compliance with the License. You may obtain a copy of the License at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software distributed under the License is
 * distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See
 * the License for the specific language governing permissions and limitations under the License.
 */

using UnityEngine;

using PFW.Model.Game;
using PFW.Model.Armory;

namespace PFW.Units.Component.Weapon
{
    /// <summary>
    /// Manages a single weapon by picking targets for it.
    /// </summary>
    public class TargetingComponent : MonoBehaviour
    {
        public UnitDispatcher Unit { get; private set; }
        private bool _movingTowardsTarget = false;
        private TargetTuple _target;

        public void SetTarget(Vector3 position, bool autoApproach = true)
        {
            SetTarget(new TargetTuple(position), autoApproach);
        }

        private void SetTarget(TargetTuple target, bool autoApproach)
        {
            Logger.LogTargeting("Received target from the outside.", gameObject);
            float distance = Vector3.Distance(Unit.transform.position, target.Position);

            _target = target;

            if (distance > _fireRange && autoApproach) {
                _movingTowardsTarget = true;
                Unit.SetDestination(target.Position);
            }

            _turretComponent.SetTarget(_target, _turretPriority);
        }

        private IWeapon _weapon;
        private int _fireRange;

        // --------------- BEGIN PREFAB ----------------

        // TODO the weapon class should create its own audio source:
        [SerializeField]
        private AudioSource _audioSource = null;

        [SerializeField]
        private TurretComponent _turretComponent = null;
        /// <summary>
        /// When a unit has multiple weapons that share a turret,
        /// the turret will prefer to rotate for the higher-priority weapon.
        /// </summary>
        [SerializeField]
        private int _turretPriority = 0;

        // Where the shell spawns:
        [SerializeField]
        private Transform _shotStarterPosition = null;

        [SerializeField]
        private AudioClip _shotSound = null;

        [SerializeField]
        private float _shotVolume = 1.0F;

        // TODO Should aim to make actual objects fire and not effects:
        [SerializeField]
        private ParticleSystem _shotEffect = null;

        [SerializeField]
        private ParticleSystem _muzzleFlashEffect = null;
        // ---------------- END PREFAB -----------------

        /// <summary>
        /// Constructor equivalent for MonoBehaviours.
        /// </summary>
        public void Initialize(
                TurretComponent turret,
                TurretConfig weaponData,
                ParticleSystem shotEffect,
                ParticleSystem muzzleFlashEffect)
        {
            _turretComponent = turret;
            _turretPriority = weaponData.Priority;

            _shotEffect = shotEffect;
            _muzzleFlashEffect = muzzleFlashEffect;

            // TODO just pass the weapon from the outside?
            if (weaponData.Cannon != null)
            {
                _weapon = new Cannon(
                        weaponData.Cannon,
                        _audioSource,
                        _shotEffect,
                        _shotSound,
                        _muzzleFlashEffect,
                        _shotVolume);
                _fireRange = weaponData.Cannon.FireRange;
            }
            else if (weaponData.Howitzer != null)
            {
                _weapon = new Howitzer(
                        weaponData.Howitzer,
                        _audioSource,
                        _shotEffect,
                        _shotSound,
                        _shotStarterPosition,
                        _shotVolume);
                _fireRange = weaponData.Howitzer.FireRange;
            }
            else 
            {
                Debug.LogError("Couldn't create a weapon in targeting component. " +
                    "No weapon specified in the config?");
            }

            Logger.LogTargeting("Created a weapon in TargetingComponent.Initialize().", gameObject);
        }

        private void Awake()
        {
            enabled = false;
        }

        private void Start()
        {
            Unit = gameObject.GetComponent<UnitDispatcher>();
        }

        private void StopMovingIfInRangeOfTarget()
        {
            if (_movingTowardsTarget) {
                if (Vector3.Distance(Unit.transform.position, _target.Position) < _fireRange) {
                    _movingTowardsTarget = false;
                    Unit.SetDestination(Unit.transform.position);

                    Logger.LogTargeting(
                        "Stopped moving because a targeted enemy unit is in range.", gameObject);
                }
            }
        }

        private void Update()
        {
            StopMovingIfInRangeOfTarget();

            if (_target != null && _target.Exists) {
                MaybeDropOutOfRangeTarget();

                if (_target.IsUnit && !_target.Enemy.VisionComponent.IsSpotted) {
                    Logger.LogTargeting(
                        "Dropping a target because it is no longer spotted.", gameObject);
                    _target = null;
                }
            }

            if (_target != null && _target.Exists) {

                bool targetInRange = !_movingTowardsTarget;
                bool shotFired = false;

                if (_turretComponent.IsFacingTarget && targetInRange)
                {
                    // The displacement from the unit to the target
                    Vector3 displacement = _target.Position - Unit.transform.position;
                    shotFired = _weapon.TryShoot(_target, Time.deltaTime, displacement);
                }

                // If shooting at the ground, stop after the first shot:
                if (shotFired && _target.IsGround)
                {
                    _target = null;
                    _turretComponent.SetTarget(null, _turretPriority);
                }

            } else {
                FindAndTargetClosestEnemy();
            }
        }

        private void FindAndTargetClosestEnemy()
        {
            Logger.LogTargeting("Scanning for a target.", gameObject);

            // TODO utilize precomputed distance lists from session
            // Maybe add Sphere shaped collider with the radius of the range and then 
            // use trigger enter and exit to keep a list of in range Units

            foreach (UnitDispatcher enemy in MatchSession.Current.EnemiesByTeam[Unit.Platoon.Owner.Team]) {
                if (!enemy.VisionComponent.IsSpotted)
                    continue;

                // See if they are in range of weapon:
                float distance = Vector3.Distance(Unit.transform.position, enemy.Transform.position);
                if (distance < _fireRange) {
                    Logger.LogTargeting("Target found and selected after scanning.", gameObject);
                    SetTarget(enemy.TargetTuple, false);
                    break;
                }
            }
        }

        /// <summary>
        /// If the target is an enemy unit and it is out of range,
        /// forget about it.
        /// </summary>
        private void MaybeDropOutOfRangeTarget()
        {
            // We only drop unit targets, not positions:
            if (_target.Enemy == null)
                return;

            float distance = Vector3.Distance(Unit.transform.position, _target.Position);
            if (distance > _fireRange) {
                _target = null;
                Logger.LogTargeting("Dropping a target because it is out of range.", gameObject);
            }
        }

        public bool HasTarget => _target != null;
    }
}

