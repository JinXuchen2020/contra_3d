using System;
using System.Collections.Generic;
using System.Numerics;

namespace Contra3D.Core
{
    public partial class AiSystem
    {
        private void UpdatePatrol(EnemyAIState state, EnemyDefinition def, float dt, bool playerInSight, float distToPlayer, bool playerInAttackRange)
        {
            switch (state.State)
            {
                case AiState.Idle:
                    if (state.Vigilance >= def.AlertThreshold)
                        state.State = AiState.Alert;
                    break;
                case AiState.Alert:
                    if (state.Vigilance >= def.ComprehensionThreshold)
                        state.State = AiState.Combat;
                    else if (state.Vigilance < def.AlertThreshold * 0.5f)
                        state.State = AiState.Idle;
                    break;
                case AiState.Combat:
                    if (!playerInSight || distToPlayer > def.VisionRange * 1.5f)
                    {
                        if (state.Vigilance <= 0f) state.State = AiState.Patrol;
                    }
                    else if (distToPlayer <= def.AttackRange)
                    {
                        // Attack
                    }
                    break;
                case AiState.Patrol:
                    // Move towards patrol target
                    if (Vector3.Distance(state.Position, state.PatrolTarget) < 1f)
                        state.PatrolTarget = state.Position + new Vector3(_random.NextFloat(-10f, 10f), 0, _random.NextFloat(-10f, 10f));
                    Vector3 dir = Vector3.Normalize(state.PatrolTarget - state.Position);
                    state.Position += dir * def.Speed * dt;
                    if (state.Vigilance >= def.AlertThreshold)
                        state.State = AiState.Alert;
                    break;
            }
        }

        private void UpdateChase(EnemyAIState state, EnemyDefinition def, float dt, bool playerInSight, float distToPlayer, bool playerInAttackRange)
        {
            switch (state.State)
            {
                case AiState.Idle:
                    if (playerInSight) state.State = AiState.Chase;
                    break;
                case AiState.Chase:
                    if (!playerInSight)
                    {
                        state.State = AiState.Idle;
                        break;
                    }
                    Vector3 dir = Vector3.Normalize(_playerPosition - state.Position);
                    state.Position += dir * def.Speed * dt;
                    if (playerInAttackRange) state.State = AiState.Combat;
                    break;
                case AiState.Combat:
                    if (!playerInSight || distToPlayer > def.AttackRange * 1.5f)
                        state.State = AiState.Chase;
                    // Attack logic handled by weapon_system
                    break;
            }
        }

        private void UpdateSniper(EnemyAIState state, EnemyDefinition def, float dt, bool playerInSight, float distToPlayer, bool playerInAttackRange)
        {
            switch (state.State)
            {
                case AiState.Idle:
                    if (playerInSight && distToPlayer <= def.VisionRange)
                        state.State = AiState.Aim;
                    break;
                case AiState.Aim:
                    if (!playerInSight || distToPlayer > def.VisionRange)
                    {
                        state.State = AiState.Idle;
                        break;
                    }
                    if (playerInAttackRange)
                        state.State = AiState.Combat; // Reposition
                    // Aim logic: wait for clear shot
                    break;
                case AiState.Combat:
                    if (playerInSight && distToPlayer <= def.VisionRange)
                        state.State = AiState.Aim;
                    else
                        state.State = AiState.Idle;
                    break;
            }
        }

        private void UpdateRusher(EnemyAIState state, EnemyDefinition def, float dt, bool playerInSight, float distToPlayer, bool playerInAttackRange)
        {
            switch (state.State)
            {
                case AiState.Idle:
                    if (playerInSight) state.State = AiState.Rush;
                    break;
                case AiState.Rush:
                    if (!playerInSight)
                    {
                        state.State = AiState.Idle;
                        break;
                    }
                    Vector3 rushDir = Vector3.Normalize(_playerPosition - state.Position);
                    state.Position += rushDir * def.Speed * dt;
                    if (distToPlayer <= def.AttackRange)
                    {
                        // Explode/Strike
                        state.State = AiState.Dead;
                    }
                    break;
            }
        }
    }
}