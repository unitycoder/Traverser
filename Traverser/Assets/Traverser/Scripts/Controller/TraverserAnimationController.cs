using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace Traverser
{
    [RequireComponent(typeof(TraverserCharacterController))]

    public class TraverserAnimationController : MonoBehaviour
    {
        // --- Attributes ---

        // --- Stores animator parameters for future reference ---
        public Dictionary<string, int> animatorParameters;

        public struct AnimatorParameters
        {
            public bool Move;
            public float Speed;
            public float Heading;

            public int MoveID;
            public int SpeedID;
            public int HeadingID;
        }

        [Header("Animation")]
        [Tooltip("Reference to the skeleton's parent. The controller positions the skeleton at the skeletonRef's position. Used to kill animation's root motion.")]
        public Transform skeleton;
        [Tooltip("Reference to the skeleton's reference position. A transform that follows the controller's object motion, with an offset to the bone position (f.ex hips).")]
        public Transform skeletonRef;

        [Header("Debug")]
        [Tooltip("If active, debug utilities will be shown (information/geometry draw). Select the object to show debug geometry.")]
        public bool debugDraw = false;

        [Tooltip("The radius of the spheres used to debug draw.")]
        [Range(0.1f, 1.0f)]
        public float contactDebugSphereRadius = 0.5f;

        // --- Animator and animation transition handler ---
        public Animator animator;
        public TraverserTransition transition;

        // --------------------------------

        // --- Use in case you do not want the skeleton to be adjusted to the reference in a custom transition ---
        public bool fakeTransition = false;

        // --- Private Variables ---

        private TraverserCharacterController controller;

        // --- Motion that has to be warped in the current frame given timeToTarget, pre deltaTime ---
        private Vector3 currentdeltaPosition;

        // --- Used to store last matchPosition, debug draw purposes ---
        private Vector3 lastWarpPosition;

        // --- Rotation that has to be warped in the current frame given timeToTarget, pre deltaTime ---
        private Quaternion currentdeltaRotation;

        // --- Difference between match position and current position ---
        private Vector3 deltaPosition;

        // --------------------------------

        private void Awake()
        {
            deltaPosition = Vector3.zero;
            currentdeltaPosition = Vector3.zero;
            lastWarpPosition = Vector3.zero;

            controller = GetComponent<TraverserCharacterController>();
        }

        private void Start()
        {
            transition = new TraverserTransition(this, ref controller);
            animatorParameters = new Dictionary<string, int>();

            // --- Save all animator parameter hashes for future reference ---
            foreach (AnimatorControllerParameter param in animator.parameters)
            {
                animatorParameters.Add(param.name, param.nameHash);
            }
        }

        // --- Basic Methods ---

        private void LateUpdate()
        {
            // --- Ensure the skeleton does not get separated from the controller (forcing in-place animation) ---
            if (!transition.isON && !fakeTransition)
                skeleton.transform.position = skeletonRef.transform.position;
            else
            {
                // --- Apply warping ---
                if (transition.isWarping)
                {
                    transform.position = Vector3.Lerp(transform.position, transform.position + currentdeltaPosition, Time.deltaTime);
                    transform.rotation = Quaternion.Slerp(transform.rotation, currentdeltaRotation, Time.deltaTime);
                }

                currentdeltaPosition = Vector3.zero;
            }
        }

        // --------------------------------

        // --- Utility Methods ---

        public void InitializeAnimatorParameters(ref AnimatorParameters parameters)
        {
            parameters.Move = false;
            parameters.Speed = 0.0f;
            parameters.Heading = 0.0f;
            parameters.MoveID = Animator.StringToHash("Move");
            parameters.SpeedID = Animator.StringToHash("Speed");
            parameters.HeadingID = Animator.StringToHash("Heading");
        }

        public void UpdateAnimator(ref AnimatorParameters parameters)
        {
            // --- Update animator with the given parameter's values ---
            animator.SetBool(parameters.MoveID, parameters.Move);
            animator.SetFloat(parameters.SpeedID, parameters.Speed);
            animator.SetFloat(parameters.HeadingID, parameters.Heading);
        }

        public bool WarpToTarget(Vector3 matchPosition, Quaternion matchRotation, float validDistance)
        {
            bool ret = true;

            // --- Warp position and rotation to match matchPosition and matchRotation ---  
            lastWarpPosition = matchPosition;

            //// --- Compute distance to match position and velocity ---
            Vector3 difference = matchPosition - transform.position;
            float time = animator.GetCurrentAnimatorStateInfo(0).length;
            Vector3 velocity = difference / time;

            // --- If velocity is zero, which means we just started warping, set it to one ---
            if (deltaPosition.magnitude < 0.01f || velocity.magnitude < 0.01f)
                velocity = controller.targetVelocity;

            // --- Compute time to reach match position ---
            float timeToTarget = 0.0f;

            if (velocity.magnitude > 0.01f
                && velocity.magnitude != float.PositiveInfinity
                && velocity.magnitude != float.NaN)
                timeToTarget = difference.magnitude / velocity.magnitude;

            // --- Finally compute the motion that has to be warped ---
            deltaPosition = difference;
            currentdeltaPosition = difference * timeToTarget;

            // TODO: For now, we do not want Y adjustments ---
            deltaPosition.y = 0.0f;
            currentdeltaPosition.y = 0.0f;

            // --- Compute the rotation that has to be warped ---
            currentdeltaRotation = Quaternion.Lerp(transform.rotation, matchRotation, timeToTarget); 

            // --- If close enough to validDistance, end warp ---

            // For now, let's trick the warper into believing there is no height difference
            Vector3 matchGround = matchPosition;
            matchGround.y = transform.position.y;

            // TODO: This distance takes into account Y, giving problems to valid distances of those transitions that do not use Y
            if (math.distance(transform.position, matchGround) < validDistance)
            {
                deltaPosition = Vector3.zero;
                currentdeltaPosition = Vector3.zero;
                currentdeltaRotation = Quaternion.identity;
                ret = false;
            }

            return ret;
        }

        public void SetRootMotion(bool rootMotion)
        {
            animator.applyRootMotion = rootMotion;
        }

        private void OnDrawGizmosSelected()
        {
            if (!debugDraw || controller == null)
                return;

            // --- Draw transition contact and target point ---
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(lastWarpPosition, contactDebugSphereRadius);
        }

        // --------------------------------

    }
}
