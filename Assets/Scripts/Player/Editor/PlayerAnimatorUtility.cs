using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace PlayerVisual.Editor
{
    /// <summary>
    /// Utility otomatis untuk memvalidasi dan memastikan Animator Controller Player
    /// memiliki parameter 'isHolding' dan 'pick', serta state animasi yang lengkap.
    /// Dijalankan secara otomatis saat editor load atau compile C#, serta dapat dipanggil manual lewat menu.
    /// </summary>
    [InitializeOnLoad]
    public static class PlayerAnimatorUtility
    {
        private const string ControllerPath = "Assets/Animations/Player.controller";
        private const string PickClipPath = "Assets/Animations/pick.anim";
        private const string PickupIdleClipPath = "Assets/Animations/pickup_idle.anim";
        private const string PickupWalkClipPath = "Assets/Animations/pickup_walk.anim";
        private const string WateringClipPath = "Assets/Animations/watering_plant.anim";
        private const string DiggingClipPath = "Assets/Animations/digging_animation.anim";

        static PlayerAnimatorUtility()
        {
            EditorApplication.delayCall += EnsureParametersAndStates;
        }

        [MenuItem("Tools/Ensure Player Animator Parameters & States")]
        public static void EnsureParametersAndStates()
        {
            // 1. Paksa re-import terlebih dahulu untuk memastikan sinkronisasi file sistem
            AssetDatabase.ImportAsset(ControllerPath, ImportAssetOptions.ForceUpdate);

            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                Debug.LogWarning($"[PlayerAnimatorUtility] Animator Controller tidak ditemukan di: {ControllerPath}");
                return;
            }

            bool changed = false;

            // 2. Pastikan parameter 'isHolding', 'pick', 'isWatering', dan 'isDigging' tersedia
            bool hasHolding = controller.parameters.Any(p => p.name == "isHolding");
            bool hasPick = controller.parameters.Any(p => p.name == "pick");
            bool hasWatering = controller.parameters.Any(p => p.name == "isWatering");
            bool hasDigging = controller.parameters.Any(p => p.name == "isDigging");

            if (!hasHolding)
            {
                controller.AddParameter("isHolding", AnimatorControllerParameterType.Bool);
                changed = true;
                Debug.Log("<color=green>[PlayerAnimatorUtility]</color> Parameter 'isHolding' (Bool) berhasil ditambahkan.");
            }

            if (!hasPick)
            {
                controller.AddParameter("pick", AnimatorControllerParameterType.Trigger);
                changed = true;
                Debug.Log("<color=green>[PlayerAnimatorUtility]</color> Parameter 'pick' (Trigger) berhasil ditambahkan.");
            }

            if (!hasWatering)
            {
                controller.AddParameter("isWatering", AnimatorControllerParameterType.Trigger);
                changed = true;
                Debug.Log("<color=green>[PlayerAnimatorUtility]</color> Parameter 'isWatering' (Trigger) berhasil ditambahkan.");
            }

            if (!hasDigging)
            {
                controller.AddParameter("isDigging", AnimatorControllerParameterType.Trigger);
                changed = true;
                Debug.Log("<color=green>[PlayerAnimatorUtility]</color> Parameter 'isDigging' (Trigger) berhasil ditambahkan.");
            }

            // 3. Pastikan State Machine dan Animasi Pickup terhubung
            if (controller.layers.Length > 0)
            {
                AnimatorStateMachine rootSm = controller.layers[0].stateMachine;
                AnimationClip pickClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(PickClipPath);
                AnimationClip pickupIdleClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(PickupIdleClipPath);
                AnimationClip pickupWalkClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(PickupWalkClipPath);

                AnimatorState idleState = rootSm.states.FirstOrDefault(s => s.state.name == "Idle").state;
                AnimatorState walkState = rootSm.states.FirstOrDefault(s => s.state.name == "Walk").state;
                AnimatorState pickState = rootSm.states.FirstOrDefault(s => s.state.name == "pick").state;
                AnimatorState pickupIdleState = rootSm.states.FirstOrDefault(s => s.state.name == "pickup_idle").state;
                AnimatorState pickupWalkState = rootSm.states.FirstOrDefault(s => s.state.name == "pickup_walk").state;

                if (pickState == null && pickClip != null)
                {
                    pickState = rootSm.AddState("pick", new Vector3(910, 160, 0));
                    pickState.motion = pickClip;
                    changed = true;
                }

                if (pickupIdleState == null && pickupIdleClip != null)
                {
                    pickupIdleState = rootSm.AddState("pickup_idle", new Vector3(945, 225, 0));
                    pickupIdleState.motion = pickupIdleClip;
                    changed = true;
                }

                if (pickupWalkState == null && pickupWalkClip != null)
                {
                    pickupWalkState = rootSm.AddState("pickup_walk", new Vector3(875, 95, 0));
                    pickupWalkState.motion = pickupWalkClip;
                    changed = true;
                }

                // AnyState -> pick
                if (pickState != null && !rootSm.anyStateTransitions.Any(t => t.destinationState == pickState))
                {
                    AnimatorStateTransition anyToPick = rootSm.AddAnyStateTransition(pickState);
                    anyToPick.duration = 0f;
                    anyToPick.hasExitTime = false;
                    anyToPick.AddCondition(AnimatorConditionMode.If, 0, "pick");
                    changed = true;
                }

                // pick -> pickup_walk
                if (pickState != null && pickupWalkState != null && !pickState.transitions.Any(t => t.destinationState == pickupWalkState))
                {
                    AnimatorStateTransition t = pickState.AddTransition(pickupWalkState);
                    t.duration = 0f;
                    t.hasExitTime = true;
                    t.exitTime = 1f;
                    t.AddCondition(AnimatorConditionMode.If, 0, "isHolding");
                    t.AddCondition(AnimatorConditionMode.If, 0, "isWalking");
                    changed = true;
                }

                // pick -> pickup_idle
                if (pickState != null && pickupIdleState != null && !pickState.transitions.Any(t => t.destinationState == pickupIdleState))
                {
                    AnimatorStateTransition t = pickState.AddTransition(pickupIdleState);
                    t.duration = 0f;
                    t.hasExitTime = true;
                    t.exitTime = 1f;
                    t.AddCondition(AnimatorConditionMode.If, 0, "isHolding");
                    t.AddCondition(AnimatorConditionMode.IfNot, 0, "isWalking");
                    changed = true;
                }

                // pickup_idle <-> pickup_walk
                if (pickupIdleState != null && pickupWalkState != null)
                {
                    if (!pickupIdleState.transitions.Any(t => t.destinationState == pickupWalkState))
                    {
                        AnimatorStateTransition t = pickupIdleState.AddTransition(pickupWalkState);
                        t.duration = 0f;
                        t.hasExitTime = false;
                        t.AddCondition(AnimatorConditionMode.If, 0, "isHolding");
                        t.AddCondition(AnimatorConditionMode.If, 0, "isWalking");
                        changed = true;
                    }

                    if (!pickupWalkState.transitions.Any(t => t.destinationState == pickupIdleState))
                    {
                        AnimatorStateTransition t = pickupWalkState.AddTransition(pickupIdleState);
                        t.duration = 0f;
                        t.hasExitTime = false;
                        t.AddCondition(AnimatorConditionMode.If, 0, "isHolding");
                        t.AddCondition(AnimatorConditionMode.IfNot, 0, "isWalking");
                        changed = true;
                    }
                }

                // Exit pickup: pickup_idle -> Idle
                if (pickupIdleState != null && idleState != null && !pickupIdleState.transitions.Any(t => t.destinationState == idleState))
                {
                    AnimatorStateTransition t = pickupIdleState.AddTransition(idleState);
                    t.duration = 0f;
                    t.hasExitTime = false;
                    t.AddCondition(AnimatorConditionMode.IfNot, 0, "isHolding");
                    changed = true;
                }

                // Exit pickup: pickup_walk -> Walk
                if (pickupWalkState != null && walkState != null && !pickupWalkState.transitions.Any(t => t.destinationState == walkState))
                {
                    AnimatorStateTransition t = pickupWalkState.AddTransition(walkState);
                    t.duration = 0f;
                    t.hasExitTime = false;
                    t.AddCondition(AnimatorConditionMode.IfNot, 0, "isHolding");
                    t.AddCondition(AnimatorConditionMode.If, 0, "isWalking");
                    changed = true;
                }

                // Exit pickup: pickup_walk -> Idle
                if (pickupWalkState != null && idleState != null && !pickupWalkState.transitions.Any(t => t.destinationState == idleState))
                {
                    AnimatorStateTransition t = pickupWalkState.AddTransition(idleState);
                    t.duration = 0f;
                    t.hasExitTime = false;
                    t.AddCondition(AnimatorConditionMode.IfNot, 0, "isHolding");
                    t.AddCondition(AnimatorConditionMode.IfNot, 0, "isWalking");
                    changed = true;
                }

                // 4. Pastikan State Machine dan Animasi Watering terhubung
                AnimationClip wateringClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(WateringClipPath);
                AnimatorState wateringState = rootSm.states.FirstOrDefault(s => s.state.name == "watering_plant").state;

                if (wateringState == null && wateringClip != null)
                {
                    wateringState = rootSm.AddState("watering_plant", new Vector3(300, 200, 0));
                    wateringState.motion = wateringClip;
                    changed = true;
                }
                else if (wateringState != null && wateringState.motion == null && wateringClip != null)
                {
                    wateringState.motion = wateringClip;
                    changed = true;
                }

                // AnyState -> watering_plant
                if (wateringState != null && !rootSm.anyStateTransitions.Any(t => t.destinationState == wateringState))
                {
                    AnimatorStateTransition anyToWater = rootSm.AddAnyStateTransition(wateringState);
                    anyToWater.duration = 0f;
                    anyToWater.hasExitTime = false;
                    anyToWater.canTransitionToSelf = false;
                    anyToWater.AddCondition(AnimatorConditionMode.If, 0, "isWatering");
                    changed = true;
                }

                // watering_plant -> Walk
                if (wateringState != null && walkState != null && !wateringState.transitions.Any(t => t.destinationState == walkState))
                {
                    AnimatorStateTransition toWalk = wateringState.AddTransition(walkState);
                    toWalk.duration = 0.05f;
                    toWalk.hasExitTime = true;
                    toWalk.exitTime = 1f;
                    toWalk.hasFixedDuration = true;
                    toWalk.AddCondition(AnimatorConditionMode.If, 0, "isWalking");
                    changed = true;
                }

                // watering_plant -> Idle
                if (wateringState != null && idleState != null && !wateringState.transitions.Any(t => t.destinationState == idleState))
                {
                    AnimatorStateTransition toIdle = wateringState.AddTransition(idleState);
                    toIdle.duration = 0.05f;
                    toIdle.hasExitTime = true;
                    toIdle.exitTime = 1f;
                    toIdle.hasFixedDuration = true;
                    toIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "isWalking");
                    changed = true;
                }

                // 5. Pastikan State Machine dan Animasi Digging terhubung
                AnimationClip diggingClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(DiggingClipPath);
                AnimatorState diggingState = rootSm.states.FirstOrDefault(s => s.state.name == "digging_animation").state;

                if (diggingState == null && diggingClip != null)
                {
                    diggingState = rootSm.AddState("digging_animation", new Vector3(300, 260, 0));
                    diggingState.motion = diggingClip;
                    changed = true;
                }
                else if (diggingState != null && diggingState.motion == null && diggingClip != null)
                {
                    diggingState.motion = diggingClip;
                    changed = true;
                }

                // AnyState -> digging_animation
                if (diggingState != null && !rootSm.anyStateTransitions.Any(t => t.destinationState == diggingState))
                {
                    AnimatorStateTransition anyToDig = rootSm.AddAnyStateTransition(diggingState);
                    anyToDig.duration = 0f;
                    anyToDig.hasExitTime = false;
                    anyToDig.canTransitionToSelf = false;
                    anyToDig.AddCondition(AnimatorConditionMode.If, 0, "isDigging");
                    changed = true;
                }

                // digging_animation -> Walk
                if (diggingState != null && walkState != null && !diggingState.transitions.Any(t => t.destinationState == walkState))
                {
                    AnimatorStateTransition toWalk = diggingState.AddTransition(walkState);
                    toWalk.duration = 0.05f;
                    toWalk.hasExitTime = true;
                    toWalk.exitTime = 1f;
                    toWalk.hasFixedDuration = true;
                    toWalk.AddCondition(AnimatorConditionMode.If, 0, "isWalking");
                    changed = true;
                }

                // digging_animation -> Idle
                if (diggingState != null && idleState != null && !diggingState.transitions.Any(t => t.destinationState == idleState))
                {
                    AnimatorStateTransition toIdle = diggingState.AddTransition(idleState);
                    toIdle.duration = 0.05f;
                    toIdle.hasExitTime = true;
                    toIdle.exitTime = 1f;
                    toIdle.hasFixedDuration = true;
                    toIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "isWalking");
                    changed = true;
                }
            }

            if (changed)
            {
                EditorUtility.SetDirty(controller);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(ControllerPath, ImportAssetOptions.ForceUpdate);
                Debug.Log("<color=green>[PlayerAnimatorUtility]</color> Player.controller berhasil diperbarui dan disinkronkan.");
            }
        }
    }
}
